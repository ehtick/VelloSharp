using System;
using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Baseline;

internal sealed class GeometryStressBaselineScene : ISkiaGalleryScene
{
    public string Title => "Baseline: Geometry Stress";

    public string Description => "Emits hundreds of rotating path segments to surface tessellation gaps and winding issues.";

    public SkiaSceneFeature Feature => SkiaSceneFeature.Geometry;

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(8, 8, 12));
        canvas.Translate(info.Width / 2f, info.Height / 2f);

        using var stroke = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.5f,
            Color = new SKColor(255, 255, 255, 180),
            IsAntialias = true,
        };

        for (var i = 0; i < 180; i++)
        {
            canvas.Save();
            canvas.RotateDegrees(i * 2f);

            using var path = new SKPath();
            path.MoveTo(0, -20);

            for (var radius = 40; radius <= 220; radius += 12)
            {
                var angle = (float)(radius * Math.PI / 14);
                path.LineTo(
                    (float)(Math.Cos(angle) * radius),
                    (float)(Math.Sin(angle) * radius));
            }

            canvas.DrawPath(path, stroke);
            canvas.Restore();
        }
    }
}
