using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using HarfBuzzSharp;
using VelloSharp;

namespace AvaloniaVelloExamples.Scenes;

public sealed class SimpleText : IDisposable
{
    private readonly FontResource _roboto;
    private readonly FontResource _inconsolata;
    private readonly FontResource _notoEmojiColr;
    private readonly FontResource _notoEmojiCbtf;
    private bool _disposed;

    private SimpleText(FontResource roboto, FontResource inconsolata, FontResource notoEmojiColr, FontResource notoEmojiCbtf)
    {
        _roboto = roboto;
        _inconsolata = inconsolata;
        _notoEmojiColr = notoEmojiColr;
        _notoEmojiCbtf = notoEmojiCbtf;
    }

    public static SimpleText Create(string? assetRoot)
    {
        var root = assetRoot ?? AppContext.BaseDirectory;

        static string ResolvePath(string rootPath, string relative)
        {
            if (Path.IsPathRooted(relative))
            {
                return relative;
            }
            return Path.Combine(rootPath, relative);
        }

        FontResource LoadFont(string relative)
        {
            var path = ResolvePath(root, relative);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Font asset '{relative}' was not found.", path);
            }
            return new FontResource(path);
        }

        return new SimpleText(
            LoadFont(Path.Combine("roboto", "Roboto-Regular.ttf")),
            LoadFont(Path.Combine("inconsolata", "Inconsolata.ttf")),
            LoadFont(Path.Combine("noto_color_emoji", "NotoColorEmoji-Subset.ttf")),
            LoadFont(Path.Combine("noto_color_emoji", "NotoColorEmoji-CBTF-Subset.ttf")));
    }

    public void Add(Scene scene, float size, RgbaColor color, Matrix3x2 transform, Matrix3x2? glyphTransform, string text)
    {
        ArgumentNullException.ThrowIfNull(scene);
        Draw(scene, _roboto, size, new SolidColorBrush(color), transform, glyphTransform, GlyphRunStyle.Fill, stroke: null, hint: false, brushAlpha: 1f, text);
    }

    public void AddRun(Scene scene, float size, RgbaColor color, Matrix3x2 transform, Matrix3x2? glyphTransform, StrokeStyle stroke, string text)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(stroke);
        Draw(scene, _roboto, size, new SolidColorBrush(color), transform, glyphTransform, GlyphRunStyle.Stroke, CloneStroke(stroke), hint: false, brushAlpha: 1f, text);
    }

    public void AddVarRun(
        Scene scene,
        float size,
        ReadOnlySpan<(string Axis, float Value)> variations,
        RgbaColor color,
        Matrix3x2 transform,
        Matrix3x2? glyphTransform,
        GlyphRunStyle style,
        string text,
        bool hint,
        StrokeStyle? stroke = null,
        float brushAlpha = 1f)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var font = variations.Length > 0 ? _inconsolata : _roboto;
        var effectiveStroke = style == GlyphRunStyle.Stroke && stroke is not null ? CloneStroke(stroke) : null;
        var effectiveGlyphTransform = glyphTransform;

        for (var i = 0; i < variations.Length; i++)
        {
            var (axis, value) = variations[i];
            if (axis.Equals("wdth", StringComparison.OrdinalIgnoreCase))
            {
                var scaleX = value / 100f;
                if (!float.IsFinite(scaleX) || scaleX <= 0f)
                {
                    scaleX = 1f;
                }
                var scale = Matrix3x2.CreateScale(scaleX, 1f);
                effectiveGlyphTransform = effectiveGlyphTransform.HasValue
                    ? scale * effectiveGlyphTransform.Value
                    : scale;
            }
            else if (axis.Equals("wght", StringComparison.OrdinalIgnoreCase) && effectiveStroke is not null)
            {
                var widthScale = value / 400f;
                if (!float.IsFinite(widthScale) || widthScale <= 0f)
                {
                    widthScale = 1f;
                }
                effectiveStroke.Width = Math.Max(0.1, effectiveStroke.Width * widthScale);
            }
        }

        Draw(scene, font, size, new SolidColorBrush(color), transform, effectiveGlyphTransform, style, effectiveStroke, hint, brushAlpha, text);
    }

    public void AddColrEmojiRun(Scene scene, float size, Matrix3x2 transform, Matrix3x2? glyphTransform, string text)
    {
        ArgumentNullException.ThrowIfNull(scene);
        Draw(scene, _notoEmojiColr, size, new SolidColorBrush(new RgbaColor(1f, 1f, 1f, 1f)), transform, glyphTransform, GlyphRunStyle.Fill, stroke: null, hint: false, brushAlpha: 1f, text);
    }

    public void AddBitmapEmojiRun(Scene scene, float size, Matrix3x2 transform, Matrix3x2? glyphTransform, string text)
    {
        ArgumentNullException.ThrowIfNull(scene);
        Draw(scene, _notoEmojiCbtf, size, new SolidColorBrush(new RgbaColor(1f, 1f, 1f, 1f)), transform, glyphTransform, GlyphRunStyle.Fill, stroke: null, hint: false, brushAlpha: 1f, text);
    }

    private void Draw(
        Scene scene,
        FontResource font,
        float size,
        Brush brush,
        Matrix3x2 transform,
        Matrix3x2? glyphTransform,
        GlyphRunStyle style,
        StrokeStyle? stroke,
        bool hint,
        float brushAlpha,
        string text)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(brush);

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        using var hbFont = new HarfBuzzSharp.Font(font.Face);
        hbFont.SetFunctionsOpenType();
        var scaled = Math.Max(1, (int)MathF.Round(size * 64f));
        hbFont.SetScale(scaled, scaled);

        var extents = hbFont.GetFontExtentsForDirection(Direction.LeftToRight);
        var lineHeight = (extents.Ascender - extents.Descender + extents.LineGap) / 64f;
        if (!float.IsFinite(lineHeight) || lineHeight <= 0f)
        {
            lineHeight = size * 1.2f;
        }

        var glyphs = new List<Glyph>(text.Length);
        float baselineY = 0f;
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            AppendLineGlyphs(hbFont, lines[i], glyphs, baselineY);
            if (i < lines.Length - 1)
            {
                baselineY += lineHeight;
            }
        }

        if (glyphs.Count == 0)
        {
            return;
        }

        var options = new GlyphRunOptions
        {
            Brush = brush,
            FontSize = size,
            Hint = hint,
            Style = style,
            Stroke = style == GlyphRunStyle.Stroke ? stroke : null,
            BrushAlpha = brushAlpha,
            Transform = transform,
            GlyphTransform = glyphTransform,
        };

        scene.DrawGlyphRun(font.VelloFont, CollectionsMarshal.AsSpan(glyphs), options);
    }

    private static void AppendLineGlyphs(HarfBuzzSharp.Font font, string text, List<Glyph> glyphs, float baselineY)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        using var buffer = new HarfBuzzSharp.Buffer();
        buffer.Direction = Direction.LeftToRight;
        buffer.AddUtf16(text);
        buffer.GuessSegmentProperties();
        font.Shape(buffer);

        var infos = buffer.GlyphInfos;
        var positions = buffer.GlyphPositions;
        var penX = 0f;
        var penY = baselineY;

        for (var i = 0; i < infos.Length; i++)
        {
            var info = infos[i];
            var pos = positions[i];
            var glyphX = penX + pos.XOffset / 64f;
            var glyphY = penY - pos.YOffset / 64f;
            glyphs.Add(new Glyph(info.Codepoint, glyphX, glyphY));
            penX += pos.XAdvance / 64f;
            penY += pos.YAdvance / 64f;
        }
    }

    private static StrokeStyle CloneStroke(StrokeStyle source)
    {
        var clone = new StrokeStyle
        {
            Width = source.Width,
            MiterLimit = source.MiterLimit,
            StartCap = source.StartCap,
            EndCap = source.EndCap,
            LineJoin = source.LineJoin,
            DashPhase = source.DashPhase,
        };
        if (source.DashPattern is { Length: > 0 } dash)
        {
            clone.DashPattern = (double[])dash.Clone();
        }
        return clone;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SimpleText));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _roboto.Dispose();
        _inconsolata.Dispose();
        _notoEmojiColr.Dispose();
        _notoEmojiCbtf.Dispose();
        _disposed = true;
    }

    private sealed class FontResource : IDisposable
    {
        private readonly GCHandle _dataHandle;
        private readonly Blob _blob;
        public VelloSharp.Font VelloFont { get; }
        public Face Face { get; }
        private bool _disposed;

        public FontResource(string path)
        {
            var data = File.ReadAllBytes(path);
            _dataHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
            _blob = new Blob(_dataHandle.AddrOfPinnedObject(), data.Length, MemoryMode.ReadOnly);
            Face = new Face(_blob, 0);
            VelloFont = VelloSharp.Font.Load(data);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            VelloFont.Dispose();
            Face.Dispose();
            _blob.Dispose();
            if (_dataHandle.IsAllocated)
            {
                _dataHandle.Free();
            }
            _disposed = true;
        }
    }
}
