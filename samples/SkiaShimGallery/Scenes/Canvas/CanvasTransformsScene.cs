using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Canvas;

internal sealed class CanvasTransformsScene : ISkiaGalleryScene
{
    public string Title => "Canvas: Transforms";

    public string Description => "Exercises save/restore, matrix operations, and quick reject logic with simple translated cards.";

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(250, 251, 255));

        using var basePaint = new SKPaint
        {
            Color = new SKColor(76, 120, 196),
            IsAntialias = true,
        };

        using var outline = new SKPaint
        {
            Color = new SKColor(32, 54, 91),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2.5f,
            IsAntialias = true,
        };

        var card = SKRect.Create(0, 0, 120, 80);
        var cardCenter = new SKPoint(60, 40);

        canvas.Save();
        canvas.Translate(42, 36);
        canvas.Scale(1.1f);
        canvas.RotateDegrees(8f, cardCenter.X, cardCenter.Y);
        DrawCard(canvas, card, basePaint, outline);
        canvas.Restore();

        canvas.Save();
        canvas.Translate(210, 64);
        canvas.Scale(0.95f, 1.25f);
        canvas.RotateDegrees(-12f);
        DrawCard(canvas, card, basePaint, outline);
        canvas.Restore();

        var customMatrix = SKMatrix.CreateScale(1.05f, 0.9f);
        customMatrix = customMatrix.PreConcat(SKMatrix.CreateRotationDegrees(14f, cardCenter.X, cardCenter.Y));
        customMatrix = customMatrix.PreConcat(SKMatrix.CreateTranslation(360f, 96f));

        canvas.Save();
        canvas.SetMatrix(customMatrix);
#if SKIA_SHIM
        _ = canvas.TotalMatrix;
        _ = canvas.TotalMatrix44; // touch matrix4x4 projection
#else
        _ = canvas.TotalMatrix;
#endif

        if (!canvas.QuickReject(card))
        {
            using var highlight = new SKPaint
            {
                Color = new SKColor(210, 230, 255),
                IsAntialias = true,
            };

            DrawCard(canvas, card, highlight, outline);
        }

        canvas.Restore();

        canvas.Save();
        canvas.Translate(64, 176);
        canvas.Scale(1.3f);
        canvas.ResetMatrix();
        canvas.Translate(520, 200);
        canvas.Scale(1.08f, 0.9f); // Cover multi-parameter scale
        DrawCard(canvas, card, basePaint, outline);
        canvas.Restore();
    }

    private static void DrawCard(SKCanvas canvas, SKRect rect, SKPaint fill, SKPaint outline)
    {
        canvas.DrawRoundRect(rect, 16, 16, fill);
        canvas.DrawRoundRect(rect, 16, 16, outline);
    }
}
