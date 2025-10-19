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

    public static SKMatrix44 CreateTranslation(float x, float y, float z)
    {
        var matrix = CreateIdentity();
        matrix.M03 = x;
        matrix.M13 = y;
        matrix.M23 = z;
        return matrix;
    }

    public static SKMatrix44 CreateScale(float x, float y, float z)
    {
        var matrix = CreateIdentity();
        matrix.M00 = x;
        matrix.M11 = y;
        matrix.M22 = z;
        return matrix;
    }

    public static SKMatrix44 CreateScale(float x, float y, float z, float pivotX, float pivotY, float pivotZ)
    {
        var translationToOrigin = CreateTranslation(-pivotX, -pivotY, -pivotZ);
        var scale = CreateScale(x, y, z);
        var translationBack = CreateTranslation(pivotX, pivotY, pivotZ);
        return translationBack * scale * translationToOrigin;
    }

    public static SKMatrix44 CreateRotation(float x, float y, float z, float radians)
    {
        var axis = new Vector3(x, y, z);
        if (axis == Vector3.Zero)
        {
            return CreateIdentity();
        }

        axis = Vector3.Normalize(axis);
        var matrix = Matrix4x4.CreateFromAxisAngle(axis, radians);
        return FromMatrix4x4(matrix);
    }

    public static SKMatrix44 CreateRotationDegrees(float x, float y, float z, float degrees) =>
        CreateRotation(x, y, z, degrees * (float)(Math.PI / 180.0));

    public static SKMatrix44 FromMatrix3x2(Matrix3x2 matrix)
    {
        var result = CreateIdentity();
        result.M00 = matrix.M11;
        result.M01 = matrix.M12;
        result.M03 = matrix.M31;
        result.M10 = matrix.M21;
        result.M11 = matrix.M22;
        result.M13 = matrix.M32;
        return result;
    }

    public static SKMatrix44 FromMatrix4x4(Matrix4x4 matrix) => new()
    {
        M00 = matrix.M11,
        M01 = matrix.M12,
        M02 = matrix.M13,
        M03 = matrix.M14,
        M10 = matrix.M21,
        M11 = matrix.M22,
        M12 = matrix.M23,
        M13 = matrix.M24,
        M20 = matrix.M31,
        M21 = matrix.M32,
        M22 = matrix.M33,
        M23 = matrix.M34,
        M30 = matrix.M41,
        M31 = matrix.M42,
        M32 = matrix.M43,
        M33 = matrix.M44,
    };

    public Matrix3x2 ToMatrix3x2() => new(
        M00, M10,
        M01, M11,
        M03, M13);

    public Matrix4x4 ToMatrix4x4() => new(
        M00, M01, M02, M03,
        M10, M11, M12, M13,
        M20, M21, M22, M23,
        M30, M31, M32, M33);

    public void ToRowMajor(Span<float> destination)
    {
        if (destination.Length != 16)
        {
            throw new ArgumentException("Destination span must contain 16 elements.", nameof(destination));
        }

        destination[00] = M00;
        destination[01] = M01;
        destination[02] = M02;
        destination[03] = M03;
        destination[04] = M10;
        destination[05] = M11;
        destination[06] = M12;
        destination[07] = M13;
        destination[08] = M20;
        destination[09] = M21;
        destination[10] = M22;
        destination[11] = M23;
        destination[12] = M30;
        destination[13] = M31;
        destination[14] = M32;
        destination[15] = M33;
    }

    public void ToColumnMajor(Span<float> destination)
    {
        if (destination.Length != 16)
        {
            throw new ArgumentException("Destination span must contain 16 elements.", nameof(destination));
        }

        destination[00] = M00;
        destination[01] = M10;
        destination[02] = M20;
        destination[03] = M30;
        destination[04] = M01;
        destination[05] = M11;
        destination[06] = M21;
        destination[07] = M31;
        destination[08] = M02;
        destination[09] = M12;
        destination[10] = M22;
        destination[11] = M32;
        destination[12] = M03;
        destination[13] = M13;
        destination[14] = M23;
        destination[15] = M33;
    }

    public static SKMatrix44 FromRowMajor(ReadOnlySpan<float> source)
    {
        if (source.Length != 16)
        {
            throw new ArgumentException("Source span must contain 16 elements.", nameof(source));
        }

        return new SKMatrix44
        {
            M00 = source[0],
            M01 = source[1],
            M02 = source[2],
            M03 = source[3],
            M10 = source[4],
            M11 = source[5],
            M12 = source[6],
            M13 = source[7],
            M20 = source[8],
            M21 = source[9],
            M22 = source[10],
            M23 = source[11],
            M30 = source[12],
            M31 = source[13],
            M32 = source[14],
            M33 = source[15],
        };
    }

    public static SKMatrix44 FromColumnMajor(ReadOnlySpan<float> source)
    {
        if (source.Length != 16)
        {
            throw new ArgumentException("Source span must contain 16 elements.", nameof(source));
        }

        return new SKMatrix44
        {
            M00 = source[0],
            M01 = source[4],
            M02 = source[8],
            M03 = source[12],
            M10 = source[1],
            M11 = source[5],
            M12 = source[9],
            M13 = source[13],
            M20 = source[2],
            M21 = source[6],
            M22 = source[10],
            M23 = source[14],
            M30 = source[3],
            M31 = source[7],
            M32 = source[11],
            M33 = source[15],
        };
    }

    public static SKMatrix44 operator *(SKMatrix44 lhs, SKMatrix44 rhs)
    {
        var left = lhs.ToMatrix4x4();
        var right = rhs.ToMatrix4x4();
        return FromMatrix4x4(Matrix4x4.Multiply(left, right));
    }
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
