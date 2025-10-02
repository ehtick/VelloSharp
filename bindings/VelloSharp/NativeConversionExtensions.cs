using System.Numerics;

namespace VelloSharp;

internal static class NativeConversionExtensions
{
    public static VelloColor ToNative(this RgbaColor color) => new()
    {
        R = color.R,
        G = color.G,
        B = color.B,
        A = color.A,
    };

    public static VelloPoint ToNativePoint(this Vector2 point) => new()
    {
        X = point.X,
        Y = point.Y,
    };

    public static VelloAffine ToNativeAffine(this Matrix3x2 matrix) => new()
    {
        M11 = matrix.M11,
        M12 = matrix.M12,
        M21 = matrix.M21,
        M22 = matrix.M22,
        Dx = matrix.M31,
        Dy = matrix.M32,
    };

    public static VelloGradientStop ToNative(this GradientStop stop) => new()
    {
        Offset = stop.Offset,
        Color = stop.Color.ToNative(),
    };
}
