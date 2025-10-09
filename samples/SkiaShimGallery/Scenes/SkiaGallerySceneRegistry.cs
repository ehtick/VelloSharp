using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Platform;
using SkiaSharp;

namespace SkiaGallery.SharedScenes;

public interface ISkiaGalleryScene
{
    string Title { get; }
    string Description { get; }
    void Render(SKCanvas canvas, SKImageInfo info);
}

public static class SkiaGallerySceneRegistry
{
    public static IReadOnlyList<ISkiaGalleryScene> All { get; } = new ISkiaGalleryScene[]
    {
        new BasicShapesScene(),
        new GradientScene(),
        new TextScene(),
        new ImageScene(),
        new PictureScene(),
    };
}

internal sealed class BasicShapesScene : ISkiaGalleryScene
{
    public string Title => "Basic geometry & strokes";
    public string Description => "Draws rectangles, circles, paths, and stroked outlines using the SkiaSharp shim API.";

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(SKColors.White);

        using var background = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(info.Width, info.Height),
                new[] { new SKColor(230, 245, 255), new SKColor(200, 220, 255) },
                null,
                SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(new SKRect(0, 0, info.Width, info.Height), background);

        using var fill = new SKPaint
        {
            Color = new SKColor(58, 109, 210),
            IsAntialias = true,
        };
        canvas.DrawRoundRect(new SKRect(48, 32, 220, 160), 24, 24, fill);

        using var circle = new SKPaint
        {
            Color = new SKColor(240, 120, 32),
            IsAntialias = true,
        };
        canvas.DrawCircle(260, 96, 48, circle);

        using var stroke = new SKPaint
        {
            Color = new SKColor(20, 30, 60),
            StrokeWidth = 6,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true,
        };

        using var path = new SKPath();
        path.MoveTo(80, 190);
        path.CubicTo(
            new SKPoint(140, 150),
            new SKPoint(200, 230),
            new SKPoint(260, 190));
        path.LineTo(300, 260);
        path.LineTo(60, 260);
        path.Close();
        canvas.DrawPath(path, stroke);
    }
}

internal sealed class GradientScene : ISkiaGalleryScene
{
    public string Title => "Gradient brushes & shaders";
    public string Description => "Uses linear, radial, and sweep gradients to showcase shader mapping.";

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(SKColors.White);

        using var linearPaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(32, 32),
                new SKPoint(240, 32),
                new[]
                {
                    new SKColor(255, 51, 102),
                    new SKColor(255, 204, 0),
                    new SKColor(0, 191, 165),
                },
                new[] { 0f, 0.5f, 1f },
                SKShaderTileMode.Clamp),
            IsAntialias = true,
        };
        canvas.DrawRect(new SKRect(32, 32, 240, 120), linearPaint);

        using var radialPaint = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(320, 80),
                60,
                new[] { new SKColor(102, 153, 255), SKColors.Transparent },
                null,
                SKShaderTileMode.Clamp),
            IsAntialias = true,
        };
        canvas.DrawCircle(320, 80, 60, radialPaint);

        using var sweepPaint = new SKPaint
        {
            Shader = SKShader.CreateSweepGradient(
                new SKPoint(140, 210),
                new[]
                {
                    new SKColor(64, 64, 255),
                    new SKColor(255, 64, 192),
                    new SKColor(64, 255, 192),
                    new SKColor(64, 64, 255),
                },
                new[] { 0f, 0.4f, 0.8f, 1f }),
            IsAntialias = true,
        };
        canvas.DrawOval(new SKRect(72, 160, 208, 280), sweepPaint);
    }
}

internal sealed class TextScene : ISkiaGalleryScene
{
    public string Title => "Text layout & glyph rendering";
    public string Description => "Draws multiple lines of text with varying fonts and sizes via the shim's font stack.";

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(SKColors.White);

        using var heading = new SKPaint
        {
            Color = new SKColor(30, 30, 40),
            TextSize = 36,
            IsAntialias = true,
        };
        canvas.DrawText("Vello-powered SkiaSharp shim", 32, 64, heading);

        using var subheading = new SKPaint
        {
            Color = new SKColor(80, 80, 100),
            TextSize = 20,
            IsAntialias = true,
        };
        canvas.DrawText("Fonts, hinting, and glyph metrics flow through VelloSharp.Text", 32, 96, subheading);

        using var body = new SKPaint
        {
            Color = new SKColor(40, 40, 50),
            TextSize = 18,
            IsAntialias = true,
        };

        var lines = new[]
        {
            "• Subpixel glyph positions honour the shim's matrix stack.",
            "• Typeface fallback resolves against the embedded Roboto font.",
            "• Text APIs reuse familiar SkiaSharp entry points.",
        };

        var y = 140f;
        foreach (var line in lines)
        {
            canvas.DrawText(line, 40, y, body);
            y += 28f;
        }
    }
}

internal sealed class ImageScene : ISkiaGalleryScene
{
    public string Title => "Bitmap decoding & image drawing";
    public string Description => "Decodes a PNG via the shim's SKImage APIs and draws it with scaling and opacity.";

    private SKImage? _cachedImage;

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(SKColors.White);

        var image = GetOrCreateImage();
        if (image is null)
        {
            using var fallback = new SKPaint { Color = SKColors.LightGray };
            canvas.DrawRect(new SKRect(32, 32, info.Width - 32, info.Height - 32), fallback);
            return;
        }

        canvas.DrawImage(image, new SKRect(32, 32, 192, 192));

        using var layerPaint = new SKPaint { Color = new SKColor(255, 255, 255, (byte)(0.6f * 255f)) };
        canvas.SaveLayer(layerPaint);
        canvas.DrawImage(image, new SKRect(220, 80, 380, 240));
        canvas.Restore();
    }

    private SKImage? GetOrCreateImage()
    {
        if (_cachedImage is { } cached)
        {
            return cached;
        }

        var assemblyName = typeof(ImageScene).Assembly.GetName().Name ?? throw new InvalidOperationException("Missing assembly name for the image asset host.");
        var uri = new Uri($"avares://{assemblyName}/Assets/avalonia-32.png");
        using var stream = AssetLoader.Open(uri);
        using var data = SKData.Create(stream);
        _cachedImage = SKImage.FromEncodedData(data);
        return _cachedImage;
    }
}

internal sealed class PictureScene : ISkiaGalleryScene
{
    public string Title => "Recording & picture playback";
    public string Description => "Uses SKPictureRecorder to capture a scene and replays it three times with transforms.";

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(SKColors.White);

        using var recorder = new SKPictureRecorder();
        var recordingCanvas = recorder.BeginRecording(new SKRect(0, 0, 160, 160));
        DrawBadge(recordingCanvas);
        var picture = recorder.EndRecording();

        canvas.Save();
        canvas.Translate(32, 32);
        canvas.DrawPicture(picture);
        canvas.Restore();

        canvas.Save();
        canvas.Translate(220, 36);
        canvas.Scale(0.8f, 0.8f);
        canvas.RotateDegrees(-10);
        canvas.DrawPicture(picture);
        canvas.Restore();

        canvas.Save();
        canvas.Translate(120, 210);
        canvas.Scale(1.2f, 1.2f);
        canvas.RotateDegrees(12);
        canvas.DrawPicture(picture);
        canvas.Restore();
    }

    private static void DrawBadge(SKCanvas canvas)
    {
        using var background = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(80, 80),
                80,
                new[] { new SKColor(86, 140, 255), new SKColor(19, 35, 88) },
                null,
                SKShaderTileMode.Clamp),
            IsAntialias = true,
        };
        canvas.DrawCircle(80, 80, 76, background);

        using var ring = new SKPaint
        {
            Color = new SKColor(255, 255, 255, 200),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 6,
            IsAntialias = true,
        };
        canvas.DrawCircle(80, 80, 56, ring);

        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 32,
            IsAntialias = true,
        };
        canvas.DrawText("Shim", 36, 92, textPaint);
    }
}
