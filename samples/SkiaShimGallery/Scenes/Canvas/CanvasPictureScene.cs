using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Canvas;

internal sealed class CanvasPictureScene : ISkiaGalleryScene
{
    public string Title => "Canvas: Pictures";

    public string Description => "Records a picture and replays it with and without an additional matrix, then flushes the canvas.";

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(252, 252, 255));

        using var recorder = new SKPictureRecorder();
        var pictureRect = SKRect.Create(0, 0, 140, 140);
        var pictureCenter = new SKPoint(70, 70);
        var recordingCanvas = recorder.BeginRecording(pictureRect);
        DrawBadge(recordingCanvas);
        using var picture = recorder.EndRecording();

        canvas.DrawPicture(picture);

        var matrix = SKMatrix.CreateTranslation(200, 10);
        matrix = matrix.PreConcat(SKMatrix.CreateScale(0.9f, 0.9f));
#if REAL_SKIA
        canvas.DrawPicture(picture, ref matrix);
#else
        canvas.DrawPicture(picture, matrix);
#endif

        var secondMatrix = SKMatrix.CreateRotationDegrees(12f, pictureCenter.X, pictureCenter.Y);
        secondMatrix = secondMatrix.PreConcat(SKMatrix.CreateTranslation(400, 30));
#if REAL_SKIA
        canvas.DrawPicture(picture, ref secondMatrix);
#else
        canvas.DrawPicture(picture, secondMatrix);
#endif

        canvas.Flush();
    }

    private static void DrawBadge(SKCanvas canvas)
    {
        using var background = new SKPaint
        {
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(70, 70),
                70,
                new[]
                {
                    new SKColor(123, 160, 247),
                    new SKColor(59, 88, 158),
                },
                null,
                SKShaderTileMode.Clamp),
            IsAntialias = true,
        };
        canvas.DrawCircle(70, 70, 68, background);

        using var ring = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 6f,
            Color = SKColors.White,
            IsAntialias = true,
        };
        canvas.DrawCircle(70, 70, 54, ring);

        using var label = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 28,
            IsAntialias = true,
        };
        canvas.DrawText("SR", 52, 82, label);
    }
}
