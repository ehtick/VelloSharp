using System.Collections.Generic;
using SkiaGallery.SharedScenes.Canvas;
using SkiaGallery.SharedScenes.Images;
using SkiaGallery.SharedScenes.Paint;
using SkiaGallery.SharedScenes.Resources;
using SkiaGallery.SharedScenes.Shaders;
using SkiaGallery.SharedScenes.Text;

namespace SkiaGallery.SharedScenes;

public static class SkiaGallerySceneRegistry
{
    public static IReadOnlyList<ISkiaGalleryScene> All { get; } = new ISkiaGalleryScene[]
    {
        new CanvasBasicsScene(),
        new CanvasTransformsScene(),
        new CanvasClipAndLayerScene(),
        new CanvasPathScene(),
        new CanvasRoundAndOvalScene(),
        new CanvasPictureScene(),

        new PaintStrokeScene(),
        new PaintStrokeAndFillScene(),

        new LinearGradientScene(),
        new RadialGradientScene(),
        new TwoPointConicalGradientScene(),
        new SweepGradientScene(),
        new ComposeShaderScene(),
        new PictureShaderScene(),

        new TextSimpleScene(),
        new TextBlobScene(),

        new ImageDecodeScene(),
        new BitmapInstallPixelsScene(),
        new BitmapCodecScene(),
        new ImagePixelsScene(),

        new SurfaceSnapshotScene(),
        new DrawableScene(),
    };
}
