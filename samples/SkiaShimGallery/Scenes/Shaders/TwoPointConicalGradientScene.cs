using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Shaders;

internal sealed class TwoPointConicalGradientScene : ISkiaGalleryScene
{
    public string Title => "Shaders: Two-Point Conical";

    public string Description => "Demonstrates two-point conical gradients with independent radii.";

    public SkiaSceneFeature Feature => SkiaSceneFeature.Gradients;

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(250, 250, 252));

        using var shader = SKShader.CreateTwoPointConicalGradient(
            new SKPoint(120, 100),
            10,
            new SKPoint(200, 160),
            80,
            new[]
            {
                new SKColor(255, 255, 255),
                new SKColor(180, 210, 255),
                new SKColor(70, 90, 180),
            },
            new[] { 0f, 0.45f, 1f },
            SKShaderTileMode.Clamp);

        using var paint = new SKPaint
        {
            Shader = shader,
            IsAntialias = true,
        };

        canvas.DrawOval(SKRect.Create(80, 60, 220, 140), paint);
    }
}
