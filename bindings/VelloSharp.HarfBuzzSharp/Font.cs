using System;
using System.Runtime.InteropServices;
using VelloSharp;

namespace HarfBuzzSharp;

public sealed class Font : IDisposable
{
    private readonly Face _face;
    private readonly IntPtr _fontHandle;
    private readonly FontOpenTypeMetrics _openTypeMetrics;
    private readonly VelloSharp.Font? _ownedFont;
    private int _scaleX;
    private int _scaleY;

    public Font(Face face, IntPtr fontHandle, int unitsPerEm)
    {
        _face = face ?? throw new ArgumentNullException(nameof(face));
        _fontHandle = fontHandle;
        _scaleX = unitsPerEm == 0 ? 2048 : unitsPerEm;
        _scaleY = _scaleX;
        _openTypeMetrics = new FontOpenTypeMetrics(_fontHandle, _scaleX);
    }

    public Font(Face face)
    {
        _face = face ?? throw new ArgumentNullException(nameof(face));
        _ownedFont = TryCreateVelloFont(face);
        _fontHandle = _ownedFont?.Handle ?? IntPtr.Zero;
        var unitsPerEm = face.UnitsPerEm == 0 ? 2048 : face.UnitsPerEm;
        _scaleX = unitsPerEm;
        _scaleY = unitsPerEm;
        _openTypeMetrics = new FontOpenTypeMetrics(_fontHandle, _scaleX);
    }

    public FontOpenTypeMetrics OpenTypeMetrics => _openTypeMetrics;

    public void SetScale(int xScale, int yScale)
    {
        _scaleX = xScale;
        _scaleY = yScale;
    }

    public void GetScale(out int xScale, out int yScale)
    {
        xScale = _scaleX;
        yScale = _scaleY;
    }

    public void SetFunctionsOpenType()
    {
        // No-op; metrics queries are handled through Vello APIs.
    }

    public bool TryGetGlyphExtents(ushort glyph, out GlyphExtents extents)
    {
        extents = default;
        if (_fontHandle == IntPtr.Zero)
        {
            return false;
        }

        if (NativeMethods.vello_font_get_glyph_metrics(_fontHandle, glyph, _scaleX, out var metrics) != VelloStatus.Success)
        {
            return false;
        }

        extents = new GlyphExtents
        {
            XBearing = metrics.XBearing,
            YBearing = metrics.YBearing,
            Width = metrics.Width,
            Height = metrics.Height,
        };
        return true;
    }

    public bool TryGetGlyph(uint codepoint, out uint glyph)
    {
        glyph = 0;
        if (_fontHandle == IntPtr.Zero)
        {
            return false;
        }

        if (NativeMethods.vello_font_get_glyph_index(_fontHandle, codepoint, out var mapped) != VelloStatus.Success)
        {
            return false;
        }

        glyph = mapped;
        return glyph != 0;
    }

    public int GetHorizontalGlyphAdvance(ushort glyph)
    {
        if (_fontHandle == IntPtr.Zero)
        {
            return 0;
        }

        if (NativeMethods.vello_font_get_glyph_metrics(_fontHandle, glyph, _scaleX, out var metrics) != VelloStatus.Success)
        {
            return 0;
        }

        return (int)MathF.Round(metrics.Advance);
    }

    public int[] GetHorizontalGlyphAdvances(ReadOnlySpan<uint> glyphs)
    {
        var advances = new int[glyphs.Length];
        for (var i = 0; i < glyphs.Length; i++)
        {
            advances[i] = GetHorizontalGlyphAdvance((ushort)glyphs[i]);
        }

        return advances;
    }

    public FontExtents GetFontExtentsForDirection(Direction direction)
    {
        if (_fontHandle == IntPtr.Zero)
        {
            return default;
        }

        if (NativeMethods.vello_font_get_metrics(_fontHandle, _scaleX, out var metrics) != VelloStatus.Success)
        {
            return default;
        }

        return new FontExtents
        {
            Ascender = -metrics.Ascent,
            Descender = -metrics.Descent,
            LineGap = metrics.Leading,
        };
    }

    public void Shape(Buffer buffer, Feature[]? features = null)
    {
        if (buffer is null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        if (_fontHandle == IntPtr.Zero || buffer.TextLength == 0)
        {
            buffer.PopulateFallback(_fontHandle, _scaleX);
            return;
        }

        var glyphs = VelloSharp.Text.VelloTextShaperCore.ShapeUtf16(
            _fontHandle,
            buffer.TextSpan,
            _scaleX,
            buffer.Direction == Direction.RightToLeft,
            0f);

        if (glyphs.Count == 0)
        {
            buffer.PopulateFallback(_fontHandle, _scaleX);
            return;
        }

        buffer.SetLength(glyphs.Count);

        var infos = buffer.GetGlyphInfoSpan();
        var positions = buffer.GetGlyphPositionSpan();

        for (var i = 0; i < glyphs.Count; i++)
        {
            var glyph = glyphs[i];
            var cluster = buffer.ClusterOffset + (int)glyph.Cluster;
            buffer.SetGlyph(i, glyph.GlyphId, (uint)cluster);
            buffer.SetPosition(i, glyph.XAdvance, glyph.YAdvance, glyph.XOffset, glyph.YOffset);
        }
    }

    public void Dispose()
    {
        _ownedFont?.Dispose();
    }

    public sealed class FontOpenTypeMetrics
    {
        private readonly IntPtr _fontHandle;
        private readonly float _scale;

        internal FontOpenTypeMetrics(IntPtr fontHandle, float scale)
        {
            _fontHandle = fontHandle;
            _scale = scale;
        }

        public bool TryGetPosition(OpenTypeMetricsTag tag, out int position)
        {
            position = 0;
            if (_fontHandle == IntPtr.Zero)
            {
                return false;
            }

            if (NativeMethods.vello_font_get_metrics(_fontHandle, _scale, out var metrics) != VelloStatus.Success)
            {
                return false;
            }

            switch (tag)
            {
                case OpenTypeMetricsTag.UnderlineOffset:
                    position = (int)MathF.Round(metrics.UnderlinePosition);
                    return true;
                case OpenTypeMetricsTag.UnderlineSize:
                    position = (int)MathF.Round(metrics.UnderlineThickness);
                    return true;
                case OpenTypeMetricsTag.StrikeoutOffset:
                    position = (int)MathF.Round(metrics.StrikeoutPosition);
                    return true;
                case OpenTypeMetricsTag.StrikeoutSize:
                    position = (int)MathF.Round(metrics.StrikeoutThickness);
                    return true;
                default:
                    return false;
            }
        }
    }

    private static VelloSharp.Font? TryCreateVelloFont(Face face)
    {
        if (face.Blob is Blob blob)
        {
            var data = blob.AsSpan();
            if (!data.IsEmpty)
            {
                return VelloSharp.Font.Load(data.ToArray());
            }
        }

        return null;
    }
}
