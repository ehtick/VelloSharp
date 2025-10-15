using System.IO;
using System.Runtime.InteropServices;
using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Images;

internal sealed class ImagePixelsScene : ISkiaGalleryScene
{
    public string Title => "Images: Pixels & Sampling";

    public string Description => "Scales via SKPixmap, reads pixels into a managed buffer, and recreates an image from raw data.";

    public SkiaSceneFeature Feature => SkiaSceneFeature.ImageCodecs;

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(248, 250, 254));

        var bytes = SceneResources.GetLogoBytes();
        using var dataStream = new MemoryStream(bytes, writable: false);
        using var data = SKData.Create(dataStream);
        using var image = SKImage.FromEncodedData(data)!;

        var scaledInfo = new SKImageInfo(96, 96, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var scaledPixels = new byte[scaledInfo.RowBytes * scaledInfo.Height];
#if SKIA_SHIM
        var pixmap = new SKPixmap(scaledInfo, scaledPixels, scaledInfo.RowBytes);
        image.ScalePixels(pixmap, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
#else
        var scaledHandle = GCHandle.Alloc(scaledPixels, GCHandleType.Pinned);
        try
        {
            using var pix = new SKPixmap(scaledInfo, scaledHandle.AddrOfPinnedObject(), scaledInfo.RowBytes);
            image.ScalePixels(pix, SKFilterQuality.High);
        }
        finally
        {
            scaledHandle.Free();
        }
#endif

        var readInfo = new SKImageInfo(96, 96, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var readPixels = new byte[readInfo.RowBytes * readInfo.Height];
        var handle = GCHandle.Alloc(readPixels, GCHandleType.Pinned);
        SKImage recreated;
        try
        {
            var pointer = handle.AddrOfPinnedObject();
            image.ReadPixels(readInfo, pointer, readInfo.RowBytes, 0, 0, SKImageCachingHint.Allow);
#if SKIA_SHIM
            recreated = SKImage.FromPixels(readInfo, readPixels, readInfo.RowBytes);
#else
            recreated = SKImage.FromPixels(readInfo, pointer, readInfo.RowBytes);
#endif
        }
        finally
        {
            handle.Free();
        }

        using var recreatedImage = recreated;

        canvas.DrawImage(recreatedImage, SKRect.Create(40, 40, 160, 160));
        canvas.DrawImage(image, SKRect.Create(220, 40, 120, 120));
    }

}
