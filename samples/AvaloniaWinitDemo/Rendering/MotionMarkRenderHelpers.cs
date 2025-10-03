using System;
using System.Numerics;
using Avalonia;
using Avalonia.Media;
using AvaloniaWinitDemo.Scenes;
using VelloSharp;

namespace AvaloniaWinitDemo.Rendering;

internal static class MotionMarkRenderHelpers
{
    public static Matrix3x2 CreateSceneTransform(Rect bounds)
    {
        var width = (float)bounds.Width;
        var height = (float)bounds.Height;
        var scale = MathF.Min(
            width / MotionMarkScene.CanvasWidth,
            height / MotionMarkScene.CanvasHeight);

        if (!float.IsFinite(scale) || scale <= 0f)
        {
            scale = 1f;
        }

        var scaledWidth = MotionMarkScene.CanvasWidth * scale;
        var scaledHeight = MotionMarkScene.CanvasHeight * scale;
        var offsetX = (width - scaledWidth) * 0.5f + (float)bounds.X;
        var offsetY = (height - scaledHeight) * 0.5f + (float)bounds.Y;

        var transform = Matrix3x2.CreateScale(scale);
        transform.Translation = new Vector2(offsetX, offsetY);
        return transform;
    }

    public static Matrix ToAvaloniaMatrix(Matrix3x2 matrix) => new(
        matrix.M11,
        matrix.M12,
        matrix.M21,
        matrix.M22,
        matrix.M31,
        matrix.M32);

    public static Color ToAvaloniaColor(RgbaColor color)
    {
        static byte ToByte(float value)
        {
            var scaled = (int)MathF.Round(value * 255f);
            return (byte)Math.Clamp(scaled, 0, 255);
        }

        return Color.FromArgb(
            ToByte(color.A),
            ToByte(color.R),
            ToByte(color.G),
            ToByte(color.B));
    }
}
