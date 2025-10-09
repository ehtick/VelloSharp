using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Shaders;

internal sealed class PictureShaderScene : ISkiaGalleryScene
{
    public string Title => "Shaders: Picture Shader";

    public string Description => "Converts an SKPicture into a shader and tiles it across a larger fill region.";

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(242, 248, 255));

        using var recorder = new SKPictureRecorder();
        var cullRect = SKRect.Create(0, 0, 80, 80);
        var pictureCanvas = recorder.BeginRecording(cullRect);

        using var background = new SKPaint
        {
            Color = new SKColor(120, 195, 255),
            IsAntialias = true,
        };
        pictureCanvas.DrawCircle(40, 40, 35, background);

        using var stroke = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 6,
            IsAntialias = true,
        };
        pictureCanvas.DrawCircle(40, 40, 26, stroke);

        using var markPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 18,
            IsAntialias = true,
        };
        pictureCanvas.DrawText("PS", 28, 46, markPaint);

        using var picture = recorder.EndRecording();

        var localMatrix = SKMatrix.CreateScale(0.8f, 0.8f);
        using var shader = picture.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Mirror, localMatrix, cullRect);

        using var paint = new SKPaint
        {
            Shader = shader,
            IsAntialias = true,
        };

        canvas.DrawRect(SKRect.Create(40, 60, 320, 180), paint);
    }
}
