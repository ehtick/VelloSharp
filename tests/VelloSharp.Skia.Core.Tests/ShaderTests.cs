using SkiaSharp;
using Xunit;

namespace VelloSharp.Skia.Core.Tests;

public sealed class ShaderTests
{
    [Fact]
    public void LinearGradientProducesInterpolatedColor()
    {
        using var surface = CreateSurface();
        var canvas = surface.Canvas;

        using var shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(surface.Info.Width, 0),
            new[] { new SKColor(255, 0, 0), new SKColor(0, 0, 255) },
            colorPos: null,
            SKShaderTileMode.Clamp);

        using var paint = new SKPaint { Shader = shader };
        canvas.DrawRect(SKRect.Create(0, 0, surface.Info.Width, surface.Info.Height), paint);

        var pixels = SurfaceRenderHelper.RenderToBgra(surface);
        var left = SurfaceRenderHelper.GetPixel(pixels, surface.Info, 4, surface.Info.Height / 2);
        var middle = SurfaceRenderHelper.GetPixel(pixels, surface.Info, surface.Info.Width / 2, surface.Info.Height / 2);
        var right = SurfaceRenderHelper.GetPixel(pixels, surface.Info, surface.Info.Width - 4, surface.Info.Height / 2);

        Assert.True(left.Red > left.Blue, "Left edge should favour red.");
        Assert.True(right.Blue > right.Red, "Right edge should favour blue.");
        Assert.True(middle.Red > 0 && middle.Blue > 0, "Midpoint should blend red/blue.");
    }

    [Fact]
    public void DrawImageProducesPixels()
    {
        using var tileSurface = SKSurface.Create(new SKImageInfo(2, 2, SKImageInfo.PlatformColorType, SKAlphaType.Premul));
        var tileCanvas = tileSurface.Canvas;
        tileCanvas.Clear(SKColors.Transparent);

        using (var yellowPaint = new SKPaint { Color = new SKColor(255, 255, 0) })
        {
            tileCanvas.DrawRect(SKRect.Create(0, 0, 1, 1), yellowPaint);
        }

        using (var blackPaint = new SKPaint { Color = new SKColor(0, 0, 0) })
        {
            tileCanvas.DrawRect(SKRect.Create(1, 0, 1, 1), blackPaint);
            tileCanvas.DrawRect(SKRect.Create(0, 1, 1, 1), blackPaint);
        }

        using var tileImage = tileSurface.Snapshot();

        using var surface = CreateSurface();
        var canvas = surface.Canvas;

        canvas.DrawImage(tileImage, SKRect.Create(0, 0, tileImage.Width, tileImage.Height));

        var pixels = SurfaceRenderHelper.RenderToBgra(surface);
        Assert.True(SurfaceRenderHelper.CountNonTransparentPixels(pixels) > 0, "Expected image drawing to write pixels.");
        Assert.NotEqual(0UL, SurfaceRenderHelper.ComputeChecksum(pixels));
    }

    private static SKSurface CreateSurface(int size = 64)
    {
        var info = new SKImageInfo(size, size, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        return SKSurface.Create(info);
    }
}
