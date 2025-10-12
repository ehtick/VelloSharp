extern alias VelloHB;

using System;
using System.Globalization;
using Xunit;
using HBBlob = VelloHB::HarfBuzzSharp.Blob;
using HBBuffer = VelloHB::HarfBuzzSharp.Buffer;
using HBFace = VelloHB::HarfBuzzSharp.Face;
using HBFeature = VelloHB::HarfBuzzSharp.Feature;
using HBFont = VelloHB::HarfBuzzSharp.Font;

namespace VelloTextParityTests;

public sealed class HarfBuzzShimSmokeTests : IDisposable
{
    private readonly HBBlob _blob;
    private readonly HBFace _face;
    private readonly HBFont _font;

    public HarfBuzzShimSmokeTests()
    {
        using var stream = TestFontLoader.OpenPrimaryFontStream();
        _blob = HBBlob.FromStream(stream);
        _face = new HBFace(_blob) { UnitsPerEm = (ushort)TestFontLoader.PrimaryUnitsPerEm };
        _font = new HBFont(_face);
    }

    [Fact]
    public void ShapesLatinTextWithoutErrors()
    {
        using var buffer = CreateBuffer("Hello Vello", VelloHB::HarfBuzzSharp.Direction.LeftToRight);
        _font.Shape(buffer, Array.Empty<HBFeature>());

        Assert.Equal(VelloHB::HarfBuzzSharp.ContentType.Glyphs, buffer.ContentType);
        Assert.True(buffer.Length > 0);

#if DEBUG
        var debugDump = buffer.DebugDescribeGlyphs();
        Assert.StartsWith("[", debugDump, StringComparison.Ordinal);
#endif
    }

    [Fact]
    public void ShapesRtlTextWithoutErrors()
    {
        using var buffer = CreateBuffer("مرحبا بالعالم", VelloHB::HarfBuzzSharp.Direction.RightToLeft);
        _font.Shape(buffer, Array.Empty<HBFeature>());

        Assert.Equal(VelloHB::HarfBuzzSharp.ContentType.Glyphs, buffer.ContentType);
        Assert.True(buffer.Length > 0);
    }

    private static HBBuffer CreateBuffer(string text, VelloHB::HarfBuzzSharp.Direction direction)
    {
        var buffer = new HBBuffer();
        buffer.AddUtf16(text.AsSpan(), 0, text.Length);
        buffer.Direction = direction;
        buffer.Language = new VelloHB::HarfBuzzSharp.Language(CultureInfo.InvariantCulture);
        buffer.GuessSegmentProperties();
        return buffer;
    }

    public void Dispose()
    {
        _font.Dispose();
        _face.Dispose();
        _blob.Dispose();
    }
}
