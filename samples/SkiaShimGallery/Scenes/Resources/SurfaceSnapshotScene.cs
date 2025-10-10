using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Resources;

internal sealed class SurfaceSnapshotScene : ISkiaGalleryScene
{
    public string Title => "Surface: Snapshot & Draw";

    public string Description => "Renders content into an SKSurface, snapshots it, and draws both the image and surface.";

    public SkiaSceneFeature Feature => SkiaSceneFeature.Resources;

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(244, 248, 252));

        var surfaceInfo = new SKImageInfo(128, 128, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var surface = SKSurface.Create(surfaceInfo);
        var surfaceCanvas = surface.Canvas;
        surfaceCanvas.Clear(new SKColor(90, 140, 220));

        using var ring = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 8,
            Color = SKColors.White,
            IsAntialias = true,
        };
        surfaceCanvas.DrawCircle(64, 64, 50, ring);

        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 28,
            IsAntialias = true,
        };
        surfaceCanvas.DrawText("SF", 46, 74, textPaint);

        using var snapshot = surface.Snapshot();
        canvas.DrawImage(snapshot, SKRect.Create(40, 40, 160, 160));

        surface.Draw(canvas, 240, 60, null);
    }
}
