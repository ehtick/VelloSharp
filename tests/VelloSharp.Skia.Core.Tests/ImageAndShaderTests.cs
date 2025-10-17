using System;
using System.IO;
using SkiaSharp;
using Xunit;

namespace VelloSharp.Skia.Core.Tests;

public class ImageAndShaderTests
{
    [Fact]
    public void PaintColorF_Roundtrip()
    {
        using var paint = new SKPaint();
        var colorF = new SKColorF(0.25f, 0.5f, 0.75f, 0.6f);

        paint.ColorF = colorF;

        var color = paint.Color;
        Assert.InRange(color.Red, 0, 255);
        Assert.InRange(color.Green, 0, 255);
        Assert.InRange(color.Blue, 0, 255);
        Assert.InRange(color.Alpha, 0, 255);

        var roundtrip = paint.ColorF;
        Assert.InRange(Math.Abs(roundtrip.Red - colorF.Red), 0f, 0.01f);
        Assert.InRange(Math.Abs(roundtrip.Green - colorF.Green), 0f, 0.01f);
        Assert.InRange(Math.Abs(roundtrip.Blue - colorF.Blue), 0f, 0.01f);
        Assert.InRange(Math.Abs(roundtrip.Alpha - colorF.Alpha), 0f, 0.01f);
    }

    [Fact]
    public void ColorEmpty_EqualsDefault()
    {
        Assert.Equal(default, SKColor.Empty);
    }

    [Fact]
    public void DataSaveTo_WritesStream()
    {
        using var data = SKData.CreateCopy(new byte[] { 1, 2, 3 });
        using var stream = new MemoryStream();

        data.SaveTo(stream);

        Assert.Equal(new byte[] { 1, 2, 3 }, stream.ToArray());
    }

    [Fact]
    public void ImageEncode_ReturnsPngData()
    {
        var info = new SKImageInfo(2, 2, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        var pixels = new byte[info.BytesSize];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i + 0] = 255;
            pixels[i + 1] = 0;
            pixels[i + 2] = 0;
            pixels[i + 3] = 255;
        }

        using var image = SKImage.FromPixels(info, pixels, info.RowBytes);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 90);

        Assert.True(encoded.AsSpan().Length > 0);

        using var bitmap = SKBitmap.Decode(encoded);
        Assert.NotNull(bitmap);
        Assert.Equal(info.Width, bitmap!.Width);
    }

    [Fact]
    public void ImageToShader_DoesNotDisposeOriginal()
    {
        var info = new SKImageInfo(1, 1, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        var pixels = new byte[info.BytesSize];
        pixels[3] = 255; // opaque

        using var image = SKImage.FromPixels(info, pixels, info.RowBytes);
        var originalWidth = image.Width;

        using (var shader = image.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, SKMatrix.CreateIdentity()))
        {
            Assert.NotNull(shader);
        }

        Assert.Equal(originalWidth, image.Width);
    }

    [Fact]
    public void ShaderCreateBitmap_ReturnsShader()
    {
        var info = new SKImageInfo(1, 1, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        using var bitmap = new SKBitmap(info);
        bitmap.Erase(SKColors.Blue);

        using var shader = SKShader.CreateBitmap(bitmap, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
        Assert.NotNull(shader);
    }

    [Fact]
    public void ImageToRasterImage_ReturnsCopy()
    {
        var info = new SKImageInfo(2, 2, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        var pixels = new byte[info.BytesSize];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i + 0] = 0;
            pixels[i + 1] = 255;
            pixels[i + 2] = 0;
            pixels[i + 3] = 255;
        }

        using var image = SKImage.FromPixels(info, pixels, info.RowBytes);
        using var copy = image.ToRasterImage(true);
        Assert.NotSame(image, copy);

        // Original image remains valid after disposing the copy.
        Assert.Equal(info.Width, image.Width);
        Assert.Equal(info.Height, image.Height);
    }

    [Fact]
    public void TypefaceReportsGlyphMetadata()
    {
        var typeface = SKTypeface.Default;
        Assert.True(typeface.GlyphCount > 0);
        _ = typeface.IsFixedPitch;
    }

    [Fact]
    public void FontReturnsGlyphPath()
    {
        using var font = new SKFont(SKTypeface.Default, 16f);
        var glyphs = font.GetGlyphs("A");
        Assert.NotEmpty(glyphs);

        var path = font.GetGlyphPath(glyphs[0]);
        Assert.NotNull(path);
        Assert.False(path!.IsEmpty);
        path.Dispose();
    }
}
