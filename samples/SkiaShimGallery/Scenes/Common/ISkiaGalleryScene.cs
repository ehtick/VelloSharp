using SkiaSharp;

namespace SkiaGallery.SharedScenes;

public interface ISkiaGalleryScene
{
    string Title { get; }
    string Description { get; }
    void Render(SKCanvas canvas, SKImageInfo info);
}
