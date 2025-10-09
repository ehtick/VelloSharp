using System.Linq;
using SkiaSharp;
using Xunit;

namespace VelloSharp.Skia.Core.Tests;

public sealed class TextAndRecordingTests
{
    [Fact]
    public void DrawTextProducesInk()
    {
        using var surface = CreateSurface(width: 128, height: 64);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        using var paint = new SKPaint
        {
            Color = new SKColor(20, 20, 20),
            TextSize = 32,
            IsAntialias = true,
        };

        canvas.DrawText("Vello", 8, 40, paint);

        var pixels = SurfaceRenderHelper.RenderToBgra(surface);
        var (hasInk, darkPixelCount) = ScanForInk(pixels);

        Assert.True(hasInk, "Expected at least one non-white pixel from text rendering.");
        Assert.InRange(darkPixelCount, 50, pixels.Length / 4);
    }

    [Fact]
    public void PicturePlaybackMatchesDirectDraw()
    {
        var info = new SKImageInfo(96, 96, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

        using var baseline = SKSurface.Create(info);
        DrawSampleScene(baseline.Canvas);
        var baselinePixels = SurfaceRenderHelper.RenderToBgra(baseline);

        using var recorder = new SKPictureRecorder();
        var recordingCanvas = recorder.BeginRecording(SKRect.Create(0, 0, info.Width, info.Height));
        DrawSampleScene(recordingCanvas);
        var picture = recorder.EndRecording();

        using var replaySurface = SKSurface.Create(info);
        picture.Playback(replaySurface.Canvas);
        var replayPixels = SurfaceRenderHelper.RenderToBgra(replaySurface);

        Assert.Equal(SurfaceRenderHelper.ComputeChecksum(baselinePixels), SurfaceRenderHelper.ComputeChecksum(replayPixels));
        Assert.True(baselinePixels.SequenceEqual(replayPixels), "Picture replay should match direct rendering byte-for-byte.");
    }

    private static void DrawSampleScene(SKCanvas canvas)
    {
        canvas.Clear(SKColors.White);

        using var paint = new SKPaint
        {
            Color = new SKColor(34, 139, 230),
            IsAntialias = true,
        };

        canvas.DrawCircle(32, 32, 24, paint);

        using var textPaint = new SKPaint
        {
            Color = new SKColor(40, 40, 40),
            TextSize = 18,
            IsAntialias = true,
        };

        canvas.DrawText("Shim", 8, 72, textPaint);
    }

    private static (bool HasInk, int DarkPixels) ScanForInk(byte[] buffer)
    {
        var darkPixels = 0;
        for (var i = 0; i < buffer.Length; i += 4)
        {
            var b = buffer[i + 0];
            var g = buffer[i + 1];
            var r = buffer[i + 2];

            if (r < 240 || g < 240 || b < 240)
            {
                darkPixels++;
            }
        }

        return (darkPixels > 0, darkPixels);
    }

    private static SKSurface CreateSurface(int width, int height)
    {
        var info = new SKImageInfo(width, height, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
        return SKSurface.Create(info);
    }
}
