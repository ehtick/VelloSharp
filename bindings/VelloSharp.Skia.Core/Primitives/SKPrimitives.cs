using System;
using System.Numerics;

namespace SkiaSharp;

public readonly struct SKColorF
{
    public float Red { get; }
    public float Green { get; }
    public float Blue { get; }
    public float Alpha { get; }

    public SKColorF(float red, float green, float blue, float alpha = 1f)
    {
        Red = red;
        Green = green;
        Blue = blue;
        Alpha = alpha;
    }

    public SKColor ToColor()
    {
        static byte Clamp(float value)
        {
            if (value <= 0) return 0;
            if (value >= 1) return 255;
            return (byte)(value * 255f + 0.5f);
        }

        return new SKColor(Clamp(Red), Clamp(Green), Clamp(Blue), Clamp(Alpha));
    }

    public static implicit operator SKColorF(SKColor color) =>
        new(
            color.Red / 255f,
            color.Green / 255f,
            color.Blue / 255f,
            color.Alpha / 255f);

    public static implicit operator SKColor(SKColorF color) => color.ToColor();
}

public readonly struct SKSizeI
{
    public int Width { get; }
    public int Height { get; }

    public SKSizeI(int width, int height)
    {
        Width = width;
        Height = height;
    }
}

public struct SKRectI
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public SKRectI(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public static SKRectI Create(int x, int y, int width, int height)
        => new(x, y, x + width, y + height);
}

public struct SKMatrix
{
    public float ScaleX;
    public float SkewX;
    public float TransX;
    public float SkewY;
    public float ScaleY;
    public float TransY;
    public float Persp0;
    public float Persp1;
    public float Persp2;

    public static SKMatrix CreateIdentity() => new()
    {
        ScaleX = 1,
        ScaleY = 1,
        Persp2 = 1,
    };

    public static SKMatrix CreateTranslation(float dx, float dy)
    {
        var matrix = CreateIdentity();
        matrix.TransX = dx;
        matrix.TransY = dy;
        return matrix;
    }

    public static SKMatrix CreateScale(float sx, float sy)
    {
        var matrix = CreateIdentity();
        matrix.ScaleX = sx;
        matrix.ScaleY = sy;
        return matrix;
    }

    public static SKMatrix CreateRotationDegrees(float degrees, float px, float py)
    {
        var radians = degrees * (float)(Math.PI / 180.0);
        var sin = (float)Math.Sin(radians);
        var cos = (float)Math.Cos(radians);

        var translation = CreateTranslation(-px, -py);
        var rotation = new SKMatrix
        {
            ScaleX = cos,
            SkewX = -sin,
            TransX = 0,
            SkewY = sin,
            ScaleY = cos,
            TransY = 0,
            Persp0 = 0,
            Persp1 = 0,
            Persp2 = 1,
        };

        var back = CreateTranslation(px, py);
        return Concat(back, Concat(rotation, translation));
    }

    public static SKMatrix Concat(SKMatrix first, SKMatrix second)
    {
        return Multiply(first, second);
    }

    public static void Concat(ref SKMatrix target, SKMatrix first, SKMatrix second)
    {
        target = Multiply(first, second);
    }

    public SKMatrix PreConcat(SKMatrix matrix) => Concat(matrix, this);

    public Matrix3x2 ToMatrix3x2() => new(ScaleX, SkewY, SkewX, ScaleY, TransX, TransY);

    public static SKMatrix FromMatrix3x2(Matrix3x2 matrix) => new()
    {
        ScaleX = matrix.M11,
        SkewX = matrix.M21,
        TransX = matrix.M31,
        SkewY = matrix.M12,
        ScaleY = matrix.M22,
        TransY = matrix.M32,
        Persp0 = 0,
        Persp1 = 0,
        Persp2 = 1,
    };

    private static SKMatrix Multiply(in SKMatrix lhs, in SKMatrix rhs)
    {
        return new SKMatrix
        {
            ScaleX = lhs.ScaleX * rhs.ScaleX + lhs.SkewX * rhs.SkewY + lhs.TransX * rhs.Persp0,
            SkewX = lhs.ScaleX * rhs.SkewX + lhs.SkewX * rhs.ScaleY + lhs.TransX * rhs.Persp1,
            TransX = lhs.ScaleX * rhs.TransX + lhs.SkewX * rhs.TransY + lhs.TransX * rhs.Persp2,
            SkewY = lhs.SkewY * rhs.ScaleX + lhs.ScaleY * rhs.SkewY + lhs.TransY * rhs.Persp0,
            ScaleY = lhs.SkewY * rhs.SkewX + lhs.ScaleY * rhs.ScaleY + lhs.TransY * rhs.Persp1,
            TransY = lhs.SkewY * rhs.TransX + lhs.ScaleY * rhs.TransY + lhs.TransY * rhs.Persp2,
            Persp0 = lhs.Persp0 * rhs.ScaleX + lhs.Persp1 * rhs.SkewY + lhs.Persp2 * rhs.Persp0,
            Persp1 = lhs.Persp0 * rhs.SkewX + lhs.Persp1 * rhs.ScaleY + lhs.Persp2 * rhs.Persp1,
            Persp2 = lhs.Persp0 * rhs.TransX + lhs.Persp1 * rhs.TransY + lhs.Persp2 * rhs.Persp2,
        };
    }
}

public struct SKMatrix44
{
    public float M00, M01, M02, M03;
    public float M10, M11, M12, M13;
    public float M20, M21, M22, M23;
    public float M30, M31, M32, M33;

    public static SKMatrix44 CreateIdentity() => new()
    {
        M00 = 1,
        M11 = 1,
        M22 = 1,
        M33 = 1,
    };

    public static SKMatrix44 FromMatrix3x2(Matrix3x2 matrix) => new()
    {
        M00 = matrix.M11,
        M01 = matrix.M12,
        M02 = 0,
        M03 = matrix.M31,
        M10 = matrix.M21,
        M11 = matrix.M22,
        M12 = 0,
        M13 = matrix.M32,
        M20 = 0,
        M21 = 0,
        M22 = 1,
        M23 = 0,
        M30 = 0,
        M31 = 0,
        M32 = 0,
        M33 = 1,
    };

    public Matrix3x2 ToMatrix3x2() => new(
        M00, M10,
        M01, M11,
        M03, M13);
}

public struct SKMatrix4x4
{
    public float M00, M01, M02, M03;
    public float M10, M11, M12, M13;
    public float M20, M21, M22, M23;
    public float M30, M31, M32, M33;

    public static SKMatrix4x4 Identity => new()
    {
        M00 = 1,
        M11 = 1,
        M22 = 1,
        M33 = 1,
    };
}
