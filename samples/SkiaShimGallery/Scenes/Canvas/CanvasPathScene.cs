using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Canvas;

internal sealed class CanvasPathScene : ISkiaGalleryScene
{
    public string Title => "Canvas: Paths";

    public string Description => "Builds a multi-segment SKPath with curves and closes the contour to validate stroke rendering.";

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(250, 248, 252));

        using var fill = new SKPaint
        {
            Color = new SKColor(104, 189, 166),
            IsAntialias = true,
        };

        using var stroke = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4f,
            StrokeJoin = SKStrokeJoin.Round,
            StrokeCap = SKStrokeCap.Round,
            Color = new SKColor(40, 92, 83),
            IsAntialias = true,
        };

        var path = new SKPath();
        path.MoveTo(60, 180);
        path.CubicTo(new SKPoint(120, 60), new SKPoint(200, 60), new SKPoint(260, 180));
        path.LineTo(220, 220);
        path.QuadTo(new SKPoint(180, 260), new SKPoint(120, 200));
        path.Close();

        canvas.DrawPath(path, fill);
        canvas.DrawPath(path, stroke);

        using var smallPathPaint = new SKPaint
        {
            Color = new SKColor(65, 105, 225),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3f,
            IsAntialias = true,
        };

        var tinyPath = new SKPath();
        tinyPath.MoveTo(360, 100);
        tinyPath.QuadTo(new SKPoint(420, 40), new SKPoint(480, 120));
        tinyPath.CubicTo(new SKPoint(520, 160), new SKPoint(460, 220), new SKPoint(520, 260));
        tinyPath.Close();

        canvas.DrawPath(tinyPath, smallPathPaint);
    }
}
