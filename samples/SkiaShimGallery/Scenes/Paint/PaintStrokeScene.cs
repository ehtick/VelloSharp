using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Paint;

internal sealed class PaintStrokeScene : ISkiaGalleryScene
{
    public string Title => "Paint: Stroke Styles";

    public string Description => "Configures stroke width, caps, joins, and miter limits to verify stroke path conversion.";

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(247, 247, 251));

        using var butt = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 14,
            StrokeCap = SKStrokeCap.Butt,
            StrokeJoin = SKStrokeJoin.Miter,
            StrokeMiter = 2f,
            Color = new SKColor(56, 109, 196),
            IsAntialias = true,
        };
        var buttPath = new SKPath();
        buttPath.MoveTo(60, 70);
        buttPath.LineTo(new SKPoint(240, 70));
        canvas.DrawPath(buttPath, butt);

        using var round = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 14,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            Color = new SKColor(245, 160, 102),
            IsAntialias = true,
        };
        var roundedPath = new SKPath();
        roundedPath.MoveTo(60, 120);
        roundedPath.LineTo(new SKPoint(200, 120));
        roundedPath.LineTo(new SKPoint(160, 200));
        roundedPath.Close();
        canvas.DrawPath(roundedPath, round);

        using var square = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 18,
            StrokeCap = SKStrokeCap.Square,
            StrokeJoin = SKStrokeJoin.Bevel,
            StrokeMiter = 8,
            Color = new SKColor(100, 195, 180, 217),
            IsAntialias = true,
        };
        var squarePath = new SKPath();
        squarePath.MoveTo(260, 70);
        squarePath.LineTo(new SKPoint(420, 70));
        canvas.DrawPath(squarePath, square);

        var bevelPath = new SKPath();
        bevelPath.MoveTo(320, 120);
        bevelPath.LineTo(new SKPoint(420, 180));
        bevelPath.LineTo(new SKPoint(280, 210));
        bevelPath.Close();
        canvas.DrawPath(bevelPath, square);
    }
}
