using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Canvas;

internal sealed class CanvasBasicsScene : ISkiaGalleryScene
{
    public string Title => "Canvas: Basic Fills";

    public string Description => "Clears the surface and draws simple filled rectangles and circles to exercise baseline draw calls.";

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(242, 244, 248));

        using var rectPaint = new SKPaint
        {
            Color = new SKColor(40, 110, 210),
            IsAntialias = true,
        };
        canvas.DrawRect(SKRect.Create(40, 36, 140, 96), rectPaint);

        using var circlePaint = new SKPaint
        {
            Color = new SKColor(246, 114, 128),
            IsAntialias = true,
        };

        canvas.DrawCircle(240, 84, 48, circlePaint);
        canvas.DrawCircle(new SKPoint(340, 140), 36, circlePaint);

        using var border = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            Color = new SKColor(25, 45, 80),
            IsAntialias = true,
        };

        canvas.DrawRect(SKRect.Create(24, 24, info.Width - 48, info.Height - 48), border);
    }
}
