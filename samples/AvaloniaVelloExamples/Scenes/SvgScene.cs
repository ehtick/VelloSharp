using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;
using Avalonia.Media;
using SkiaSharp;
using VelloSharp;
using VelloFillRule = VelloSharp.FillRule;

namespace AvaloniaVelloExamples.Scenes;

internal sealed class SvgScene
{
    private readonly SvgSceneData _data;

    private SvgScene(SvgSceneData data)
    {
        _data = data;
    }

    public static SvgScene FromFile(string path)
    {
        var content = File.ReadAllText(path);
        return FromContent(content);
    }

    public static SvgScene FromContent(string content)
    {
        var data = SvgParser.Parse(content);
        return new SvgScene(data);
    }

    public void Render(Scene target, SceneParams parameters)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(parameters);

        parameters.Resolution = _data.Resolution;

        var builder = new PathBuilder();
        foreach (var command in _data.Commands)
        {
            BuildPath(builder, command.Segments);
            if (command.Type == SvgCommandType.Fill)
            {
                target.FillPath(builder, VelloFillRule.NonZero, command.Transform, command.Color);
            }
            else if (command.Type == SvgCommandType.Stroke)
            {
                var stroke = new StrokeStyle { Width = command.StrokeWidth };
                target.StrokePath(builder, stroke, command.Transform, command.Color);
            }
            builder.Clear();
        }
    }

    private static void BuildPath(PathBuilder builder, ReadOnlySpan<SvgPathSegment> segments)
    {
        builder.Clear();
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            switch (segment.Verb)
            {
                case SvgPathVerb.MoveTo:
                    builder.MoveTo(segment.P0.X, segment.P0.Y);
                    break;
                case SvgPathVerb.LineTo:
                    builder.LineTo(segment.P0.X, segment.P0.Y);
                    break;
                case SvgPathVerb.QuadTo:
                    builder.QuadraticTo(segment.P0.X, segment.P0.Y, segment.P1.X, segment.P1.Y);
                    break;
                case SvgPathVerb.CubicTo:
                    builder.CubicTo(segment.P0.X, segment.P0.Y, segment.P1.X, segment.P1.Y, segment.P2.X, segment.P2.Y);
                    break;
                case SvgPathVerb.Close:
                    builder.Close();
                    break;
            }
        }
    }

    private enum SvgCommandType
    {
        Fill,
        Stroke,
    }

    private readonly record struct SvgCommand(
        SvgCommandType Type,
        Matrix3x2 Transform,
        RgbaColor Color,
        float StrokeWidth,
        SvgPathSegment[] Segments);

    private enum SvgPathVerb
    {
        MoveTo,
        LineTo,
        QuadTo,
        CubicTo,
        Close,
    }

    private readonly struct SvgPathSegment
    {
        public SvgPathSegment(SvgPathVerb verb, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            Verb = verb;
            P0 = p0;
            P1 = p1;
            P2 = p2;
        }

        public SvgPathVerb Verb { get; }
        public Vector2 P0 { get; }
        public Vector2 P1 { get; }
        public Vector2 P2 { get; }
    }

    private sealed record SvgSceneData(IReadOnlyList<SvgCommand> Commands, Vector2 Resolution);

    private static class SvgParser
    {
        private static readonly SvgStyle DefaultStyle = new(new RgbaColor(0f, 0f, 0f, 1f), null, 1f, 1f);

        public static SvgSceneData Parse(string content)
        {
            var commands = new List<SvgCommand>();
            var document = XDocument.Parse(content);
            var root = document.Root ?? throw new InvalidOperationException("SVG content has no root element.");

            var (initialTransform, size) = ExtractRootTransform(root);
            Traverse(root, initialTransform, DefaultStyle, commands);

            return new SvgSceneData(commands, size);
        }

        private static void Traverse(XElement element, Matrix3x2 currentTransform, SvgStyle currentStyle, List<SvgCommand> commands)
        {
            var style = ApplyStyle(element, currentStyle);
            var transform = Multiply(currentTransform, ParseTransform(element.Attribute("transform")?.Value));

            foreach (var child in element.Elements())
            {
                switch (child.Name.LocalName)
                {
                    case "g":
                    case "svg":
                        Traverse(child, transform, style, commands);
                        break;
                    case "path":
                        ProcessPath(child, transform, style, commands);
                        break;
                    default:
                        Traverse(child, transform, style, commands);
                        break;
                }
            }
        }

        private static void ProcessPath(XElement element, Matrix3x2 transform, SvgStyle style, List<SvgCommand> commands)
        {
            var data = element.Attribute("d")?.Value;
            if (string.IsNullOrWhiteSpace(data))
            {
                return;
            }

            var segments = ConvertPathData(data);
            if (segments.Length == 0)
            {
                return;
            }

            if (style.Fill is { } fill)
            {
                var color = ApplyOpacity(fill, style.Opacity);
                commands.Add(new SvgCommand(SvgCommandType.Fill, transform, color, 0f, segments));
            }

            if (style.Stroke is { } stroke && style.StrokeWidth > 0f)
            {
                var color = ApplyOpacity(stroke, style.Opacity);
                commands.Add(new SvgCommand(SvgCommandType.Stroke, transform, color, style.StrokeWidth, segments));
            }
        }

        private static (Matrix3x2 Transform, Vector2 Size) ExtractRootTransform(XElement root)
        {
            double? width = TryParseDouble(root.Attribute("width")?.Value);
            double? height = TryParseDouble(root.Attribute("height")?.Value);

            (double X, double Y)? viewBoxOrigin = null;
            (double Width, double Height)? viewBoxSize = null;
            var viewBoxAttr = root.Attribute("viewBox")?.Value;
            if (!string.IsNullOrWhiteSpace(viewBoxAttr))
            {
                var parts = viewBoxAttr.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 4 &&
                    double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                    double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var w) &&
                    double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
                {
                    viewBoxOrigin = (x, y);
                    viewBoxSize = (w, h);
                }
            }

            var transform = Matrix3x2.Identity;
            if (viewBoxOrigin is { } origin)
            {
                transform = Multiply(transform, Matrix3x2.CreateTranslation((float)(-origin.X), (float)(-origin.Y)));
            }

            if (width is null && height is null && viewBoxSize is { } viewBoxA)
            {
                // No explicit size, use viewBox dimensions.
            }
            else if (width is { } w && height is { } h && viewBoxSize is { } viewBoxB)
            {
                var scale = Matrix3x2.CreateScale((float)(w / viewBoxB.Width), (float)(h / viewBoxB.Height));
                transform = Multiply(transform, scale);
            }
            else if (width is { } onlyWidth && viewBoxSize is { } viewBoxC)
            {
                var scale = (float)(onlyWidth / viewBoxC.Width);
                transform = Multiply(transform, Matrix3x2.CreateScale(scale));
            }
            else if (height is { } onlyHeight && viewBoxSize is { } viewBoxD)
            {
                var scale = (float)(onlyHeight / viewBoxD.Height);
                transform = Multiply(transform, Matrix3x2.CreateScale(scale));
            }

            Vector2 size;
            if (width is null && height is null && viewBoxSize is { } vbSize)
            {
                size = new Vector2((float)vbSize.Width, (float)vbSize.Height);
            }
            else if (width is { } wFinal && height is { } hFinal)
            {
                size = new Vector2((float)wFinal, (float)hFinal);
            }
            else if (width is { } wOnly && viewBoxSize is { } vbWidth)
            {
                var computedHeight = (float)(wOnly / vbWidth.Width * vbWidth.Height);
                size = new Vector2((float)wOnly, computedHeight);
            }
            else if (height is { } hOnly && viewBoxSize is { } vbHeight)
            {
                var computedWidth = (float)(hOnly / vbHeight.Height * vbHeight.Width);
                size = new Vector2(computedWidth, (float)hOnly);
            }
            else
            {
                size = new Vector2(300f, 150f);
            }

            return (transform, size);
        }

        private static SvgStyle ApplyStyle(XElement element, SvgStyle parent)
        {
            var styleMap = ParseStyle(element.Attribute("style")?.Value);

            string? GetValue(string name)
            {
                if (element.Attribute(name) is { } attr && !string.IsNullOrWhiteSpace(attr.Value))
                {
                    return attr.Value;
                }

                if (styleMap is not null && styleMap.TryGetValue(name, out var value))
                {
                    return value;
                }

                return null;
            }

            var opacity = parent.Opacity * ParseFloat(GetValue("opacity"), 1f);

            var fill = parent.Fill;
            var fillValue = GetValue("fill");
            if (fillValue is { } fv)
            {
                if (fv.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    fill = null;
                }
                else if (TryParseColor(fv, out var color))
                {
                    fill = color;
                }
            }
            var fillOpacity = ParseFloat(GetValue("fill-opacity"), 1f);
            if (fill is { } existingFill)
            {
                fill = existingFill with { A = existingFill.A * fillOpacity };
            }

            var stroke = parent.Stroke;
            var strokeValue = GetValue("stroke");
            if (strokeValue is { } sv)
            {
                if (sv.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    stroke = null;
                }
                else if (TryParseColor(sv, out var color))
                {
                    stroke = color;
                }
            }
            var strokeOpacity = ParseFloat(GetValue("stroke-opacity"), 1f);
            if (stroke is { } existingStroke)
            {
                stroke = existingStroke with { A = existingStroke.A * strokeOpacity };
            }

            var strokeWidth = parent.StrokeWidth;
            var strokeWidthValue = GetValue("stroke-width");
            if (strokeWidthValue is { } sw && TryParseDouble(sw) is { } width)
            {
                strokeWidth = (float)width;
            }

            return parent with
            {
                Fill = fill,
                Stroke = stroke,
                StrokeWidth = strokeWidth,
                Opacity = opacity,
            };
        }

        private static Dictionary<string, string>? ParseStyle(string? style)
        {
            if (string.IsNullOrWhiteSpace(style))
            {
                return null;
            }

            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var pairs = style.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var parts = pair.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    result[parts[0].Trim()] = parts[1].Trim();
                }
            }

            return result;
        }

        private static Matrix3x2 ParseTransform(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Matrix3x2.Identity;
            }

            var result = Matrix3x2.Identity;
            var remaining = value;
            while (!string.IsNullOrWhiteSpace(remaining))
            {
                remaining = remaining.Trim();
                if (remaining.StartsWith("matrix", StringComparison.OrdinalIgnoreCase))
                {
                    var parameters = ExtractParameters(remaining, "matrix");
                    if (parameters.Length == 6)
                    {
                        result = Multiply(result, new Matrix3x2(
                            (float)parameters[0],
                            (float)parameters[1],
                            (float)parameters[2],
                            (float)parameters[3],
                            (float)parameters[4],
                            (float)parameters[5]));
                    }
                    remaining = Advance(remaining, "matrix");
                }
                else if (remaining.StartsWith("translate", StringComparison.OrdinalIgnoreCase))
                {
                    var parameters = ExtractParameters(remaining, "translate");
                    var x = parameters.Length > 0 ? (float)parameters[0] : 0f;
                    var y = parameters.Length > 1 ? (float)parameters[1] : 0f;
                    result = Multiply(result, Matrix3x2.CreateTranslation(x, y));
                    remaining = Advance(remaining, "translate");
                }
                else if (remaining.StartsWith("scale", StringComparison.OrdinalIgnoreCase))
                {
                    var parameters = ExtractParameters(remaining, "scale");
                    var sx = parameters.Length > 0 ? (float)parameters[0] : 1f;
                    var sy = parameters.Length > 1 ? (float)parameters[1] : sx;
                    result = Multiply(result, Matrix3x2.CreateScale(sx, sy));
                    remaining = Advance(remaining, "scale");
                }
                else if (remaining.StartsWith("rotate", StringComparison.OrdinalIgnoreCase))
                {
                    var parameters = ExtractParameters(remaining, "rotate");
                    if (parameters.Length >= 1)
                    {
                        var angle = DegreesToRadians(parameters[0]);
                        if (parameters.Length >= 3)
                        {
                            var center = new Vector2((float)parameters[1], (float)parameters[2]);
                            var toOrigin = Matrix3x2.CreateTranslation(-center);
                            var rotation = Matrix3x2.CreateRotation((float)angle);
                            var back = Matrix3x2.CreateTranslation(center);
                            result = Multiply(result, toOrigin * rotation * back);
                        }
                        else
                        {
                            result = Multiply(result, Matrix3x2.CreateRotation((float)angle));
                        }
                    }
                    remaining = Advance(remaining, "rotate");
                }
                else if (remaining.StartsWith("skewX", StringComparison.OrdinalIgnoreCase))
                {
                    var parameters = ExtractParameters(remaining, "skewX");
                    if (parameters.Length >= 1)
                    {
                        var angle = DegreesToRadians(parameters[0]);
                        result = Multiply(result, new Matrix3x2(1f, 0f, (float)Math.Tan(angle), 1f, 0f, 0f));
                    }
                    remaining = Advance(remaining, "skewX");
                }
                else if (remaining.StartsWith("skewY", StringComparison.OrdinalIgnoreCase))
                {
                    var parameters = ExtractParameters(remaining, "skewY");
                    if (parameters.Length >= 1)
                    {
                        var angle = DegreesToRadians(parameters[0]);
                        result = Multiply(result, new Matrix3x2(1f, (float)Math.Tan(angle), 0f, 1f, 0f, 0f));
                    }
                    remaining = Advance(remaining, "skewY");
                }
                else
                {
                    break;
                }
            }

            return result;
        }

        private static double[] ExtractParameters(string input, string prefix)
        {
            var start = input.IndexOf('(');
            var end = input.IndexOf(')');
            if (start < 0 || end <= start)
            {
                return Array.Empty<double>();
            }

            var substring = input.Substring(start + 1, end - start - 1);
            var parts = substring.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var values = new double[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                _ = double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]);
            }
            return values;
        }

        private static string Advance(string input, string prefix)
        {
            var closeIndex = input.IndexOf(')');
            if (closeIndex < 0)
            {
                return string.Empty;
            }
            return input[(closeIndex + 1)..];
        }

        private static Matrix3x2 Multiply(Matrix3x2 left, Matrix3x2 right) => left * right;

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

        private static SvgPathSegment[] ConvertPathData(string data)
        {
            using var path = SKPath.ParseSvgPathData(data);
            var iterator = path.CreateRawIterator();
            var points = new SKPoint[4];
            var segments = new List<SvgPathSegment>();

            while (true)
            {
                var verb = iterator.Next(points);
                if (verb == SKPathVerb.Done)
                {
                    break;
                }

                switch (verb)
                {
                    case SKPathVerb.Move:
                        segments.Add(new SvgPathSegment(SvgPathVerb.MoveTo, ToVector(points[0]), default, default));
                        break;
                    case SKPathVerb.Line:
                        segments.Add(new SvgPathSegment(SvgPathVerb.LineTo, ToVector(points[1]), default, default));
                        break;
                    case SKPathVerb.Quad:
                        segments.Add(new SvgPathSegment(SvgPathVerb.QuadTo, ToVector(points[1]), ToVector(points[2]), default));
                        break;
                    case SKPathVerb.Conic:
                        var weight = iterator.ConicWeight();
                        var quadPoints = SKPath.ConvertConicToQuads(points[0], points[1], points[2], weight, 8);
                        for (var i = 0; i + 2 < quadPoints.Length; i += 2)
                        {
                            segments.Add(new SvgPathSegment(
                                SvgPathVerb.QuadTo,
                                ToVector(quadPoints[i + 1]),
                                ToVector(quadPoints[i + 2]),
                                default));
                        }
                        break;
                    case SKPathVerb.Cubic:
                        segments.Add(new SvgPathSegment(
                            SvgPathVerb.CubicTo,
                            ToVector(points[1]),
                            ToVector(points[2]),
                            ToVector(points[3])));
                        break;
                    case SKPathVerb.Close:
                        segments.Add(new SvgPathSegment(SvgPathVerb.Close, default, default, default));
                        break;
                }
            }

            return segments.ToArray();
        }

        private static Vector2 ToVector(SKPoint point) => new(point.X, point.Y);

        private static bool TryParseColor(string value, out RgbaColor color)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                color = default;
                return false;
            }

            if (value.StartsWith("#", StringComparison.Ordinal))
            {
                if (Color.TryParse(value, out var parsed))
                {
                    color = ToRgbaColor(parsed);
                    return true;
                }
            }
            else if (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                var start = value.IndexOf('(');
                var end = value.IndexOf(')');
                if (start >= 0 && end > start)
                {
                    var inner = value.Substring(start + 1, end - start - 1);
                    var parts = inner.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        var r = ParseColorComponent(parts[0]);
                        var g = ParseColorComponent(parts[1]);
                        var b = ParseColorComponent(parts[2]);
                        var a = parts.Length >= 4 ? ParseColorComponent(parts[3], true) : 1f;
                        color = new RgbaColor(r, g, b, a);
                        return true;
                    }
                }
            }
            else if (Color.TryParse(value, out var named))
            {
                color = ToRgbaColor(named);
                return true;
            }

            color = default;
            return false;
        }

        private static float ParseColorComponent(string value, bool allowAlpha = false)
        {
            value = value.Trim();
            if (value.EndsWith("%", StringComparison.Ordinal))
            {
                if (float.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
                {
                    return Math.Clamp(percent / 100f, 0f, 1f);
                }
            }
            else if (allowAlpha && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var alphaComponent))
            {
                return Math.Clamp(alphaComponent, 0f, 1f);
            }
            else if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var component))
            {
                return Math.Clamp(component / 255f, 0f, 1f);
            }

            return 0f;
        }

        private static RgbaColor ToRgbaColor(Color color)
            => new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

        private static RgbaColor ApplyOpacity(RgbaColor color, float opacity)
        {
            var alpha = Math.Clamp(color.A * opacity, 0f, 1f);
            return color with { A = alpha };
        }

        private static float ParseFloat(string? value, float fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static double? TryParseDouble(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        }

    }

    private readonly record struct SvgStyle(RgbaColor? Fill, RgbaColor? Stroke, float StrokeWidth, float Opacity);
}
