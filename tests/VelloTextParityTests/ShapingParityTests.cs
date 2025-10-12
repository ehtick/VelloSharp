extern alias VelloHB;
extern alias RealHB;

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
using HBTag = VelloHB::HarfBuzzSharp.Tag;
using HBVariation = VelloHB::HarfBuzzSharp.FontVariation;
using HBVariationAxis = VelloHB::HarfBuzzSharp.VariationAxis;
using HBBufferFlags = VelloHB::HarfBuzzSharp.BufferFlags;
using HBUnicodeFunctions = VelloHB::HarfBuzzSharp.UnicodeFunctions;
using HBUnicodeCombiningClass = VelloHB::HarfBuzzSharp.UnicodeCombiningClass;
using HBUnicodeGeneralCategory = VelloHB::HarfBuzzSharp.UnicodeGeneralCategory;
using ReferenceFont = RealHB::HarfBuzzSharp.Font;
using ReferenceFace = RealHB::HarfBuzzSharp.Face;
using ReferenceBlob = RealHB::HarfBuzzSharp.Blob;
using HBOpenTypeMetricsTag = VelloHB::HarfBuzzSharp.OpenTypeMetricsTag;
using ReferenceOpenTypeMetricsTag = RealHB::HarfBuzzSharp.OpenTypeMetricsTag;
using Xunit;

namespace VelloTextParityTests;

public sealed class ShapingParityTests : IDisposable
{
    private const string EmbeddedFontResource = "VelloTextParityTests.Assets.Roboto-Regular.ttf";
    private const string VariationFontResource = "VelloTextParityTests.Assets.Vazirmatn-Var.ttf";
    private static readonly byte[] s_fontData = LoadEmbeddedFont(EmbeddedFontResource);
    private static readonly byte[] s_variationFontData = LoadEmbeddedFont(VariationFontResource);
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

    [Fact]
    public void FaceReportsVariationAxes()
    {
        using var stream = new MemoryStream(s_variationFontData, writable: false);
        using var blob = HBBlob.FromStream(stream);
        using var face = new HBFace(blob);

        var axes = face.VariationAxes;
        Assert.NotEmpty(axes);
        Assert.Same(axes, face.VariationAxes);

        var weightAxis = Assert.Single(axes, axis => axis.Tag.ToString() == "wght");
        Assert.InRange(weightAxis.MinValue, 100f, 100f);
        Assert.InRange(weightAxis.MaxValue, 900f, 900f);
        Assert.InRange(weightAxis.DefaultValue, weightAxis.MinValue, weightAxis.MaxValue);
    }

    [Fact]
    public void SettingVariationsAffectsAdvance()
    {
        using var stream = new MemoryStream(s_variationFontData, writable: false);
        using var blob = HBBlob.FromStream(stream);
        using var face = new HBFace(blob);
        using var font = new HBFont(face);
        using var buffer = new HBBuffer();

        buffer.AddUtf16("a".AsSpan(), 0, 1);
        font.Shape(buffer, Array.Empty<HBFeature>());
        var defaultAdvance = buffer.GetGlyphPositionSpan()[0].XAdvance;

        var weightAxis = face.VariationAxes.Length > 0
            ? face.VariationAxes[0]
            : new HBVariationAxis(HBTag.Parse("wght"), 100f, 400f, 900f);

        font.SetVariations(new HBVariation(weightAxis.Tag, weightAxis.MaxValue + 500f));
        var variations = font.Variations.ToArray();
        Assert.Single(variations);
        Assert.Equal(weightAxis.Tag, variations[0].Tag);
        Assert.Equal(weightAxis.MaxValue, variations[0].Value);

        using var childFont = new HBFont(font);
        var childVariations = childFont.Variations.ToArray();
        Assert.Single(childVariations);
        Assert.Equal(weightAxis.Tag, childVariations[0].Tag);
        Assert.Equal(weightAxis.MaxValue, childVariations[0].Value);

        buffer.Reset();
        buffer.AddUtf16("a".AsSpan(), 0, 1);
        font.Shape(buffer, Array.Empty<HBFeature>());
        Assert.True(buffer.Length > 0);
    }

    [Theory]
    [InlineData(HBOpenTypeMetricsTag.UnderlineOffset)]
    [InlineData(HBOpenTypeMetricsTag.UnderlineSize)]
    [InlineData(HBOpenTypeMetricsTag.StrikeoutOffset)]
    [InlineData(HBOpenTypeMetricsTag.StrikeoutSize)]
    [InlineData(HBOpenTypeMetricsTag.HorizontalAscender)]
    [InlineData(HBOpenTypeMetricsTag.HorizontalDescender)]
    [InlineData(HBOpenTypeMetricsTag.HorizontalLineGap)]
    [InlineData(HBOpenTypeMetricsTag.HorizontalCaretRise)]
    [InlineData(HBOpenTypeMetricsTag.HorizontalCaretRun)]
    [InlineData(HBOpenTypeMetricsTag.HorizontalCaretOffset)]
    [InlineData(HBOpenTypeMetricsTag.XHeight)]
    [InlineData(HBOpenTypeMetricsTag.CapHeight)]
    [InlineData(HBOpenTypeMetricsTag.SubScriptEmXSize)]
    [InlineData(HBOpenTypeMetricsTag.SubScriptEmYSize)]
    [InlineData(HBOpenTypeMetricsTag.SuperScriptEmXSize)]
    [InlineData(HBOpenTypeMetricsTag.SuperScriptEmYSize)]
    public void OpenTypeMetricsMatchReference(HBOpenTypeMetricsTag tag)
    {
        using var shimStream = new MemoryStream(s_fontData, writable: false);
        using var shimBlob = HBBlob.FromStream(shimStream);
        using var shimFace = new HBFace(shimBlob) { UnitsPerEm = (ushort)s_unitsPerEm };
        using var shimFont = new HBFont(shimFace);
        var shimMetrics = shimFont.OpenTypeMetrics;

        using var referenceStream = new MemoryStream(s_fontData, writable: false);
        using var referenceBlob = ReferenceBlob.FromStream(referenceStream);
        using var referenceFace = new ReferenceFace(referenceBlob, 0);
        using var referenceFont = new ReferenceFont(referenceFace);
        var referenceMetrics = referenceFont.OpenTypeMetrics;
        var referenceTag = (ReferenceOpenTypeMetricsTag)(uint)tag;

        var referenceHasValue = referenceMetrics.TryGetPosition(referenceTag, out var expected);
        var shimHasValue = shimMetrics.TryGetPosition(tag, out var actual);

        Assert.Equal(referenceHasValue, shimHasValue);
        if (referenceHasValue)
        {
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void OpenTypeMetricsVariationScalingMatchesExpectations()
    {
        using var stream = new MemoryStream(s_variationFontData, writable: false);
        using var blob = HBBlob.FromStream(stream);
        using var face = new HBFace(blob);
        using var font = new HBFont(face);
        font.SetScale(s_unitsPerEm, s_unitsPerEm);

        font.SetVariations(new HBVariation(HBTag.Parse("wght"), 900f));
        var metrics = font.OpenTypeMetrics;

        var delta = metrics.GetVariation(HBOpenTypeMetricsTag.HorizontalAscender);
        font.GetScale(out var scaleX, out var scaleY);
        var upem = face.UnitsPerEm == 0 ? s_unitsPerEm : face.UnitsPerEm;

        var expectedX = (int)MathF.Round(delta * scaleX / upem);
        var expectedY = (int)MathF.Round(delta * scaleY / upem);

        Assert.Equal(expectedX, metrics.GetXVariation(HBOpenTypeMetricsTag.HorizontalAscender));
        Assert.Equal(expectedY, metrics.GetYVariation(HBOpenTypeMetricsTag.HorizontalAscender));
    }

    [Fact]
    public void BufferRemovesDefaultIgnorables()
    {
        using var buffer = new HBBuffer();
        buffer.Flags = HBBufferFlags.RemoveDefaultIgnorables;
        buffer.AddUtf16("a\u200Cb".AsSpan(), 0, 3);

        buffer.ApplyUnicodeProcessing();

        Assert.Equal(2, buffer.Length);
        Assert.Equal("ab", buffer.TextSpan.ToString());
    }

    [Fact]
    public void BufferUsesMirroringDelegate()
    {
        using var buffer = new HBBuffer();
        buffer.AddUtf16("(".AsSpan(), 0, 1);
        buffer.Direction = HBDirection.RightToLeft;

        var unicode = new HBUnicodeFunctions();
        var invoked = false;
        unicode.SetMirroringDelegate((_, cp) =>
        {
            invoked = true;
            return cp == '(' ? (uint)'{' : cp;
        });
        buffer.UnicodeFunctions = unicode;

        buffer.ApplyUnicodeProcessing();

        Assert.True(invoked);
        Assert.Equal("{", buffer.TextSpan.ToString());
    }

    [Fact]
    public void BufferReordersCombiningMarks()
    {
        using var buffer = new HBBuffer();
        buffer.AddUtf16("a\u0328\u0301".AsSpan(), 0, 3);

        var unicode = new HBUnicodeFunctions();
        unicode.SetCombiningClassDelegate((_, cp) => cp switch
        {
            0x0328 => (HBUnicodeCombiningClass)230,
            0x0301 => (HBUnicodeCombiningClass)220,
            _ => HBUnicodeCombiningClass.NotReordered,
        });
        buffer.UnicodeFunctions = unicode;

        buffer.ApplyUnicodeProcessing();

        Assert.Equal("a\u0301\u0328", buffer.TextSpan.ToString());
    }

    [Fact]
    public void BufferRespectsCustomDefaultIgnorableDelegate()
    {
        using var buffer = new HBBuffer();
        buffer.Flags = HBBufferFlags.RemoveDefaultIgnorables;
        buffer.AddUtf16("ab".AsSpan(), 0, 2);

        var unicode = new HBUnicodeFunctions();
        unicode.SetGeneralCategoryDelegate((_, cp) =>
            cp == 'b' ? HBUnicodeGeneralCategory.Format : HBUnicodeGeneralCategory.LowercaseLetter);
        buffer.UnicodeFunctions = unicode;

        buffer.ApplyUnicodeProcessing();

        Assert.Equal(1, buffer.Length);
        Assert.Equal("a", buffer.TextSpan.ToString());
    }


    private static byte[] LoadEmbeddedFont(string resourceName)
    {
        using var stream = typeof(ShapingParityTests).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded font '{resourceName}' not found.");
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

