using System.Numerics;
using VelloSharp;

namespace SkiaSharp;

public readonly struct SKPoint(float x, float y)
{
    public float X { get; } = x;
    public float Y { get; } = y;

    internal Vector2 ToVector2() => new(X, Y);

    public static implicit operator Vector2(SKPoint point) => point.ToVector2();
}

public readonly struct SKRect : IEquatable<SKRect>
{
    public SKRect(float left, float top, float right, float bottom)
    {
        if (right < left)
        {
            throw new ArgumentException("Right must be greater than or equal to left.", nameof(right));
        }

        if (bottom < top)
        {
            throw new ArgumentException("Bottom must be greater than or equal to top.", nameof(bottom));
        }

        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public float Left { get; }
    public float Top { get; }
    public float Right { get; }
    public float Bottom { get; }

    public float Width => Right - Left;
    public float Height => Bottom - Top;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static SKRect Create(float left, float top, float width, float height) =>
        new(left, top, left + width, top + height);

    public bool Contains(SKPoint point) =>
        point.X >= Left &&
        point.X <= Right &&
        point.Y >= Top &&
        point.Y <= Bottom;

    public bool Equals(SKRect other) =>
        Left.Equals(other.Left) &&
        Top.Equals(other.Top) &&
        Right.Equals(other.Right) &&
        Bottom.Equals(other.Bottom);

    public override bool Equals(object? obj) => obj is SKRect rect && Equals(rect);

    public override int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);

    public static bool operator ==(SKRect left, SKRect right) => left.Equals(right);

    public static bool operator !=(SKRect left, SKRect right) => !left.Equals(right);

    internal PathBuilder ToPathBuilder()
    {
        var builder = new PathBuilder();
        builder.MoveTo(Left, Top)
               .LineTo(Right, Top)
               .LineTo(Right, Bottom)
               .LineTo(Left, Bottom)
               .Close();
        return builder;
    }
}
