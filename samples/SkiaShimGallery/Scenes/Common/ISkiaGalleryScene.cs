using SkiaSharp;

namespace SkiaGallery.SharedScenes;

public interface ISkiaGalleryScene
{
    string Title { get; }
    string Description { get; }
    SkiaSceneFeature Feature => SkiaSceneFeature.CoreCanvas;
    void Render(SKCanvas canvas, SKImageInfo info);
}
