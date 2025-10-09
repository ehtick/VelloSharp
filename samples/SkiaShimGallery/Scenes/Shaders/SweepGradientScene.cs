using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Shaders;

internal sealed class SweepGradientScene : ISkiaGalleryScene
{
    public string Title => "Shaders: Sweep Gradient";

    public string Description => "Uses a sweep gradient with a local matrix to rotate colours around the origin.";

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(247, 244, 255));

        var local = SKMatrix.CreateRotationDegrees(30, 0, 0);

        using var sweep = SKShader.CreateSweepGradient(
            new SKPoint(180, 140),
            new[]
            {
                new SKColor(235, 99, 209),
                new SKColor(255, 200, 64),
                new SKColor(89, 207, 163),
                new SKColor(235, 99, 209),
            },
            new[] { 0f, 0.33f, 0.66f, 1f },
            local);

        using var paint = new SKPaint
        {
            Shader = sweep,
            IsAntialias = true,
        };

        canvas.DrawCircle(180, 140, 90, paint);
    }
}
