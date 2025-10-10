using System;
using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Baseline;

internal sealed class BlendModeBaselineScene : ISkiaGalleryScene
{
    private static readonly BlendSwatch[] s_swatches =
    {
        new(new SKColor(236, 112, 99), SKBlendMode.Multiply),
        new(new SKColor(155, 89, 182), SKBlendMode.Screen),
        new(new SKColor(41, 128, 185), SKBlendMode.Overlay),
        new(new SKColor(46, 204, 113), SKBlendMode.Darken),
        new(new SKColor(241, 196, 15), SKBlendMode.Lighten),
    };

    public string Title => "Baseline: Blend Modes";

    public string Description => "Overdraws translucent swatches using multiply, screen, overlay, darken, and lighten to highlight unsupported blend paths.";

    public SkiaSceneFeature Feature => SkiaSceneFeature.BlendModes;

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(24, 26, 33));

        using var basePaint = new SKPaint { Color = new SKColor(52, 152, 219, 200) };
        var inset = 60f;
        canvas.DrawRect(new SKRect(inset, inset, info.Width - inset, info.Height - inset), basePaint);

        var columnWidth = (info.Width - 160f) / s_swatches.Length;
        for (var i = 0; i < s_swatches.Length; i++)
        {
            var swatch = s_swatches[i];
            using var paint = new SKPaint
            {
                Color = swatch.Color,
                BlendMode = swatch.Mode,
                Style = SKPaintStyle.Fill,
            };

            var left = 80 + columnWidth * i;
            var rect = SKRect.Create(left, 100, columnWidth - 20, columnWidth - 20);
            canvas.DrawRoundRect(rect, 24, 24, paint);
        }

        using var caption = new SKPaint
        {
            Color = new SKColor(220, 222, 228),
            TextSize = 20,
        };

        for (var i = 0; i < s_swatches.Length; i++)
        {
            var label = s_swatches[i].Mode.ToString();
            var left = 80 + columnWidth * i + 4;
            canvas.DrawText(label, left, info.Height - 80, caption);
        }
    }

    private readonly record struct BlendSwatch(SKColor Color, SKBlendMode Mode);
}
