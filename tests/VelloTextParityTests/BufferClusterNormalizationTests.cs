extern alias VelloHB;
extern alias RealHB;

using System;
using System.Globalization;
using Xunit;
using HBBlob = VelloHB::HarfBuzzSharp.Blob;
using HBBuffer = VelloHB::HarfBuzzSharp.Buffer;
using HBFace = VelloHB::HarfBuzzSharp.Face;
using HBFeature = VelloHB::HarfBuzzSharp.Feature;
using HBFont = VelloHB::HarfBuzzSharp.Font;
using HBDirection = VelloHB::HarfBuzzSharp.Direction;
using ReferenceBlob = RealHB::HarfBuzzSharp.Blob;
using ReferenceBuffer = RealHB::HarfBuzzSharp.Buffer;
using ReferenceFace = RealHB::HarfBuzzSharp.Face;
using ReferenceFeature = RealHB::HarfBuzzSharp.Feature;
using ReferenceFont = RealHB::HarfBuzzSharp.Font;
using ReferenceDirection = RealHB::HarfBuzzSharp.Direction;

namespace VelloTextParityTests;

public sealed class BufferClusterNormalizationTests : IDisposable
{
    private readonly byte[] _primaryFontData = TestFontLoader.PrimaryFontData;
    private readonly int _primaryUnitsPerEm = TestFontLoader.PrimaryUnitsPerEm;
    [Theory]
    [InlineData("a\u0301e")]
    public void NormalizeGlyphsMatchesReference(string text)
    {
        using var fonts = CreateFonts(_primaryFontData, _primaryUnitsPerEm);

        ShapeBoth(text, fonts, out var shimBuffer, out var referenceBuffer);

        shimBuffer.NormalizeGlyphs();
        referenceBuffer.NormalizeGlyphs();

        AssertBuffersEqual(shimBuffer, referenceBuffer);

        shimBuffer.Dispose();
        referenceBuffer.Dispose();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("a\u0301b")]
    public void ReverseClustersMatchesReference(string text)
    {
        using var fonts = CreateFonts(_primaryFontData, _primaryUnitsPerEm);

        ShapeBoth(text, fonts, out var shimBuffer, out var referenceBuffer);

        shimBuffer.ReverseClusters();
        referenceBuffer.ReverseClusters();

        AssertBuffersEqual(shimBuffer, referenceBuffer);

        shimBuffer.Dispose();
        referenceBuffer.Dispose();
    }

    private static void ShapeBoth(
        string text,
        FontScope fonts,
        out HBBuffer shimBuffer,
        out ReferenceBuffer referenceBuffer)
    {
        shimBuffer = new HBBuffer();
        shimBuffer.AddUtf16(text.AsSpan(), 0, text.Length);
        shimBuffer.Direction = HBDirection.LeftToRight;
        shimBuffer.Language = new VelloHB::HarfBuzzSharp.Language(CultureInfo.InvariantCulture);
        shimBuffer.GuessSegmentProperties();
        fonts.ShimFont.Shape(shimBuffer, Array.Empty<HBFeature>());

        referenceBuffer = new ReferenceBuffer();
        referenceBuffer.AddUtf16(text.AsSpan(), 0, text.Length);
        referenceBuffer.Direction = ReferenceDirection.LeftToRight;
        referenceBuffer.Language = new RealHB::HarfBuzzSharp.Language(CultureInfo.InvariantCulture);
        referenceBuffer.GuessSegmentProperties();
        fonts.ReferenceFont.Shape(referenceBuffer, Array.Empty<ReferenceFeature>());
    }

    private static void AssertBuffersEqual(HBBuffer shimBuffer, ReferenceBuffer referenceBuffer)
    {
        Assert.Equal(referenceBuffer.Length, shimBuffer.Length);

        var shimInfos = shimBuffer.GetGlyphInfoSpan().ToArray();
        var referenceInfos = referenceBuffer.GetGlyphInfoSpan().ToArray();
        for (var i = 0; i < shimInfos.Length; i++)
        {
            Assert.Equal(referenceInfos[i].Codepoint, shimInfos[i].Codepoint);
            var referenceFlags = (int)referenceInfos[i].GlyphFlags;
            var shimFlags = (int)shimInfos[i].Flags;
            Assert.Equal(referenceFlags, shimFlags);
        }

        var shimPositions = shimBuffer.GetGlyphPositionSpan().ToArray();
        var referencePositions = referenceBuffer.GetGlyphPositionSpan().ToArray();
        for (var i = 0; i < shimPositions.Length; i++)
        {
            AssertAlmostEqual(referencePositions[i].XAdvance, shimPositions[i].XAdvance, nameof(VelloHB::HarfBuzzSharp.GlyphPosition.XAdvance));
            AssertAlmostEqual(referencePositions[i].YAdvance, shimPositions[i].YAdvance, nameof(VelloHB::HarfBuzzSharp.GlyphPosition.YAdvance));
            AssertAlmostEqual(referencePositions[i].XOffset, shimPositions[i].XOffset, nameof(VelloHB::HarfBuzzSharp.GlyphPosition.XOffset));
            AssertAlmostEqual(referencePositions[i].YOffset, shimPositions[i].YOffset, nameof(VelloHB::HarfBuzzSharp.GlyphPosition.YOffset));
        }
    }

    private static void AssertAlmostEqual(float expected, float actual, string property)
    {
        const float tolerance = 0.05f;
        if (float.IsNaN(expected) && float.IsNaN(actual))
        {
            return;
        }

        Assert.True(
            Math.Abs(expected - actual) <= tolerance,
            $"Expected {property}={expected}, Actual={actual}");
    }

    private sealed class FontScope : IDisposable
    {
        public FontScope(byte[] fontData, int unitsPerEm)
        {
            using var shimStream = new System.IO.MemoryStream(fontData, writable: false);
            ShimBlob = HBBlob.FromStream(shimStream);
            ShimFace = new HBFace(ShimBlob) { UnitsPerEm = (ushort)unitsPerEm };
            ShimFont = new HBFont(ShimFace);

            using var referenceStream = new System.IO.MemoryStream(fontData, writable: false);
            ReferenceBlob = ReferenceBlob.FromStream(referenceStream);
            ReferenceFace = new ReferenceFace(ReferenceBlob, 0);
            ReferenceFont = new ReferenceFont(ReferenceFace);
        }

        public HBBlob ShimBlob { get; }
        public HBFace ShimFace { get; }
        public HBFont ShimFont { get; }
        public ReferenceBlob ReferenceBlob { get; }
        public ReferenceFace ReferenceFace { get; }
        public ReferenceFont ReferenceFont { get; }

        public void Dispose()
        {
            ShimFont.Dispose();
            ShimFace.Dispose();
            ShimBlob.Dispose();
            ReferenceFont.Dispose();
            ReferenceFace.Dispose();
            ReferenceBlob.Dispose();
        }
    }

    private static FontScope CreateFonts(byte[] fontData, int unitsPerEm)
        => new(fontData, unitsPerEm);

    public void Dispose()
    {
    }
}
