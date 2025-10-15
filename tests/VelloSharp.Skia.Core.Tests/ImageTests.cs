using System;
using System.IO;
using SkiaSharp;
using Xunit;

namespace VelloSharp.Skia.Core.Tests;

public sealed class ImageTests
{
    [Fact]
    public void FromPixels_BgraInputIsRespected()
    {
        var info = new SKImageInfo(1, 1, SKColorType.Bgra8888, SKAlphaType.Premul);
        var pixels = new byte[] { 0, 0, 255, 255 }; // BGRA => solid red

        using var image = SKImage.FromPixels(info, pixels, info.RowBytes);
        Assert.NotNull(image);

        using var surface = SKSurface.Create(new SKImageInfo(16, 16, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawImage(image, SKRect.Create(0, 0, 16, 16));

        var rendered = SurfaceRenderHelper.RenderToBgra(surface);
        var centre = SurfaceRenderHelper.GetPixel(rendered, surface.Info, 8, 8);

        Assert.Equal(255, centre.Red);
        Assert.Equal(0, centre.Green);
        Assert.Equal(0, centre.Blue);
    }

    [Fact]
    public void DecodePng_ReturnsExpectedDimensions()
    {
        var assetPath = Path.Combine(AppContext.BaseDirectory, "Assets", "avalonia-32.png");
        Assert.True(File.Exists(assetPath), $"Test asset not found at '{assetPath}'.");

        using var skData = SKData.Create(File.OpenRead(assetPath));

        using var image = SKImage.FromEncodedData(skData);
        Assert.NotNull(image);
        Assert.True(image.Width >= 32);
        Assert.True(image.Height >= 32);

        var destinationInfo = new SKImageInfo(image.Width, image.Height, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul);
        var buffer = new byte[destinationInfo.RowBytes * destinationInfo.Height];
        var pixmap = new SKPixmap(destinationInfo, buffer, destinationInfo.RowBytes);

        var scaled = image.ScalePixels(pixmap, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        Assert.True(scaled);

        // Ensure at least one non-white pixel was decoded
        var hasNonWhite = false;
        for (var i = 0; i < buffer.Length; i += 4)
        {
            var b = buffer[i + 0];
            var g = buffer[i + 1];
            var r = buffer[i + 2];
            if (r != 255 || g != 255 || b != 255)
            {
                hasNonWhite = true;
                break;
            }
        }

        Assert.True(hasNonWhite, "Decoded PNG should contain coloured pixels.");
    }
}
