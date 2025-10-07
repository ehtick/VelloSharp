using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using HarfBuzzSharp;
using VelloSharp.Charting.Rendering;
using VelloSharp.Charting.Styling;
using VScene = VelloSharp.Scene;
using VGlyph = VelloSharp.Glyph;
using VGlyphRunOptions = VelloSharp.GlyphRunOptions;
using VGlyphRunStyle = VelloSharp.GlyphRunStyle;
using VSolidColorBrush = VelloSharp.SolidColorBrush;
using VFont = VelloSharp.Font;
using ChartRgbaColor = VelloSharp.Charting.Styling.RgbaColor;

namespace VelloSharp.Charting.Rendering.Text;

internal sealed class TextRenderer : IDisposable
{
    private readonly FontResource _font;
    private bool _disposed;

    public TextRenderer()
    {
        _font = new FontResource(LoadEmbeddedFont());
    }

    public void Draw(
        VScene scene,
        AxisLabelVisual label,
        float fontSize)
    {
        ArgumentNullException.ThrowIfNull(scene);

        if (string.IsNullOrWhiteSpace(label.Text))
        {
            return;
        }

        EnsureNotDisposed();

        var shaping = Shape(label.Text, fontSize);
        if (shaping.Glyphs.Length == 0)
        {
            return;
        }

        var brush = new VSolidColorBrush(label.Color.ToVelloColor());
        var options = new VGlyphRunOptions
        {
            Brush = brush,
            FontSize = fontSize,
            Hint = false,
            Style = VGlyphRunStyle.Fill,
            GlyphTransform = null,
            BrushAlpha = 1f,
        };

        var translate = ComputeTranslation(label, shaping);
        options.Transform = Matrix3x2.CreateTranslation(translate);

        scene.DrawGlyphRun(_font.SceneFont, shaping.Glyphs, options);
    }

    private GlyphRunShape Shape(string text, float size)
    {
        lock (_font)
        {
            var scaled = Math.Max(1, (int)MathF.Round(size * 64f));
            _font.HarfBuzzFont.SetScale(scaled, scaled);

            var extents = _font.HarfBuzzFont.GetFontExtentsForDirection(Direction.LeftToRight);
            var ascender = extents.Ascender / 64f;
            var descender = -extents.Descender / 64f;
            var lineGap = extents.LineGap / 64f;
            var lineHeight = (ascender + descender + lineGap).ClampPositive(size * 1.1f);

            using var buffer = new HarfBuzzSharp.Buffer();
            buffer.Direction = Direction.LeftToRight;
            buffer.AddUtf16(text);
            buffer.GuessSegmentProperties();
            _font.HarfBuzzFont.Shape(buffer);

            var infos = buffer.GlyphInfos;
            var positions = buffer.GlyphPositions;
            var glyphs = new Glyph[infos.Length];
            var penX = 0f;
            var penY = 0f;

            for (var i = 0; i < infos.Length; i++)
            {
                var info = infos[i];
                var pos = positions[i];
                var x = penX + pos.XOffset / 64f;
                var y = penY - pos.YOffset / 64f;
                glyphs[i] = new VGlyph(info.Codepoint, x, y);
                penX += pos.XAdvance / 64f;
                penY += pos.YAdvance / 64f;
            }

            var advance = penX;

            return new GlyphRunShape(glyphs, advance, ascender, descender, lineHeight);
        }
    }

    private Vector2 ComputeTranslation(AxisLabelVisual label, GlyphRunShape shaping)
    {
        var x = (float)label.X;
        var y = (float)label.Y;

        switch (label.HorizontalAlignment)
        {
            case TextAlignment.Center:
                x -= shaping.Advance / 2f;
                break;
            case TextAlignment.End:
                x -= shaping.Advance;
                break;
        }

        switch (label.VerticalAlignment)
        {
            case TextAlignment.Start:
                y += shaping.Ascender;
                break;
            case TextAlignment.Center:
                y += shaping.Ascender - shaping.LineHeight / 2f;
                break;
            case TextAlignment.End:
                y += shaping.Ascender - shaping.LineHeight;
                break;
        }

        return new Vector2(x, y);
    }

    private static byte[] LoadEmbeddedFont()
    {
        using var stream = typeof(TextRenderer).Assembly.GetManifestResourceStream("VelloSharp.Charting.Assets.Fonts.Roboto-Regular.ttf")
            ?? throw new InvalidOperationException("Embedded font resource 'Roboto-Regular.ttf' was not found.");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TextRenderer));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _font.Dispose();
        _disposed = true;
    }

    private readonly record struct GlyphRunShape(Glyph[] Glyphs, float Advance, float Ascender, float Descender, float LineHeight);

    private sealed class FontResource : IDisposable
    {
        private readonly GCHandle _handle;
        private readonly Blob _blob;
        private readonly VFont _sceneFont;
        public readonly Face Face;
        public readonly HarfBuzzSharp.Font HarfBuzzFont;
        private bool _disposed;

        public FontResource(byte[] fontData)
        {
            _handle = GCHandle.Alloc(fontData, GCHandleType.Pinned);
            _blob = new Blob(_handle.AddrOfPinnedObject(), fontData.Length, MemoryMode.ReadOnly);
            Face = new Face(_blob, 0);
            HarfBuzzFont = new HarfBuzzSharp.Font(Face);
            HarfBuzzFont.SetFunctionsOpenType();
            _sceneFont = VFont.Load(fontData);
        }

        public VFont SceneFont
        {
            get
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(FontResource));
                }

                return _sceneFont;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _sceneFont.Dispose();
            HarfBuzzFont.Dispose();
            Face.Dispose();
            _blob.Dispose();
            if (_handle.IsAllocated)
            {
                _handle.Free();
            }
            _disposed = true;
        }
    }
}

file static class FloatExtensions
{
    public static float ClampPositive(this float value, float fallback)
    {
        return float.IsFinite(value) && value > 0f ? value : fallback;
    }
}

