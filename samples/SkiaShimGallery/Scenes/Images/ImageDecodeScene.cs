using System.IO;
using SkiaGallery.SharedScenes;
using SkiaSharp;

namespace SkiaGallery.SharedScenes.Images;

internal sealed class ImageDecodeScene : ISkiaGalleryScene
{
    public string Title => "Images: Decode & Draw";

    public string Description => "Decodes an embedded PNG via SKData and draws it using both DrawImage overloads.";

    public SkiaSceneFeature Feature => SkiaSceneFeature.ImageCodecs;

    public void Render(SKCanvas canvas, SKImageInfo info)
    {
        canvas.Clear(new SKColor(244, 246, 250));

        using var assetStream = SceneResources.OpenAssetStream("avalonia-32.png");
        using var memory = new MemoryStream();
        assetStream.CopyTo(memory);
        memory.Position = 0;

#if SKIA_SHIM
        using var managedStream = new SKManagedStream(memory, leaveOpen: false);
#else
        memory.Position = 0;
        using var managedStream = new SKManagedStream(memory);
#endif
        using var data = SKData.Create(managedStream);
        _ = data.AsSpan(); // touch span conversion
        using var image = SKImage.FromEncodedData(data)!;

        canvas.DrawImage(image, 40, 40);
        canvas.DrawImage(image, SKRect.Create(120, 36, 140, 140));
    }
}
