using System;

namespace VelloSharp.Windows;

public readonly struct WindowsSurfaceSize
{
    public WindowsSurfaceSize(uint width, uint height)
    {
        Width = width;
        Height = height;
    }

    public uint Width { get; }

    public uint Height { get; }

    public bool IsEmpty => Width == 0 || Height == 0;

    public static WindowsSurfaceSize Empty => new(0, 0);

    public override string ToString()
        => FormattableString.Invariant($"{Width}x{Height}");
}
