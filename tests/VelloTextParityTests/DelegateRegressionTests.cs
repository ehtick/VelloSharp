extern alias VelloHB;

using System;
using Xunit;
using HBBlob = VelloHB::HarfBuzzSharp.Blob;
using HBFont = VelloHB::HarfBuzzSharp.Font;
using HBFace = VelloHB::HarfBuzzSharp.Face;
using HBFontFunctions = VelloHB::HarfBuzzSharp.FontFunctions;

namespace VelloTextParityTests;

public sealed class DelegateRegressionTests : IDisposable
{
    private readonly HBBlob _blob;
    private readonly HBFace _face;
    private readonly HBFont _font;

    public DelegateRegressionTests()
    {
        using var stream = TestFontLoader.OpenPrimaryFontStream();
        _blob = HBBlob.FromStream(stream);
        _face = new HBFace(_blob) { UnitsPerEm = (ushort)TestFontLoader.PrimaryUnitsPerEm };
        _font = new HBFont(_face);
    }

    [Fact]
    public void FontUsesHorizontalKerningDelegate()
    {
        var functions = new HBFontFunctions();
        var callCount = 0;

        functions.SetHorizontalGlyphKerningDelegate((font, data, left, right) =>
        {
            callCount++;
            return (int)(left + right);
        });

        _font.SetFontFunctions(functions);

        var kerning = _font.GetKerning(3, 5);
        Assert.Equal(8, kerning);
        Assert.Equal(1, callCount);

        functions.Dispose();
    }

    [Fact]
    public void FontUsesNominalGlyphsDelegate()
    {
        var functions = new HBFontFunctions();
        functions.SetNominalGlyphsDelegate((font, data, count, codepoints, glyphs) =>
        {
            for (var i = 0; i < count; i++)
            {
                glyphs[i] = codepoints[i] + 42;
            }

            return count;
        });

        _font.SetFontFunctions(functions);

        Assert.True(_font.TryGetGlyph((uint)'A', out var glyph));
        Assert.Equal((uint)'A' + 42, glyph);

        functions.Dispose();
    }

    [Fact]
    public void FontUsesHorizontalAdvancesDelegate()
    {
        var functions = new HBFontFunctions();
        functions.SetHorizontalGlyphAdvancesDelegate((font, data, count, glyphs, advances) =>
        {
            for (var i = 0; i < count; i++)
            {
                advances[i] = (int)(glyphs[i] * 3);
            }
        });

        _font.SetFontFunctions(functions);

        var advances = _font.GetHorizontalGlyphAdvances(new uint[] { 1, 2, 3 });
        Assert.Equal(new[] { 3, 6, 9 }, advances);

        functions.Dispose();
    }

    [Fact]
    public void FontUsesHorizontalOriginDelegate()
    {
        var functions = new HBFontFunctions();
        functions.SetHorizontalGlyphOriginDelegate((HBFont font, object data, uint glyph, out int x, out int y) =>
        {
            x = (int)glyph + 10;
            y = (int)glyph - 5;
            return true;
        });

        _font.SetFontFunctions(functions);

        Assert.True(_font.TryGetGlyphHorizontalOrigin(12, out var xOrigin, out var yOrigin));
        Assert.Equal(22, xOrigin);
        Assert.Equal(7, yOrigin);

        functions.Dispose();
    }

    [Fact]
    public void GlyphExtentsFallbacksWhenDelegateReturnsFalse()
    {
        var functions = new HBFontFunctions();
        functions.SetGlyphExtentsDelegate((VelloHB::HarfBuzzSharp.Font font, object fontData, uint glyph, out VelloHB::HarfBuzzSharp.GlyphExtents extents) =>
        {
            extents = default;
            return false;
        });

        _font.SetFontFunctions(functions);

        Assert.True(_font.TryGetGlyph((uint)'A', out var glyphId));
        Assert.True(_font.TryGetGlyphExtents((ushort)glyphId, out var extents));
        Assert.NotEqual(0f, extents.Width);

        functions.Dispose();
    }

    public void Dispose()
    {
        _font.Dispose();
        _face.Dispose();
        _blob.Dispose();
    }
}
