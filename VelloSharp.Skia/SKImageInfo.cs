using System;

namespace SkiaSharp;

public enum SKColorType
{
    Bgra8888,
    Rgba8888,
}

public enum SKAlphaType
{
    Premul,
    Opaque,
}

public readonly struct SKImageInfo
{
    public SKImageInfo(int width, int height, SKColorType colorType = SKColorType.Bgra8888, SKAlphaType alphaType = SKAlphaType.Premul)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        Width = width;
        Height = height;
        ColorType = colorType;
        AlphaType = alphaType;
    }

    public int Width { get; }
    public int Height { get; }
    public SKColorType ColorType { get; }
    public SKAlphaType AlphaType { get; }
}
