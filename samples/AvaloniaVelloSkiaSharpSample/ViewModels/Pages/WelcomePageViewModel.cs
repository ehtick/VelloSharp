using System;
using Avalonia.Media;
using AvaloniaVelloSkiaSharpSample.Rendering;
using SkiaSharp;

namespace AvaloniaVelloSkiaSharpSample.ViewModels.Pages;

public sealed class WelcomePageViewModel : SamplePageViewModel
{
    public WelcomePageViewModel()
        : base(
            "Welcome",
            "Overview of the SkiaSharp shim gallery running through the Vello lease pipeline.",
            "\u2605")
    {
    }

    public override void Render(in SkiaLeaseRenderContext context)
    {
        var canvas = context.Canvas;
        var info = context.ImageInfo;

        canvas.Clear(new SKColor(12, 18, 32, 255));

        using var gradientPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(info.Width * 0.5f, info.Height * 0.4f),
                Math.Max(info.Width, info.Height) * 0.65f,
                new[]
                {
                    new SKColor(96, 214, 255, 255),
                    new SKColor(126, 125, 255, 255),
                    new SKColor(216, 95, 255, 255),
                },
                null,
                SKShaderTileMode.Clamp),
            IsAntialias = true,
        };

        var capsuleRect = new SKRect(
            info.Width * 0.2f,
            info.Height * 0.22f,
            info.Width * 0.8f,
            info.Height * 0.62f);
        canvas.DrawRoundRect(capsuleRect, info.Width * 0.08f, info.Height * 0.08f, gradientPaint);

        using var ringPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            IsAntialias = true,
            StrokeWidth = Math.Max(info.Width, info.Height) * 0.012f,
            Color = new SKColor(255, 255, 255, 96),
            StrokeCap = SKStrokeCap.Round,
        };

        var radius = Math.Min(info.Width, info.Height) * 0.38f;
        canvas.DrawCircle(info.Width * 0.5f, info.Height * 0.5f, radius, ringPaint);

        using var headlinePaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            Typeface = SKTypeface.Default,
        };

        var headlineSize = Math.Min(info.Width, info.Height) * 0.07f;
        headlinePaint.TextSize = headlineSize;
        var headline = "Vello + SkiaSharp";
        var headlineWidth = ApproximateTextWidth(headline, headlineSize);
        var headlineX = (info.Width - headlineWidth) * 0.5f;
        var headlineY = info.Height * 0.72f;
        canvas.DrawText(headline, headlineX, headlineY, headlinePaint);

        using var captionPaint = new SKPaint
        {
            Color = new SKColor(220, 230, 245, 200),
            IsAntialias = true,
            Typeface = SKTypeface.Default,
        };

        var captionSize = Math.Min(info.Width, info.Height) * 0.033f;
        captionPaint.TextSize = captionSize;
        var caption = $"Frame {context.Frame} · {context.Elapsed:mm\\:ss}";
        var captionWidth = ApproximateTextWidth(caption, captionSize);
        var captionX = (info.Width - captionWidth) * 0.5f;
        var captionY = info.Height * 0.82f;
        canvas.DrawText(caption, captionX, captionY, captionPaint);
    }

    private static float ApproximateTextWidth(string text, float size)
    {
        // Simple heuristic to centre text without access to full text metrics.
        // The factor was chosen empirically for Latin characters.
        const float AverageGlyphWidthFactor = 0.55f;
        return text.Length * size * AverageGlyphWidthFactor;
    }
}
