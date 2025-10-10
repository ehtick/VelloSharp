using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Shaders;

internal sealed class LinearGradientScene : ISkiaGalleryScene
{
    public string Title => "Shaders: Linear Gradient";

    public string Description => "Applies a repeating linear gradient shader with custom stops and tile modes.";

    public SkiaSceneFeature Feature => SkiaSceneFeature.Gradients;

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(SKColors.White);

        using var gradient = SKShader.CreateLinearGradient(
            new SKPoint(40, 40),
            new SKPoint(220, 40),
            new[]
            {
                new SKColor(255, 96, 109),
                new SKColor(255, 208, 124),
                new SKColor(123, 201, 189),
            },
            new[] { 0f, 0.4f, 1f },
            SKShaderTileMode.Repeat);

        using var paint = new SKPaint
        {
            Shader = gradient,
            IsAntialias = true,
        };

        canvas.DrawRect(SKRect.Create(40, 60, 260, 120), paint);
    }
}
