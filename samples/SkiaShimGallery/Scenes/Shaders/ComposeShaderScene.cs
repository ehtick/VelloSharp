using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Shaders;

internal sealed class ComposeShaderScene : ISkiaGalleryScene
{
    public string Title => "Shaders: Compose";

    public string Description => "Composes two shaders to validate combination routing inside the shim.";

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(250, 248, 250));

        using var linear = SKShader.CreateLinearGradient(
            new SKPoint(40, 40),
            new SKPoint(240, 40),
            new[]
            {
                new SKColor(255, 138, 101),
                new SKColor(255, 213, 79),
            },
            null,
            SKShaderTileMode.Clamp);

        using var radial = SKShader.CreateRadialGradient(
            new SKPoint(180, 140),
            100,
            new[]
            {
                new SKColor(96, 125, 139, 220),
                new SKColor(38, 50, 56, 0),
            },
            null,
            SKShaderTileMode.Decal);

        using var composed = SKShader.CreateCompose(linear, radial);

        using var paint = new SKPaint
        {
            Shader = composed,
            IsAntialias = true,
        };

        canvas.DrawRect(SKRect.Create(60, 60, 240, 160), paint);
    }
}
