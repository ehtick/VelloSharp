using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Numerics;
using System.Text;
using VelloSharp;
using VelloSharp.Text;
using VelloSharp.Windows;

namespace VelloSharp.WinForms;

public sealed class VelloGraphics
{
    private static readonly LayerBlend s_clipLayerBlend = new(LayerMix.Clip, LayerCompose.SrcOver);

    private readonly VelloGraphicsSession _session;
    private readonly Scene _scene;
    private readonly Stack<VelloGraphicsState> _stateStack = new();

    private RenderState _currentState;
    private int _activeLayerDepth;
    private int _nextStateId = 1;

    public VelloGraphics(VelloGraphicsSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _scene = session.Scene ?? throw new ArgumentNullException(nameof(session.Scene));
        _currentState = RenderState.CreateDefault();
    }

    public Matrix3x2 Transform
    {
        get => _currentState.Transform;
        set => _currentState.Transform = value;
    }

    public SmoothingMode SmoothingMode
    {
        get => _currentState.SmoothingMode;
        set => _currentState.SmoothingMode = value;
    }

    public PixelOffsetMode PixelOffsetMode
    {
        get => _currentState.PixelOffsetMode;
        set => _currentState.PixelOffsetMode = value;
    }

    public InterpolationMode InterpolationMode
    {
        get => _currentState.InterpolationMode;
        set => _currentState.InterpolationMode = value;
    }

    public CompositingMode CompositingMode
    {
        get => _currentState.CompositingMode;
        set
        {
            if (value != CompositingMode.SourceOver)
            {
                throw new NotSupportedException($"Compositing mode '{value}' is not supported.");
            }

            _currentState.CompositingMode = value;
        }
    }

    public CompositingQuality CompositingQuality
    {
        get => _currentState.CompositingQuality;
        set => _currentState.CompositingQuality = value;
    }

    public TextRenderingHint TextRenderingHint
    {
        get => _currentState.TextRenderingHint;
        set => _currentState.TextRenderingHint = value;
    }

    public VelloRegion? Clip => _currentState.Clip?.Clone();

    public RectangleF ClipBounds
    {
        get
        {
            if (_currentState.Clip is { } region)
            {
                var bounds = region.GetBounds();
                if (!bounds.IsEmpty)
                {
                    return bounds;
                }
            }

            return new RectangleF(0, 0, _session.Width, _session.Height);
        }
    }

    public float DpiX => 96f;

    public float DpiY => 96f;

    public VelloGraphicsState Save()
    {
        var clipClone = _currentState.Clip?.Clone();
        var state = new VelloGraphicsState(
            _nextStateId++,
            _currentState.Transform,
            _stateStack.Count,
            _activeLayerDepth,
            _currentState.SmoothingMode,
            _currentState.PixelOffsetMode,
            _currentState.InterpolationMode,
            _currentState.CompositingMode,
            _currentState.CompositingQuality,
            _currentState.TextRenderingHint,
            clipClone);
        _stateStack.Push(state);
        return state;
    }

    public void Restore()
    {
        if (_stateStack.Count == 0)
        {
            throw new InvalidOperationException("There is no saved graphics state to restore.");
        }

        var state = _stateStack.Pop();
        PopLayersToDepth(state.LayerDepth);
        ApplyState(state);
    }

    public void Restore(VelloGraphicsState state)
    {
        if (_stateStack.Count == 0)
        {
            throw new InvalidOperationException("There is no saved graphics state to restore.");
        }

        var found = false;
        while (_stateStack.Count > 0)
        {
            var current = _stateStack.Pop();
            PopLayersToDepth(current.LayerDepth);
            if (current.StateId == state.StateId)
            {
                ApplyState(current);
                found = true;
                break;
            }
        }

        if (!found)
        {
            throw new InvalidOperationException("The provided graphics state is no longer valid.");
        }
    }

    public void ResetTransform() => _currentState.Transform = Matrix3x2.Identity;

    public void MultiplyTransform(Matrix3x2 matrix, MatrixOrder order = MatrixOrder.Prepend)
    {
        _currentState.Transform = order switch
        {
            MatrixOrder.Append => Matrix3x2.Multiply(_currentState.Transform, matrix),
            _ => Matrix3x2.Multiply(matrix, _currentState.Transform),
        };
    }

    public void TranslateTransform(float dx, float dy, MatrixOrder order = MatrixOrder.Prepend)
        => MultiplyTransform(Matrix3x2.CreateTranslation(dx, dy), order);

    public void ScaleTransform(float sx, float sy, MatrixOrder order = MatrixOrder.Prepend)
        => MultiplyTransform(Matrix3x2.CreateScale(sx, sy), order);

    public void RotateTransform(float angleDegrees, MatrixOrder order = MatrixOrder.Prepend)
    {
        var radians = (float)(Math.PI / 180.0) * angleDegrees;
        MultiplyTransform(Matrix3x2.CreateRotation(radians), order);
    }

    public void Clear(Color color)
        => FillRectangle(color, new RectangleF(0, 0, _session.Width, _session.Height));

    public void FillRectangle(Color color, RectangleF rect)
    {
        rect = NormalizeRectangle(rect);
        using var brush = new VelloSolidBrush(color);
        FillRectangle((VelloBrush)brush, rect);
    }

    public void FillRectangle(RgbaColor color, RectangleF rect)
    {
        rect = NormalizeRectangle(rect);
        var path = BuildRectanglePath(rect);
        _scene.FillPath(path, FillRule.NonZero, _currentState.Transform, color);
    }

    public void FillRectangle(VelloBrush brush, RectangleF rect)
    {
        ArgumentNullException.ThrowIfNull(brush);
        rect = NormalizeRectangle(rect);
        var path = BuildRectanglePath(rect);
        FillWithBrush(path, FillRule.NonZero, brush);
    }

    public void DrawRectangle(Color color, float thickness, RectangleF rect)
    {
        using var pen = new VelloPen(color, thickness);
        DrawRectangle(pen, rect);
    }

    public void DrawRectangle(VelloPen pen, RectangleF rect)
    {
        ValidatePen(pen);
        rect = NormalizeRectangle(rect);
        var path = BuildRectanglePath(rect);
        StrokePath(path, pen);
    }

    public void DrawLine(VelloPen pen, PointF pt1, PointF pt2)
    {
        ValidatePen(pen);
        var path = new PathBuilder();
        path.MoveTo(pt1.X, pt1.Y);
        path.LineTo(pt2.X, pt2.Y);
        StrokePath(path, pen);
    }

    public void DrawLines(VelloPen pen, ReadOnlySpan<PointF> points)
    {
        ValidatePen(pen);
        if (points.Length < 2)
        {
            throw new ArgumentException("At least two points are required.", nameof(points));
        }

        var path = new PathBuilder();
        path.MoveTo(points[0].X, points[0].Y);
        for (var i = 1; i < points.Length; i++)
        {
            path.LineTo(points[i].X, points[i].Y);
        }

        StrokePath(path, pen);
    }

    public void DrawPolygon(VelloPen pen, ReadOnlySpan<PointF> points)
    {
        ValidatePen(pen);
        if (points.Length < 3)
        {
            throw new ArgumentException("At least three points are required.", nameof(points));
        }

        var path = new PathBuilder();
        path.MoveTo(points[0].X, points[0].Y);
        for (var i = 1; i < points.Length; i++)
        {
            path.LineTo(points[i].X, points[i].Y);
        }

        path.Close();
        StrokePath(path, pen);
    }

    public void FillPolygon(VelloBrush brush, ReadOnlySpan<PointF> points, FillMode fillMode = FillMode.Alternate)
    {
        ArgumentNullException.ThrowIfNull(brush);
        if (points.Length < 3)
        {
            throw new ArgumentException("At least three points are required.", nameof(points));
        }

        var path = new PathBuilder();
        path.MoveTo(points[0].X, points[0].Y);
        for (var i = 1; i < points.Length; i++)
        {
            path.LineTo(points[i].X, points[i].Y);
        }

        path.Close();
        FillWithBrush(path, ToFillRule(fillMode), brush);
    }

    public void DrawEllipse(VelloPen pen, RectangleF rect)
    {
        ValidatePen(pen);
        rect = NormalizeRectangle(rect);
        var path = BuildEllipsePath(rect);
        StrokePath(path, pen);
    }

    public void FillEllipse(VelloBrush brush, RectangleF rect)
    {
        ArgumentNullException.ThrowIfNull(brush);
        rect = NormalizeRectangle(rect);
        var path = BuildEllipsePath(rect);
        FillWithBrush(path, FillRule.NonZero, brush);
    }

    public void DrawPath(VelloPen pen, VelloGraphicsPath path)
    {
        ValidatePen(pen);
        ArgumentNullException.ThrowIfNull(path);
        var builder = path.ToPathBuilder();
        StrokePath(builder, pen);
    }

    public void FillPath(VelloBrush brush, VelloGraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(brush);
        ArgumentNullException.ThrowIfNull(path);
        var builder = path.ToPathBuilder();
        FillWithBrush(builder, ToFillRule(path.FillMode), brush);
    }

    public void FillRegion(VelloBrush brush, VelloRegion region)
    {
        ArgumentNullException.ThrowIfNull(brush);
        ArgumentNullException.ThrowIfNull(region);
        var builder = region.ToPathBuilder();
        FillWithBrush(builder, ToFillRule(region.FillMode), brush);
    }

    public void ResetClip()
    {
        PopLayersToDepth(0);
        _currentState.Clip = null;
    }

    public void SetClip(RectangleF rectangle, CombineMode combineMode = CombineMode.Replace)
    {
        rectangle = NormalizeRectangle(rectangle);
        var path = BuildRectanglePath(rectangle);
        ApplyClip(path, combineMode, new VelloRegion(rectangle));
    }

    public void SetClip(VelloGraphicsPath path, CombineMode combineMode = CombineMode.Replace)
    {
        ArgumentNullException.ThrowIfNull(path);
        var builder = path.ToPathBuilder();
        ApplyClip(builder, combineMode, new VelloRegion(path));
    }

    public void SetClip(VelloRegion region, CombineMode combineMode = CombineMode.Replace)
    {
        ArgumentNullException.ThrowIfNull(region);
        var builder = region.ToPathBuilder();
        ApplyClip(builder, combineMode, region.Clone());
    }

    public void DrawImage(VelloBitmap bitmap, PointF point)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var dest = new RectangleF(point.X, point.Y, bitmap.Width, bitmap.Height);
        DrawImage(bitmap, dest);
    }

    public void DrawImage(VelloBitmap bitmap, RectangleF destRect)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        destRect = NormalizeRectangle(destRect);
        if (destRect.Width <= 0 || destRect.Height <= 0)
        {
            return;
        }

        var sourceRect = new RectangleF(0, 0, bitmap.Width, bitmap.Height);
        DrawImage(bitmap, destRect, sourceRect, GraphicsUnit.Pixel);
    }

    public void DrawImage(VelloBitmap bitmap, RectangleF destRect, RectangleF srcRect, GraphicsUnit srcUnit)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (srcUnit != GraphicsUnit.Pixel)
        {
            throw new NotSupportedException($"Graphics unit '{srcUnit}' is not supported.");
        }

        destRect = NormalizeRectangle(destRect);
        srcRect = NormalizeRectangle(srcRect);
        if (destRect.Width <= 0 || destRect.Height <= 0 || srcRect.Width <= 0 || srcRect.Height <= 0)
        {
            return;
        }

        var scaleX = destRect.Width / srcRect.Width;
        var scaleY = destRect.Height / srcRect.Height;
        var translationX = destRect.Left - srcRect.Left * scaleX;
        var translationY = destRect.Top - srcRect.Top * scaleY;

        var local = Matrix3x2.CreateScale(scaleX, scaleY);
        local.Translation = new Vector2(translationX, translationY);

        var transform = Matrix3x2.Multiply(local, _currentState.Transform);
        var brush = new ImageBrush(bitmap.Image)
        {
            Alpha = 1f,
            Quality = ImageQuality.High,
        };

        _scene.DrawImage(brush, transform);
    }

    public void DrawString(string text, VelloFont font, VelloBrush brush, PointF origin, VelloStringFormat? format = null)
        => DrawString(text, font, brush, new RectangleF(origin.X, origin.Y, float.PositiveInfinity, float.PositiveInfinity), format, useBoundingBox: false);

    public void DrawString(string text, VelloFont font, VelloBrush brush, RectangleF layoutRect, VelloStringFormat? format = null)
        => DrawString(text, font, brush, layoutRect, format, useBoundingBox: true);

    public void DrawString(string text, VelloFont font, Color color, PointF origin, VelloStringFormat? format = null)
    {
        using var brush = new VelloSolidBrush(color);
        DrawString(text, font, brush, origin, format);
    }

    public void DrawString(string text, VelloFont font, Color color, RectangleF layoutRect, VelloStringFormat? format = null)
    {
        using var brush = new VelloSolidBrush(color);
        DrawString(text, font, brush, layoutRect, format);
    }

    public SizeF MeasureString(string text, VelloFont font, VelloStringFormat? format = null)
    {
        ArgumentNullException.ThrowIfNull(font);
        format ??= VelloStringFormat.GenericDefault;
        var layout = BuildTextLayout(text ?? string.Empty, font, format);
        var width = layout.MaxAdvance;
        var height = layout.TotalHeight <= 0 ? layout.LineHeight : layout.TotalHeight;
        return new SizeF(width, height);
    }

    private void DrawString(string text, VelloFont font, VelloBrush brush, RectangleF layoutRect, VelloStringFormat? format, bool useBoundingBox)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(brush);
        format ??= VelloStringFormat.GenericDefault;

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var layout = BuildTextLayout(text, font, format);
        if (layout.Lines.Count == 0)
        {
            return;
        }

        var brushCore = brush.CreateCoreBrush(out _);
        var worldTransform = _currentState.Transform;

        float availableWidth = useBoundingBox && float.IsFinite(layoutRect.Width) ? layoutRect.Width : layout.MaxAdvance;
        var offsetX = layoutRect.Left;
        var offsetY = layoutRect.Top;

        if (useBoundingBox)
        {
            if (!float.IsFinite(layoutRect.Width) || layoutRect.Width <= 0)
            {
                availableWidth = layout.MaxAdvance;
            }

            if (!float.IsFinite(layoutRect.Height) || layoutRect.Height <= 0)
            {
                offsetY = layoutRect.Top;
            }

            var verticalRemaining = layoutRect.Height - layout.TotalHeight;
            if (format.LineAlignment == StringAlignment.Center)
            {
                offsetY += verticalRemaining > 0 ? verticalRemaining / 2f : 0f;
            }
            else if (format.LineAlignment == StringAlignment.Far)
            {
                offsetY += verticalRemaining > 0 ? verticalRemaining : 0f;
            }
        }
        else
        {
            offsetX = layoutRect.Left;
            offsetY = layoutRect.Top;
            availableWidth = layout.MaxAdvance;
        }

        var rtl = (format.FormatFlags & StringFormatFlags.DirectionRightToLeft) != 0;
        var alignment = format.Alignment;
        var baseline = layout.Baseline;
        var lineY = offsetY;

        var options = new GlyphRunOptions
        {
            Brush = brushCore,
            FontSize = font.Size,
            Hint = ShouldHintText(),
            Style = GlyphRunStyle.Fill,
            BrushAlpha = 1f,
        };

        for (var i = 0; i < layout.Lines.Count; i++)
        {
            var line = layout.Lines[i];
            if (line.Glyphs.Length == 0)
            {
                lineY += layout.LineHeight + format.ParagraphSpacing;
                continue;
            }

            var advance = line.Advance;
            var horizontalOffset = alignment switch
            {
                StringAlignment.Center => (availableWidth - advance) * 0.5f,
                StringAlignment.Far => availableWidth - advance,
                _ => 0f,
            };

            if (rtl)
            {
                horizontalOffset = alignment switch
                {
                    StringAlignment.Near => availableWidth - advance,
                    StringAlignment.Far => 0f,
                    StringAlignment.Center => (availableWidth - advance) * 0.5f,
                    _ => horizontalOffset,
                };
            }

            if (!useBoundingBox && rtl)
            {
                horizontalOffset = -advance;
            }

            var lineX = offsetX + horizontalOffset;
            var lineTransform = Matrix3x2.CreateTranslation(lineX, lineY + baseline);
            lineTransform = Matrix3x2.Multiply(lineTransform, worldTransform);

            options.Transform = lineTransform;
            options.GlyphTransform = null;

            _scene.DrawGlyphRun(font.CoreFont, line.Glyphs, options);

            lineY += layout.LineHeight + format.ParagraphSpacing;
        }
    }

    private void ApplyClip(PathBuilder builder, CombineMode mode, VelloRegion? regionClone)
    {
        if (mode is not CombineMode.Replace and not CombineMode.Intersect)
        {
            throw new NotSupportedException($"Combine mode '{mode}' is not supported.");
        }

        if (mode == CombineMode.Replace)
        {
            PopLayersToDepth(0);
            _currentState.Clip = regionClone;
        }
        else
        {
            _currentState.Clip = null;
        }

        _scene.PushLayer(builder, s_clipLayerBlend, _currentState.Transform, alpha: 1f);
        _activeLayerDepth++;
    }

    private void StrokePath(PathBuilder path, VelloPen pen)
    {
        pen.ThrowIfDisposed();
        var stroke = pen.CreateStrokeStyle();

        if (pen.TryGetStrokeBrush(out var brush, out var brushTransform))
        {
            var transform = CombineBrushTransform(brushTransform, _currentState.Transform);
            _scene.StrokePath(path, stroke, _currentState.Transform, brush, transform);
        }
        else
        {
            _scene.StrokePath(path, stroke, _currentState.Transform, pen.GetStrokeColor());
        }
    }

    private void FillWithBrush(PathBuilder path, FillRule fillRule, VelloBrush brush)
    {
        var coreBrush = brush.CreateCoreBrush(out var brushTransform);
        var transform = CombineBrushTransform(brushTransform, _currentState.Transform);
        _scene.FillPath(path, fillRule, _currentState.Transform, coreBrush, transform);
    }

    private void PopLayersToDepth(int targetDepth)
    {
        while (_activeLayerDepth > targetDepth)
        {
            _scene.PopLayer();
            _activeLayerDepth--;
        }
    }

    private void ApplyState(VelloGraphicsState state)
    {
        _currentState.Transform = state.Transform;
        _currentState.SmoothingMode = state.SmoothingMode;
        _currentState.PixelOffsetMode = state.PixelOffsetMode;
        _currentState.InterpolationMode = state.InterpolationMode;
        _currentState.CompositingMode = state.CompositingMode;
        _currentState.CompositingQuality = state.CompositingQuality;
        _currentState.TextRenderingHint = state.TextRenderingHint;
        _currentState.Clip = state.Clip?.Clone();
        _activeLayerDepth = state.LayerDepth;
    }

    private TextLayoutResult BuildTextLayout(string text, VelloFont font, VelloStringFormat format)
    {
        var processed = ApplyHotkeyPrefix(text ?? string.Empty, format.HotkeyPrefix);
        var normalized = processed.Replace("\r\n", "\n").Replace('\r', '\n');
        var segments = normalized.Split('\n');

        var lines = new List<TextLineLayout>(segments.Length);
        var rtl = (format.FormatFlags & StringFormatFlags.DirectionRightToLeft) != 0;
        var spacing = format.LineSpacing <= 0 ? 1f : format.LineSpacing;
        var (baseline, lineHeight) = ComputeLineMetrics(font, spacing);
        var maxAdvance = 0f;

        foreach (var segment in segments)
        {
            var glyphPlan = ShapeTextLine(segment.AsSpan(), font, rtl);
            lines.Add(glyphPlan);
            if (glyphPlan.Advance > maxAdvance)
            {
                maxAdvance = glyphPlan.Advance;
            }
        }

        if (lines.Count == 0)
        {
            lineHeight = font.Size * spacing;
        }

        var paragraphSpacing = format.ParagraphSpacing;
        var totalHeight = lines.Count == 0
            ? 0f
            : (lines.Count * lineHeight) + Math.Max(0, lines.Count - 1) * paragraphSpacing;

        return new TextLayoutResult(lines, baseline, lineHeight, totalHeight, maxAdvance);
    }

    private TextLineLayout ShapeTextLine(ReadOnlySpan<char> text, VelloFont font, bool rtl)
    {
        if (text.IsEmpty)
        {
            return new TextLineLayout(Array.Empty<Glyph>(), 0f);
        }

        var options = new VelloTextShaperOptions(font.Size, rtl, 0f, null, null, true, font.Culture);
        var shaped = VelloTextShaperCore.ShapeUtf16(font.CoreFont.Handle, text, options, null, font.FaceIndex);
        if (shaped.Count == 0)
        {
            return new TextLineLayout(Array.Empty<Glyph>(), 0f);
        }

        var glyphs = new Glyph[shaped.Count];
        var advanceX = 0f;
        var advanceY = 0f;
        for (var i = 0; i < shaped.Count; i++)
        {
            var glyph = shaped[i];
            var x = advanceX + glyph.XOffset;
            var y = advanceY + glyph.YOffset;
            glyphs[i] = new Glyph(glyph.GlyphId, x, y);
            advanceX += glyph.XAdvance;
            advanceY += glyph.YAdvance;
        }

        return new TextLineLayout(glyphs, advanceX);
    }

    private static (float Baseline, float LineHeight) ComputeLineMetrics(VelloFont font, float spacing)
    {
        var baseline = font.Size;
        var lineHeight = font.Size;
        if (font.CoreFont.TryGetGlyphIndex('M', out var glyphId) && font.CoreFont.TryGetGlyphMetrics(glyphId, font.Size, out var metrics))
        {
            if (metrics.YBearing > 0)
            {
                baseline = metrics.YBearing;
            }

            if (metrics.Height > 0)
            {
                lineHeight = metrics.Height;
            }
        }

        baseline = MathF.Max(baseline, font.Size * 0.8f);
        lineHeight = MathF.Max(lineHeight, font.Size);
        lineHeight *= spacing;
        return (baseline, lineHeight);
    }

    private bool ShouldHintText() => _currentState.TextRenderingHint switch
    {
        TextRenderingHint.AntiAlias => false,
        TextRenderingHint.AntiAliasGridFit => false,
        _ => true,
    };

    private static RectangleF NormalizeRectangle(RectangleF rect)
    {
        if (rect.Width < 0)
        {
            rect.X += rect.Width;
            rect.Width = -rect.Width;
        }

        if (rect.Height < 0)
        {
            rect.Y += rect.Height;
            rect.Height = -rect.Height;
        }

        return rect;
    }

    private static FillRule ToFillRule(FillMode fillMode) => fillMode switch
    {
        FillMode.Alternate => FillRule.EvenOdd,
        FillMode.Winding => FillRule.NonZero,
        _ => FillRule.NonZero,
    };

    private static PathBuilder BuildRectanglePath(RectangleF rect)
    {
        var path = new PathBuilder();
        path.MoveTo(rect.Left, rect.Top);
        path.LineTo(rect.Right, rect.Top);
        path.LineTo(rect.Right, rect.Bottom);
        path.LineTo(rect.Left, rect.Bottom);
        path.Close();
        return path;
    }

    private static PathBuilder BuildEllipsePath(RectangleF rect)
    {
        const double kappa = 0.55228474983079363;
        var path = new PathBuilder();
        var cx = rect.Left + rect.Width / 2.0;
        var cy = rect.Top + rect.Height / 2.0;
        var rx = rect.Width / 2.0;
        var ry = rect.Height / 2.0;
        var ox = rx * kappa;
        var oy = ry * kappa;
        var top = cy - ry;
        var bottom = cy + ry;
        var left = rect.Left;
        var right = rect.Right;

        path.MoveTo(cx, top);
        path.CubicTo(cx + ox, top, right, cy - oy, right, cy);
        path.CubicTo(right, cy + oy, cx + ox, bottom, cx, bottom);
        path.CubicTo(cx - ox, bottom, left, cy + oy, left, cy);
        path.CubicTo(left, cy - oy, cx - ox, top, cx, top);
        path.Close();
        return path;
    }

    private static Matrix3x2? CombineBrushTransform(Matrix3x2? brushTransform, Matrix3x2 worldTransform)
    {
        var brushMatrix = brushTransform ?? Matrix3x2.Identity;
        if (worldTransform == Matrix3x2.Identity && brushTransform is null)
        {
            return null;
        }

        var combined = brushMatrix;
        if (worldTransform != Matrix3x2.Identity)
        {
            combined = Matrix3x2.Multiply(brushMatrix, worldTransform);
        }

        return combined == Matrix3x2.Identity ? (Matrix3x2?)null : combined;
    }

    private static string ApplyHotkeyPrefix(string text, HotkeyPrefix prefix)
    {
        if (prefix == HotkeyPrefix.None || string.IsNullOrEmpty(text))
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '&')
            {
                if (i + 1 < text.Length && text[i + 1] == '&')
                {
                    builder.Append('&');
                    i++;
                    continue;
                }

                if (prefix is HotkeyPrefix.Hide or HotkeyPrefix.Show)
                {
                    continue;
                }
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private void ValidatePen(VelloPen pen)
    {
        ArgumentNullException.ThrowIfNull(pen);
        pen.ThrowIfDisposed();
        if (pen.Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pen.Width), "Pen width must be positive.");
        }

        if (pen.Alignment != PenAlignment.Center)
        {
            throw new NotSupportedException($"Pen alignment '{pen.Alignment}' is not supported.");
        }
    }

    private readonly record struct TextLineLayout(Glyph[] Glyphs, float Advance);

    private sealed class TextLayoutResult
    {
        public TextLayoutResult(List<TextLineLayout> lines, float baseline, float lineHeight, float totalHeight, float maxAdvance)
        {
            Lines = lines;
            Baseline = baseline;
            LineHeight = lineHeight;
            TotalHeight = totalHeight;
            MaxAdvance = maxAdvance;
        }

        public List<TextLineLayout> Lines { get; }

        public float Baseline { get; }

        public float LineHeight { get; }

        public float TotalHeight { get; }

        public float MaxAdvance { get; }
    }

    private struct RenderState
    {
        public Matrix3x2 Transform;
        public SmoothingMode SmoothingMode;
        public PixelOffsetMode PixelOffsetMode;
        public InterpolationMode InterpolationMode;
        public CompositingMode CompositingMode;
        public CompositingQuality CompositingQuality;
        public TextRenderingHint TextRenderingHint;
        public VelloRegion? Clip;

        public static RenderState CreateDefault() => new()
        {
            Transform = Matrix3x2.Identity,
            SmoothingMode = SmoothingMode.AntiAlias,
            PixelOffsetMode = PixelOffsetMode.Default,
            InterpolationMode = InterpolationMode.HighQualityBilinear,
            CompositingMode = CompositingMode.SourceOver,
            CompositingQuality = CompositingQuality.Default,
            TextRenderingHint = TextRenderingHint.SystemDefault,
            Clip = null,
        };
    }
}







