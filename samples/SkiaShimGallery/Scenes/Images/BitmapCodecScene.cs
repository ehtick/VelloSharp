using System.IO;
using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Images;

internal sealed class BitmapCodecScene : ISkiaGalleryScene
{
    public string Title => "Bitmap: Codec & Resize";

    public string Description => "Decodes via SKCodec, resizes with sampling options, and snapshots the bitmap.";

    public SkiaSceneFeature Feature => SkiaSceneFeature.ImageCodecs;

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(244, 244, 249));

        var bytes = SceneResources.GetLogoBytes();
        using var dataStream = new MemoryStream(bytes, writable: false);
        using var data = SKData.Create(dataStream);
#if SKIA_SHIM
        using var codec = SKCodec.Create(data);
        var scaledInfo = codec.GetScaledDimensions(1.5f);
        using var decoded = SKBitmap.Decode(codec, scaledInfo);
        var sampling = new SKSamplingOptions(useHighQuality: true);
        using var resized = decoded.Resize(new SKImageInfo(128, 128, SKColorType.Rgba8888, SKAlphaType.Premul), sampling);
        using var copy = resized.Copy();
        var encodedSize = codec.ToEncodedBytes().Length;
        using var image = SKImage.FromBitmap(copy)!;
#else
        using var codec = SKCodec.Create(data);
        var scaledSize = codec.GetScaledDimensions(1.5f);
        using var decoded = SKBitmap.Decode(data);
        using var resized = decoded?.Resize(new SKImageInfo(128, 128, SKColorType.Rgba8888, SKAlphaType.Premul), SKFilterQuality.High);
        using var copy = resized?.Copy() ?? decoded?.Copy();
        var encodedSize = bytes.Length;
        using var image = SKImage.FromBitmap(copy ?? decoded)!;
#endif
        canvas.DrawImage(image, SKRect.Create(40, 40, 160, 160));

        using var labelPaint = new SKPaint
        {
            Color = new SKColor(90, 90, 120),
            TextSize = 16,
            IsAntialias = true,
        };
        canvas.DrawText($"Encoded bytes: {encodedSize}", 40, 220, labelPaint);
    }
}
