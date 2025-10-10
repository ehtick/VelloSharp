using System;
using System.Globalization;
using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Baseline;

internal sealed class ImageCodecBaselineScene : ISkiaGalleryScene
{
    public string Title => "Baseline: Image Codecs";

    public string Description => "Decodes the embedded Avalonia logo and renders layered thumbnails to compare sampling, scaling, and alpha handling.";

    public SkiaSceneFeature Feature => SkiaSceneFeature.ImageCodecs;

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(20, 20, 22));

        using var assetStream = SceneResources.OpenAssetStream("avalonia-32.png");
#if SKIA_SHIM
        using var managedStream = new SKManagedStream(assetStream, leaveOpen: false);
#else
        using var managedStream = new SKManagedStream(assetStream);
#endif
        using var data = SKData.Create(managedStream);
        using var image = SKImage.FromEncodedData(data) ?? throw new InvalidOperationException("Failed to decode codec baseline asset.");

        var decodedBytes = data.AsSpan().Length;

        var size = Math.Min(info.Width, info.Height) - 180;
        var dest = SKRect.Create(80, 80, size, size);
        canvas.DrawImage(image, dest);

        var overlayWidth = dest.Width * 0.6f;
        var overlayHeight = dest.Height * 0.6f;
        var overlay = SKRect.Create(dest.Right - overlayWidth, dest.Bottom - overlayHeight, overlayWidth, overlayHeight);
        canvas.DrawImage(image, overlay);

        using var caption = new SKPaint
        {
            Color = new SKColor(235, 236, 240),
            TextSize = 22,
        };
        canvas.DrawText($"Decoded bytes: {decodedBytes.ToString("N0", CultureInfo.InvariantCulture)}", 80, dest.Bottom + 44, caption);
        canvas.DrawText("Top-left: native asset", 80, dest.Bottom + 76, caption);
        canvas.DrawText("Bottom-right: scaled draw", 80, dest.Bottom + 108, caption);
    }
}


