using System;
using System.Numerics;
using VelloSharp;

namespace SkiaSharp;

internal readonly struct PaintBrush
{
    public PaintBrush(Brush brush, Matrix3x2? transform)
    {
        Brush = brush ?? throw new ArgumentNullException(nameof(brush));
        Transform = transform;
    }

    public Brush Brush { get; }
    public Matrix3x2? Transform { get; }

    public void Deconstruct(out Brush brush, out Matrix3x2? transform)
    {
        brush = Brush;
        transform = Transform;
    }
}
