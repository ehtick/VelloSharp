using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Canvas;

internal sealed class CanvasClipAndLayerScene : ISkiaGalleryScene
{
    public string Title => "Canvas: Clip & Layers";

    public string Description => "Demonstrates clip rectangles plus the SaveLayer overloads with opacity and bounds.";

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(244, 248, 254));

        using var background = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(info.Width, info.Height),
                new[]
                {
                    new SKColor(225, 236, 255),
                    new SKColor(205, 224, 255),
                },
                null,
                SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(new SKRect(0, 0, info.Width, info.Height), background);

        var clippedArea = SKRect.Create(48, 32, 200, 140);
        canvas.Save();
        canvas.ClipRect(clippedArea);
        canvas.SaveLayer(); // basic layer

        using var clippedFill = new SKPaint
        {
            Color = new SKColor(62, 122, 214),
            IsAntialias = true,
        };
        canvas.DrawRoundRect(SKRect.Create(24, 16, 200, 120), 24, 24, clippedFill);

        canvas.Restore(); // layer
        canvas.Restore(); // clip

        var boundedLayerRect = SKRect.Create(280, 32, 200, 140);
        canvas.Save();
        using var translucent = new SKPaint
        {
            Color = new SKColor(245, 109, 124, 180),
            IsAntialias = true,
        };
        canvas.SaveLayer(boundedLayerRect, translucent);

        using var white = new SKPaint { Color = new SKColor(255, 255, 255, 200), IsAntialias = true };
        canvas.DrawOval(boundedLayerRect, white);
        canvas.DrawOval(boundedLayerRect, translucent);

        canvas.Restore(); // bound layer
        canvas.Restore();

        canvas.Save();
        var rectLayerBounds = SKRect.Create(520, 32, 180, 140);
#if REAL_SKIA
        canvas.SaveLayer(rectLayerBounds, null);
#else
        canvas.SaveLayer(rectLayerBounds);
#endif
        using var accent = new SKPaint
        {
            Color = new SKColor(92, 200, 170),
            IsAntialias = true,
        };
        var rectCenterX = rectLayerBounds.Left + (rectLayerBounds.Right - rectLayerBounds.Left) * 0.5f;
        var rectCenterY = rectLayerBounds.Top + (rectLayerBounds.Bottom - rectLayerBounds.Top) * 0.5f;
        canvas.DrawCircle(rectCenterX, rectCenterY, 64, accent);

        using var border = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 4,
            Color = new SKColor(16, 60, 90),
            IsAntialias = true,
        };
        canvas.DrawRect(rectLayerBounds, border);
        canvas.Restore(); // rect-bound layer
        canvas.Restore();
    }
}
