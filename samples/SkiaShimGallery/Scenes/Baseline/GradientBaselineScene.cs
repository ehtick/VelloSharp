using System;
using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Baseline;

internal sealed class GradientBaselineScene : ISkiaGalleryScene
{
    public string Title => "Baseline: Gradient Coverage";

    public string Description => "Composite linear and radial gradients to compare color precision and tiling across the shim and Skia.";

    public SkiaSceneFeature Feature => SkiaSceneFeature.Gradients;

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(14, 14, 18));

        var linearRect = SKRect.Create(70, 80, info.Width - 140, info.Height / 2f - 90);
        using var linearShader = SKShader.CreateLinearGradient(
            new SKPoint(linearRect.Left, linearRect.Top),
            new SKPoint(linearRect.Right, linearRect.Bottom),
            new[]
            {
                new SKColor(255, 99, 132),
                new SKColor(54, 162, 235),
                new SKColor(255, 206, 86),
                new SKColor(75, 192, 192),
            },
            new[] { 0f, 0.35f, 0.65f, 1f },
            SKShaderTileMode.Clamp);

        using var fill = new SKPaint { Shader = linearShader };
        canvas.DrawRoundRect(linearRect, 36, 36, fill);

        var radialRect = SKRect.Create(120, linearRect.Bottom + 40, info.Width - 240, info.Height / 2f - 120);
        var radialCenterX = radialRect.Left + radialRect.Width * 0.5f;
        var radialCenterY = radialRect.Top + radialRect.Height * 0.5f;
        using var radialShader = SKShader.CreateRadialGradient(
            new SKPoint(radialCenterX, radialCenterY),
            Math.Min(radialRect.Width, radialRect.Height) / 2f,
            new[]
            {
                new SKColor(120, 123, 255, 220),
                new SKColor(195, 79, 238, 180),
                new SKColor(255, 255, 255, 0),
            },
            new[] { 0f, 0.6f, 1f },
            SKShaderTileMode.Clamp);

        using var stroke = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 6,
            Shader = radialShader,
        };

        canvas.DrawOval(radialRect, stroke);

        using var caption = new SKPaint
        {
            Color = new SKColor(215, 220, 230),
            TextSize = 20,
        };
        canvas.DrawText("Linear gradient sweep", linearRect.Left, linearRect.Bottom + 28, caption);
        canvas.DrawText("Radial gradient outline", radialRect.Left, radialRect.Bottom + 36, caption);
    }
}
