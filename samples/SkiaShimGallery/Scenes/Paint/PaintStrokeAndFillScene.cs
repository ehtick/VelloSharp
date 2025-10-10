using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Paint;

internal sealed class PaintStrokeAndFillScene : ISkiaGalleryScene
{
    public string Title => "Paint: Stroke & Fill";

    public string Description => "Uses the StrokeAndFill paint style to render outlined shapes in a single draw call.";

    public SkiaSceneFeature Feature => SkiaSceneFeature.Paint;

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(253, 249, 245));

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.StrokeAndFill,
            StrokeWidth = 6,
            Color = new SKColor(235, 133, 131),
            IsAntialias = true,
        };

        canvas.DrawCircle(120, 120, 56, paint);
        canvas.DrawRect(SKRect.Create(220, 70, 140, 100), paint);
        canvas.DrawOval(SKRect.Create(400, 60, 160, 110), paint);
    }
}
