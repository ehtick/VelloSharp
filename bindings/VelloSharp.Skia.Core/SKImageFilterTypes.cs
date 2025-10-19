namespace SkiaSharp;

public enum SKColorChannel
{
    R,
    G,
    B,
    A,
}

public enum SKDropShadowImageFilterShadowMode
{
    DrawShadowAndForeground,
    DrawShadowOnly,
}

public enum SKImageFilterMorphologyType
{
    Dilate,
    Erode,
}

public sealed class SKColorTable
{
    // TODO: Implement color table storage when the backend supports table filters.
}
