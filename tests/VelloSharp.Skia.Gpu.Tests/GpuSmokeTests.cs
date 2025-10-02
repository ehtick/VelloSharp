using System;
using System.IO;
using System.Numerics;
using SkiaSharp;
using VelloSharp;
using Xunit;

namespace VelloSharp.Skia.Gpu.Tests;

public class GpuSmokeTests
{
    [Fact]
    public void GpuBackend_RendersGradientStrokeAndGlyphs()
    {
        var info = new SKImageInfo(128, 128, SKImageInfo.PlatformColorType, SKAlphaType.Premul);

        using var surface = new GpuSurfaceBackend(info);
        var canvas = surface.CanvasBackend;

        surface.Clear(RgbaColor.FromBytes(20, 24, 32, 255));

        var gradientBrush = new LinearGradientBrush(
            new Vector2(0f, 0f),
            new Vector2(info.Width, info.Height),
            new[]
            {
                new GradientStop(0f, RgbaColor.FromBytes(255, 128, 0, 255)),
                new GradientStop(1f, RgbaColor.FromBytes(64, 0, 192, 255)),
            });

        var backgroundPath = new PathBuilder()
            .MoveTo(0, 0)
            .LineTo(info.Width, 0)
            .LineTo(info.Width, info.Height)
            .LineTo(0, info.Height)
            .Close();
        canvas.FillPath(backgroundPath, FillRule.NonZero, Matrix3x2.Identity, gradientBrush, null);

        var strokePath = new PathBuilder();
        strokePath.MoveTo(12f, info.Height - 16f);
        strokePath.LineTo(info.Width - 16f, 12f);
        var strokeBrush = new SolidColorBrush(RgbaColor.FromBytes(240, 240, 240, 255));
        var strokeStyle = new StrokeStyle { Width = 4.0 };
        canvas.StrokePath(strokePath, strokeStyle, Matrix3x2.Identity, strokeBrush, null);

        var fontPath = Path.Combine(AppContext.BaseDirectory, "Fonts", "Roboto-Regular.ttf");
        Assert.True(File.Exists(fontPath), $"Font asset not found at {fontPath}.");
        var fontData = File.ReadAllBytes(fontPath);
        using var font = Font.Load(fontData);

        Assert.True(font.TryGetGlyphIndex('A', out var glyphId), "Failed to resolve glyph for 'A'.");
        var glyphs = new[] { new Glyph(glyphId, 36f, 80f) };
        var glyphOptions = new GlyphRunOptions
        {
            Brush = new SolidColorBrush(RgbaColor.FromBytes(250, 250, 250, 255)),
            FontSize = 36f,
            Hint = false,
        };
        canvas.DrawGlyphRun(font, glyphs, glyphOptions);

        using var snapshot = surface.Snapshot();
        var rowBytes = info.RowBytes > 0 ? info.RowBytes : info.Width * 4;
        var pixelBuffer = new byte[rowBytes * info.Height];
        var pixmap = new SKPixmap(info, pixelBuffer, rowBytes);
        Assert.True(snapshot.ScalePixels(pixmap, SKSamplingOptions.Default));
        var pixels = pixmap.GetReadOnlyPixels();

        var hasColor = false;
        foreach (var value in pixels)
        {
            if (value != 0)
            {
                hasColor = true;
                break;
            }
        }

        Assert.True(hasColor, "Rendered GPU image was empty.");
    }
}
