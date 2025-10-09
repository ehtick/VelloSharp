using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Shaders;

internal sealed class RadialGradientScene : ISkiaGalleryScene
{
    public string Title => "Shaders: Radial Gradient";

    public string Description => "Creates a mirrored radial gradient to demonstrate center/radius handling.";

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(245, 244, 250));

        using var radial = SKShader.CreateRadialGradient(
            new SKPoint(180, 120),
            80,
            new[]
            {
                new SKColor(104, 153, 253),
                new SKColor(51, 86, 220),
                new SKColor(12, 37, 125),
            },
            null,
            SKShaderTileMode.Mirror);

        using var paint = new SKPaint
        {
            Shader = radial,
            IsAntialias = true,
        };

        canvas.DrawCircle(180, 120, 90, paint);
    }
}
