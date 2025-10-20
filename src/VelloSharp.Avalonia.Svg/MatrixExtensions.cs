using System.Numerics;
using Avalonia;

namespace VelloSharp.Avalonia.Svg;

internal static class MatrixExtensions
{
    public static Matrix3x2 ToMatrix3x2(this Matrix matrix)
    {
        return new Matrix3x2(
            (float)matrix.M11,
            (float)matrix.M12,
            (float)matrix.M21,
            (float)matrix.M22,
            (float)matrix.M31,
            (float)matrix.M32);
    }

    public static Matrix3x2 Multiply(this Matrix3x2 left, Matrix3x2 right)
    {
        return Matrix3x2.Multiply(left, right);
    }

    public static Matrix3x2 ToMatrix3x2(this Matrix3x2 matrix)
    {
        return matrix;
    }

    public static Matrix ToAvaloniaMatrix(this Matrix3x2 matrix)
    {
        return new Matrix(
            matrix.M11,
            matrix.M12,
            matrix.M21,
            matrix.M22,
            matrix.M31,
            matrix.M32);
    }
}
