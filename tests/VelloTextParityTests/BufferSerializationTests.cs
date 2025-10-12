extern alias VelloHB;
extern alias RealHB;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using HBBlob = VelloHB::HarfBuzzSharp.Blob;
using HBBuffer = VelloHB::HarfBuzzSharp.Buffer;
using HBFace = VelloHB::HarfBuzzSharp.Face;
using HBFeature = VelloHB::HarfBuzzSharp.Feature;
using HBGlyphFlags = VelloHB::HarfBuzzSharp.GlyphFlags;
using HBFont = VelloHB::HarfBuzzSharp.Font;
using HBSerializeFlag = VelloHB::HarfBuzzSharp.SerializeFlag;
using HBSerializeFormat = VelloHB::HarfBuzzSharp.SerializeFormat;
using ReferenceBlob = RealHB::HarfBuzzSharp.Blob;
using ReferenceBuffer = RealHB::HarfBuzzSharp.Buffer;
using ReferenceFace = RealHB::HarfBuzzSharp.Face;
using ReferenceFeature = RealHB::HarfBuzzSharp.Feature;
using ReferenceFont = RealHB::HarfBuzzSharp.Font;
using ReferenceSerializeFlag = RealHB::HarfBuzzSharp.SerializeFlag;
using ReferenceSerializeFormat = RealHB::HarfBuzzSharp.SerializeFormat;
using HBDirection = VelloHB::HarfBuzzSharp.Direction;
using ReferenceDirection = RealHB::HarfBuzzSharp.Direction;

namespace VelloTextParityTests;

public sealed class BufferSerializationTests : IDisposable
{
    private readonly HBBlob _shimBlob;
    private readonly HBFace _shimFace;
    private readonly HBFont _shimFont;

    public BufferSerializationTests()
    {
        using var stream = TestFontLoader.OpenPrimaryFontStream();
        _shimBlob = HBBlob.FromStream(stream);
        _shimFace = new HBFace(_shimBlob) { UnitsPerEm = (ushort)TestFontLoader.PrimaryUnitsPerEm };
        _shimFont = new HBFont(_shimFace);
    }

    [Theory]
    [InlineData(HBSerializeFormat.Text)]
    [InlineData(HBSerializeFormat.Json)]
    public void SerializeGlyphsMatchesReference(HBSerializeFormat format)
    {
        const string text = "Serialize me!";

        using var shimBuffer = ShapeWithShim(text);

        using var referenceStream = TestFontLoader.OpenPrimaryFontStream();
        using var referenceBlob = ReferenceBlob.FromStream(referenceStream);
        using var referenceFace = new ReferenceFace(referenceBlob, 0);
        using var referenceFont = new ReferenceFont(referenceFace);
        using var referenceBuffer = new ReferenceBuffer();
        referenceBuffer.AddUtf16(text.AsSpan(), 0, text.Length);
        referenceBuffer.Direction = RealHB::HarfBuzzSharp.Direction.LeftToRight;
        referenceBuffer.Language = new RealHB::HarfBuzzSharp.Language(CultureInfo.InvariantCulture);
        referenceBuffer.GuessSegmentProperties();
        referenceFont.Shape(referenceBuffer, Array.Empty<ReferenceFeature>());

        var (shimFlags, referenceFlags) = GetSerializeFlags(format);

        var shimSerialized = shimBuffer.SerializeGlyphs(_shimFont, format, shimFlags);
        var referenceSerialized = referenceBuffer.SerializeGlyphs(referenceFont, (ReferenceSerializeFormat)format, referenceFlags);

        Assert.Equal(
            StripJsonGlyphFlags(referenceSerialized, format),
            StripJsonGlyphFlags(shimSerialized, format));
    }

    public static IEnumerable<object[]> SerializationCorpusCases()
    {
        yield return new object[]
        {
            "Roboto-Regular.ttf",
            "Hello Vello",
            CultureInfo.InvariantCulture,
            HBDirection.LeftToRight,
        };
        yield return new object[]
        {
            "WenQuanYiMicroHei.ttf",
            "漢字測試",
            new CultureInfo("zh-Hant"),
            HBDirection.LeftToRight,
        };
        yield return new object[]
        {
            "Vazirmatn-Var.ttf",
            "مرحبا بالعالم",
            new CultureInfo("ar"),
            HBDirection.RightToLeft,
        };
    }

    [Theory]
    [MemberData(nameof(SerializationCorpusCases))]
    public void SerializeGlyphsMatchesReferenceAcrossFonts(string assetFileName, string text, CultureInfo culture, HBDirection direction)
    {
        var fontData = TestFontLoader.LoadFontBytes(assetFileName);
        var unitsPerEm = TestFontLoader.GetUnitsPerEm(assetFileName);

        using var shimStream = new MemoryStream(fontData, writable: false);
        using var shimBlob = HBBlob.FromStream(shimStream);
        using var shimFace = new HBFace(shimBlob) { UnitsPerEm = (ushort)unitsPerEm };
        using var shimFont = new HBFont(shimFace);
        using var shimBuffer = new HBBuffer();
        shimBuffer.AddUtf16(text.AsSpan(), 0, text.Length);
        shimBuffer.Direction = direction;
        shimBuffer.Language = new VelloHB::HarfBuzzSharp.Language(culture);
        shimBuffer.GuessSegmentProperties();
        shimFont.Shape(shimBuffer, Array.Empty<HBFeature>());
        if (direction == HBDirection.RightToLeft)
        {
            shimBuffer.Reverse();
        }

        using var referenceStream = new MemoryStream(fontData, writable: false);
        using var referenceBlob = ReferenceBlob.FromStream(referenceStream);
        using var referenceFace = new ReferenceFace(referenceBlob, 0);
        using var referenceFont = new ReferenceFont(referenceFace);
        using var referenceBuffer = new ReferenceBuffer();
        referenceBuffer.AddUtf16(text.AsSpan(), 0, text.Length);
        referenceBuffer.Direction = direction == HBDirection.RightToLeft ? ReferenceDirection.RightToLeft : ReferenceDirection.LeftToRight;
        referenceBuffer.Language = new RealHB::HarfBuzzSharp.Language(culture);
        referenceBuffer.GuessSegmentProperties();
        referenceFont.Shape(referenceBuffer, Array.Empty<ReferenceFeature>());
        if (direction == HBDirection.RightToLeft)
        {
            referenceBuffer.Reverse();
        }

        foreach (var format in new[] { HBSerializeFormat.Text, HBSerializeFormat.Json })
        {
            var (shimFlags, referenceFlags) = GetSerializeFlags(format, includeClusters: false);

            var shimSerialized = shimBuffer.SerializeGlyphs(shimFont, format, shimFlags);
            var referenceSerialized = referenceBuffer.SerializeGlyphs(referenceFont, (ReferenceSerializeFormat)format, referenceFlags);

            Assert.Equal(
                StripJsonGlyphFlags(referenceSerialized, format),
                StripJsonGlyphFlags(shimSerialized, format));
        }
    }

    [Fact]
    public void SerializeAndDeserializeJsonRoundTripPreservesGlyphFlags()
    {
        const string text = "Round-trip";

        using var originalBuffer = ShapeWithShim(text);
        var infos = originalBuffer.GetGlyphInfoSpan();
        if (!infos.IsEmpty)
        {
            ref var first = ref infos[0];
            first = new VelloHB::HarfBuzzSharp.GlyphInfo(first.Codepoint, first.Cluster, HBGlyphFlags.UnsafeToBreak | HBGlyphFlags.UnsafeToConcat);
        }

        var json = originalBuffer.SerializeGlyphs(_shimFont, HBSerializeFormat.Json, HBSerializeFlag.GlyphFlags);

        using var roundTripBuffer = new HBBuffer();
        roundTripBuffer.DeserializeGlyphs(json, _shimFont, HBSerializeFormat.Json);

        Assert.Equal(originalBuffer.Length, roundTripBuffer.Length);

        var originalInfos = originalBuffer.GetGlyphInfoSpan().ToArray();
        var roundTripInfos = roundTripBuffer.GetGlyphInfoSpan().ToArray();
        Assert.Equal(originalInfos.Length, roundTripInfos.Length);

        for (var i = 0; i < originalInfos.Length; i++)
        {
            Assert.Equal(originalInfos[i].Codepoint, roundTripInfos[i].Codepoint);
            Assert.Equal(originalInfos[i].Cluster, roundTripInfos[i].Cluster);
            Assert.Equal(originalInfos[i].Flags, roundTripInfos[i].Flags);
        }

        var originalPositions = originalBuffer.GetGlyphPositionSpan().ToArray();
        var roundTripPositions = roundTripBuffer.GetGlyphPositionSpan().ToArray();
        Assert.Equal(originalPositions.Length, roundTripPositions.Length);

        for (var i = 0; i < originalPositions.Length; i++)
        {
            Assert.Equal(originalPositions[i].XAdvance, roundTripPositions[i].XAdvance);
            Assert.Equal(originalPositions[i].YAdvance, roundTripPositions[i].YAdvance);
            Assert.Equal(originalPositions[i].XOffset, roundTripPositions[i].XOffset);
            Assert.Equal(originalPositions[i].YOffset, roundTripPositions[i].YOffset);
        }
    }

    [Fact]
    public void DeserializeTextFromReferenceBufferMatchesReferenceOutput()
    {
        const string text = "Deserialize me";

        using var referenceStream = TestFontLoader.OpenPrimaryFontStream();
        using var referenceBlob = ReferenceBlob.FromStream(referenceStream);
        using var referenceFace = new ReferenceFace(referenceBlob, 0);
        using var referenceFont = new ReferenceFont(referenceFace);
        using var referenceBuffer = new ReferenceBuffer();
        referenceBuffer.AddUtf16(text.AsSpan(), 0, text.Length);
        referenceBuffer.Direction = RealHB::HarfBuzzSharp.Direction.LeftToRight;
        referenceBuffer.Language = new RealHB::HarfBuzzSharp.Language(CultureInfo.InvariantCulture);
        referenceBuffer.GuessSegmentProperties();
        referenceFont.Shape(referenceBuffer, Array.Empty<ReferenceFeature>());
        var referenceSerialized = referenceBuffer.SerializeGlyphs(referenceFont, ReferenceSerializeFormat.Text, ReferenceSerializeFlag.NoGlyphNames);

        using var shimBuffer = new HBBuffer();
        shimBuffer.DeserializeGlyphs(referenceSerialized, _shimFont, HBSerializeFormat.Text);

        var shimSerialized = shimBuffer.SerializeGlyphs(_shimFont, HBSerializeFormat.Text, HBSerializeFlag.NoGlyphNames);
        Assert.Equal(referenceSerialized, shimSerialized);
    }

    private HBBuffer ShapeWithShim(string text)
    {
        var buffer = new HBBuffer();
        buffer.AddUtf16(text.AsSpan(), 0, text.Length);
        buffer.Direction = VelloHB::HarfBuzzSharp.Direction.LeftToRight;
        buffer.Language = new VelloHB::HarfBuzzSharp.Language(CultureInfo.InvariantCulture);
        buffer.GuessSegmentProperties();
        _shimFont.Shape(buffer, Array.Empty<HBFeature>());
        return buffer;
    }

    public void Dispose()
    {
        _shimFont.Dispose();
        _shimFace.Dispose();
        _shimBlob.Dispose();
    }

    private static (HBSerializeFlag Shim, ReferenceSerializeFlag Reference) GetSerializeFlags(
        HBSerializeFormat format,
        bool includeClusters = true)
    {
        var shimFlags = HBSerializeFlag.Default;
        var referenceFlags = ReferenceSerializeFlag.Default;

        if (!includeClusters)
        {
            shimFlags |= HBSerializeFlag.NoClusters;
            referenceFlags |= ReferenceSerializeFlag.NoClusters;
        }

        shimFlags |= HBSerializeFlag.NoGlyphNames;
        referenceFlags |= ReferenceSerializeFlag.NoGlyphNames;

        return (shimFlags, referenceFlags);
    }

    private static string StripGlyphFlags(string serialized)
    {
        var builder = new StringBuilder(serialized.Length);
        for (var i = 0; i < serialized.Length; i++)
        {
            var current = serialized[i];
            if (current == '#')
            {
                i++;
                while (i < serialized.Length && serialized[i] != '|' && serialized[i] != ']' && serialized[i] != '<')
                {
                    i++;
                }

                i--;
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static string StripJsonGlyphFlags(string serialized, HBSerializeFormat format)
    {
        if (format == HBSerializeFormat.Json)
        {
            return Regex.Replace(serialized, ",\"fl\":\\d+", string.Empty, RegexOptions.CultureInvariant);
        }

        return StripGlyphFlags(serialized);
    }
}
