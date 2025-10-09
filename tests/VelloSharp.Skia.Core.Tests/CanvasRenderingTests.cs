using SkiaSharp;
using VelloSharp;
using Xunit;

namespace VelloSharp.Skia.Core.Tests;

public sealed class CanvasRenderingTests
{
    [Fact]
    public void DrawRect_WritesSolidPixels()
    {
        using var surface = CreateSurface();
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        using var paint = new SKPaint
        {
            Color = new SKColor(32, 160, 240),
            IsAntialias = true,
        };

        var rect = SKRect.Create(8, 8, 32, 32);
        canvas.DrawRect(rect, paint);

        var pixels = SurfaceRenderHelper.RenderToBgra(surface);
        var inside = SurfaceRenderHelper.GetPixel(pixels, surface.Info, 24, 24);
        var outside = SurfaceRenderHelper.GetPixel(pixels, surface.Info, 2, 2);

        Assert.True(inside.Red > 30 && inside.Green > 150 && inside.Blue > 200, $"Expected cyan-ish fill, actual {inside}.");
        Assert.Equal(byte.MaxValue, inside.Alpha);
        Assert.Equal(SKColors.White.Red, outside.Red);
        Assert.Equal(SKColors.White.Green, outside.Green);
        Assert.Equal(SKColors.White.Blue, outside.Blue);
    }

    [Fact]
    public void PaintOpacityAffectsResult()
    {
        using var surface = CreateSurface();
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Black);

        using var paint = new SKPaint
        {
            Color = new SKColor(0, 255, 0),
            Opacity = 0.5f,
            IsAntialias = true,
        };

        canvas.DrawRect(SKRect.Create(8, 8, 32, 32), paint);

        var pixels = SurfaceRenderHelper.RenderToBgra(surface);
        var inside = SurfaceRenderHelper.GetPixel(pixels, surface.Info, 16, 16);

        Assert.InRange(inside.Green, 1, 254);
        Assert.True(inside.Red < 10 && inside.Blue < 10, "Opacity fill should preserve green channel only.");
    }

    [Fact]
    public void QuickRejectRecognisesOutsideBounds()
    {
        using var surface = CreateSurface();
        var canvas = surface.Canvas;

        Assert.False(canvas.QuickReject(SKRect.Create(10, 10, 20, 20)));
        Assert.True(canvas.QuickReject(SKRect.Create(1000, 1000, 50, 50)));
    }

    private static SKSurface CreateSurface(int size = 64)
    {
        var info = new SKImageInfo(size, size, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        return SKSurface.Create(info);
    }
}
