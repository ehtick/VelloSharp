using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Canvas;

internal sealed class CanvasRoundAndOvalScene : ISkiaGalleryScene
{
    public string Title => "Canvas: RoundRects & Ovals";

    public string Description => "Draws rounded rectangles and ovals to exercise the helper geometry conversions inside the shim.";

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(245, 250, 255));

        using var roundFill = new SKPaint
        {
            Color = new SKColor(255, 176, 67),
            IsAntialias = true,
        };
        canvas.DrawRoundRect(SKRect.Create(56, 64, 160, 110), 28, 28, roundFill);

        using var ovalPaint = new SKPaint
        {
            Color = new SKColor(98, 162, 228),
            IsAntialias = true,
        };
        canvas.DrawOval(SKRect.Create(260, 80, 180, 120), ovalPaint);

        using var outline = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3f,
            StrokeJoin = SKStrokeJoin.Miter,
            Color = new SKColor(36, 62, 104),
            IsAntialias = true,
        };

        canvas.DrawRoundRect(SKRect.Create(480, 60, 180, 120), 40, 20, outline);
        canvas.DrawOval(SKRect.Create(520, 200, 140, 80), outline);
    }
}
