using System.Numerics;
using Avalonia;

namespace VelloSharp.Avalonia.Controls;

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
}

