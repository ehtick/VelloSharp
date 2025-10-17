using System.Numerics;
using VelloSharp;

namespace SkiaSharp;

public struct SKPoint : IEquatable<SKPoint>
{
    public SKPoint(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float X { get; set; }
    public float Y { get; set; }

    internal readonly Vector2 ToVector2() => new(X, Y);

    public static implicit operator Vector2(SKPoint point) => point.ToVector2();

    public readonly bool Equals(SKPoint other) => X.Equals(other.X) && Y.Equals(other.Y);

    public override readonly bool Equals(object? obj) => obj is SKPoint point && Equals(point);

    public override readonly int GetHashCode() => HashCode.Combine(X, Y);

    public static bool operator ==(SKPoint left, SKPoint right) => left.Equals(right);

    public static bool operator !=(SKPoint left, SKPoint right) => !left.Equals(right);
}

public readonly struct SKPointI(int x, int y)
{
    public int X { get; } = x;
    public int Y { get; } = y;

    public SKPoint ToPoint() => new(X, Y);

    public override string ToString() => $"({X}, {Y})";
}

public readonly struct SKPoint3(float x, float y, float z)
{
    public float X { get; } = x;
    public float Y { get; } = y;
    public float Z { get; } = z;

    public static implicit operator Vector3(SKPoint3 point) => new(point.X, point.Y, point.Z);

    public static implicit operator SKPoint3(Vector3 vector) => new(vector.X, vector.Y, vector.Z);
}

public struct SKRect : IEquatable<SKRect>
{
    public SKRect(float left, float top, float right, float bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public float Left { get; set; }
    public float Top { get; set; }
    public float Right { get; set; }
    public float Bottom { get; set; }

    public readonly float Width => Right - Left;
    public readonly float Height => Bottom - Top;
    public readonly bool IsEmpty => Width <= 0 || Height <= 0;

    public static SKRect Create(float left, float top, float width, float height) =>
        new(left, top, left + width, top + height);

    public readonly bool Contains(SKPoint point) =>
        point.X >= Left &&
        point.X <= Right &&
        point.Y >= Top &&
        point.Y <= Bottom;

    public void Offset(float dx, float dy)
    {
        Left += dx;
        Right += dx;
        Top += dy;
        Bottom += dy;
    }

    public void Offset(SKPoint delta) => Offset(delta.X, delta.Y);

    public void Offset(SKPointI delta) => Offset(delta.X, delta.Y);

    public void Inflate(float dx, float dy)
    {
        Left -= dx;
        Top -= dy;
        Right += dx;
        Bottom += dy;
    }

    public void Inflate(SKPoint size) => Inflate(size.X, size.Y);

    public void Deflate(float dx, float dy) => Inflate(-dx, -dy);

    public void Deflate(SKPoint size) => Deflate(size.X, size.Y);

    public void Union(SKRect rect)
    {
        if (rect.IsEmpty)
        {
            return;
        }

        if (IsEmpty)
        {
            this = rect;
            return;
        }

        Left = Math.Min(Left, rect.Left);
        Top = Math.Min(Top, rect.Top);
        Right = Math.Max(Right, rect.Right);
        Bottom = Math.Max(Bottom, rect.Bottom);
    }

    public void Union(SKPoint point)
    {
        if (IsEmpty)
        {
            Left = Right = point.X;
            Top = Bottom = point.Y;
            return;
        }

        Left = Math.Min(Left, point.X);
        Top = Math.Min(Top, point.Y);
        Right = Math.Max(Right, point.X);
        Bottom = Math.Max(Bottom, point.Y);
    }

    public static SKRect Union(SKRect a, SKRect b)
    {
        a.Union(b);
        return a;
    }

    public readonly bool Equals(SKRect other) =>
        Left.Equals(other.Left) &&
        Top.Equals(other.Top) &&
        Right.Equals(other.Right) &&
        Bottom.Equals(other.Bottom);

    public override readonly bool Equals(object? obj) => obj is SKRect rect && Equals(rect);

    public override readonly int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);

    public static bool operator ==(SKRect left, SKRect right) => left.Equals(right);

    public static bool operator !=(SKRect left, SKRect right) => !left.Equals(right);

    internal readonly PathBuilder ToPathBuilder()
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
