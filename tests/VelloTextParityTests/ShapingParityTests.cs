extern alias VelloHB;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SkiaSharp;
using HBFont = VelloHB::HarfBuzzSharp.Font;
using HBFace = VelloHB::HarfBuzzSharp.Face;
using HBBlob = VelloHB::HarfBuzzSharp.Blob;
using HBBuffer = VelloHB::HarfBuzzSharp.Buffer;
using HBDirection = VelloHB::HarfBuzzSharp.Direction;
using HBFeature = VelloHB::HarfBuzzSharp.Feature;
using HBLanguage = VelloHB::HarfBuzzSharp.Language;
using Xunit;

namespace VelloTextParityTests;

public sealed class ShapingParityTests : IDisposable
{
    private const string EmbeddedFontResource = "VelloTextParityTests.Assets.Roboto-Regular.ttf";
    private static readonly byte[] s_fontData = LoadEmbeddedFont();
    private static readonly int s_unitsPerEm = GetUnitsPerEm(s_fontData);

    private readonly SKTypeface _typeface;

    public ShapingParityTests()
    {
        _typeface = SKTypeface.FromData(SKData.CreateCopy(s_fontData));
    }

    [Theory]
    [InlineData("Hello Vello", false)]
    [InlineData("Привет, мир", false)]
    [InlineData("مرحبا بالعالم", true)]
    [InlineData("こんにちは世界", false)]
    [InlineData("👩🏽‍💻🚀✨", false)]
    public void ShapingProducesGlyphs(string text, bool rtl)
    {
        var glyphs = ShapeText(text, rtl ? new CultureInfo("ar") : CultureInfo.InvariantCulture, rtl);
        Assert.NotEmpty(glyphs);
        Assert.All(glyphs, g => Assert.True(g.Advance >= 0));
    }

    [Fact]
    public void FallbackShapingProducesClusters()
    {
        using var face = new HBFace((_, _) => null) { UnitsPerEm = (ushort)s_unitsPerEm };
        using var font = new HBFont(face);

        using var buffer = new HBBuffer();
        buffer.AddUtf16("abc".AsSpan(), 0, 3);
        font.Shape(buffer, Array.Empty<HBFeature>());

        Assert.Equal(3, buffer.Length);
        var clusters = buffer.GetGlyphInfoSpan().ToArray().Select(x => x.Cluster).ToArray();
        Assert.Equal(new uint[] { 0, 1, 2 }, clusters);
    }

    private static IReadOnlyList<GlyphRecord> ShapeText(string text, CultureInfo culture, bool rtl)
    {
        using var stream = new MemoryStream(s_fontData, writable: false);
        using var blob = HBBlob.FromStream(stream);
        using var face = new HBFace(blob) { UnitsPerEm = (ushort)s_unitsPerEm };
        using var font = new HBFont(face);

        using var buffer = new HBBuffer();
        buffer.Reset();
        buffer.AddUtf16(text.AsSpan(), 0, text.Length);
        buffer.Direction = rtl ? HBDirection.RightToLeft : HBDirection.LeftToRight;
        buffer.Language = new HBLanguage(culture);
        buffer.GuessSegmentProperties();

        font.Shape(buffer, Array.Empty<HBFeature>());

        if (rtl)
        {
            buffer.Reverse();
        }

        var infos = buffer.GetGlyphInfoSpan();
        var positions = buffer.GetGlyphPositionSpan();

        var result = new GlyphRecord[buffer.Length];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = new GlyphRecord((ushort)infos[i].Codepoint, (int)infos[i].Cluster, positions[i].XAdvance);
        }

        return result;
    }

    private static byte[] LoadEmbeddedFont()
    {
        using var stream = typeof(ShapingParityTests).Assembly.GetManifestResourceStream(EmbeddedFontResource)
            ?? throw new InvalidOperationException($"Embedded font '{EmbeddedFontResource}' not found.");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static int GetUnitsPerEm(byte[] fontData)
    {
        using var typeface = SKTypeface.FromData(SKData.CreateCopy(fontData));
        return typeface.UnitsPerEm;
    }

    public void Dispose()
    {
        _typeface.Dispose();
    }

    private readonly record struct GlyphRecord(ushort GlyphId, int Cluster, float Advance);
}
