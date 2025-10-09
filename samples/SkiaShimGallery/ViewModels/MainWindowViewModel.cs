using System.Collections.Generic;
using SkiaGallery.SharedScenes;

namespace SkiaShimGallery.ViewModels;

public sealed class MainWindowViewModel
{
    public IReadOnlyList<ISkiaGalleryScene> Scenes { get; } = SkiaGallerySceneRegistry.All;
}
