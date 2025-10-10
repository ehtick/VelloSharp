using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Baseline;

internal sealed class TextHintingBaselineScene : ISkiaGalleryScene
{
    private const string Sample = "The quick brown fox jumps over the lazy dog";

    public string Title => "Baseline: Text Rendering";

    public string Description => "Draws repeated strings with transforms to observe hinting, glyph placement, and per-sample differences.";

    public SkiaSceneFeature Feature => SkiaSceneFeature.Text;

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(12, 15, 20));

        using var caption = new SKPaint
        {
            Color = new SKColor(119, 125, 135),
            TextSize = 20,
        };
        using var text = new SKPaint
        {
            Color = new SKColor(236, 239, 244),
            IsAntialias = true,
            TextSize = 28,
        };

        canvas.DrawText("Default hinting:", 60, 90, caption);
        canvas.DrawText(Sample, 60, 130, text);

        canvas.DrawText("Scaled transform:", 60, 210, caption);
        canvas.Save();
        canvas.Scale(1.4f, 1.4f);
        canvas.DrawText(Sample, 42, 180, text);
        canvas.Restore();

        canvas.DrawText("Vertical offset test:", 60, 320, caption);
        using var offsetText = new SKPaint
        {
            Color = new SKColor(214, 222, 255),
            IsAntialias = true,
            TextSize = 28,
        };
        for (var i = 0; i < 4; i++)
        {
            var y = 360 + i * 32;
            canvas.DrawText(Sample, 60 + i * 6, y, offsetText);
        }
    }
}
