using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Text;

internal sealed class TextSimpleScene : ISkiaGalleryScene
{
    public string Title => "Text: Paint";

    public string Description => "Renders simple strings with custom typeface selection and sizes.";

    public SkiaSceneFeature Feature => SkiaSceneFeature.Text;

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(248, 246, 250));

        using var typeface = SKTypeface.Default;
        using var paint = new SKPaint
        {
            Typeface = typeface,
            TextSize = 32,
            Color = new SKColor(60, 63, 110),
            IsAntialias = true,
        };

        canvas.DrawText("Hello from SKPaint!", 40, 90, paint);

        paint.TextSize = 20;
        paint.Color = new SKColor(104, 109, 160);
        canvas.DrawText($"Typeface: {typeface.FamilyName}", 40, 140, paint);
    }
}
