using System;
using System.IO;
using Avalonia.Media;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal sealed class VelloGlyphTypeface : IGlyphTypeface
{
    private readonly byte[] _fontData;
    private bool _disposed;

    public VelloGlyphTypeface(string familyName, FontStyle style, FontWeight weight, FontStretch stretch, byte[] fontData)
    {
        FamilyName = familyName ?? throw new ArgumentNullException(nameof(familyName));
        Style = style;
        Weight = weight;
        Stretch = stretch;
        if (fontData is null)
        {
            throw new ArgumentNullException(nameof(fontData));
        }

        _fontData = (byte[])fontData.Clone();
    }

    public string FamilyName { get; }

    public FontWeight Weight { get; }

    public FontStyle Style { get; }

    public FontStretch Stretch { get; }

    public int GlyphCount => 0xFFFF;

    public FontSimulations FontSimulations => FontSimulations.None;

    public FontMetrics Metrics => new FontMetrics
    {
        DesignEmHeight = 2048,
        Ascent = -1900,
        Descent = 500,
        LineGap = 100,
        UnderlinePosition = -150,
        UnderlineThickness = 50,
        StrikethroughPosition = 512,
        StrikethroughThickness = 50,
        IsFixedPitch = false,
    };

    public void Dispose()
    {
        _disposed = true;
    }

    public bool TryGetStream(out Stream stream)
    {
        if (_disposed)
        {
            stream = Stream.Null;
            return false;
        }

        stream = new MemoryStream(_fontData, writable: false);
        return true;
    }

    public ushort GetGlyph(uint codepoint)
    {
        return (ushort)(codepoint & 0xFFFF);
    }

    public bool TryGetGlyph(uint codepoint, out ushort glyph)
    {
        glyph = GetGlyph(codepoint);
        return true;
    }

    public ushort[] GetGlyphs(ReadOnlySpan<uint> codepoints)
    {
        var result = new ushort[codepoints.Length];
        for (var i = 0; i < codepoints.Length; i++)
        {
            result[i] = GetGlyph(codepoints[i]);
        }

        return result;
    }

    public int GetGlyphAdvance(ushort glyph) => 1024;

    public int[] GetGlyphAdvances(ReadOnlySpan<ushort> glyphs)
    {
        var advances = new int[glyphs.Length];
        for (var i = 0; i < glyphs.Length; i++)
        {
            advances[i] = 1024;
        }

        return advances;
    }

    public bool TryGetGlyphMetrics(ushort glyph, out GlyphMetrics metrics)
    {
        metrics = new GlyphMetrics
        {
            XBearing = 0,
            YBearing = 0,
            Width = 1024,
            Height = 1024,
        };

        return true;
    }

    public bool TryGetTable(uint tag, out byte[] table)
    {
        table = Array.Empty<byte>();
        return false;
    }
}
