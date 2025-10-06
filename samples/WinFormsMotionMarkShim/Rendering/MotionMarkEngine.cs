using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using MotionMark.SceneShared;
using VelloSharp;
using VelloSharp.Windows;
using StrokeLineCap = VelloSharp.LineCap;
using StrokeLineJoin = VelloSharp.LineJoin;

namespace WinFormsMotionMarkShim.Rendering;

internal sealed class MotionMarkEngine
{
    private MotionMarkScene _scene = new();
    private readonly PathBuilder _pathBuilder = new();
    private readonly StrokeStyle _strokeStyle = new()
    {
        LineJoin = StrokeLineJoin.Bevel,
        StartCap = StrokeLineCap.Round,
        EndCap = StrokeLineCap.Round,
        MiterLimit = 4.0,
    };

    public int Complexity { get; set; } = 6;

    public int LastElementTarget { get; private set; }

    public MotionMarkScene Scene => _scene;

    public void ResetScene()
    {
        _scene = new MotionMarkScene();
        _pathBuilder.Clear();
        LastElementTarget = 0;
    }

    public int PrepareFrame()
    {
        var target = _scene.PrepareFrame(Complexity);
        LastElementTarget = target;
        return target;
    }

    public int PopulateScene(Scene scene, float width, float height)
    {
        var target = PrepareFrame();
        var transform = CreateMotionMarkTransform(width, height);
        var elements = _scene.Elements;

        if (elements.Length > 0)
        {
            var builder = _pathBuilder;
            builder.Clear();

            for (var i = 0; i < elements.Length; i++)
            {
                ref readonly var element = ref elements[i];
                if (builder.Count == 0)
                {
                    builder.MoveTo(element.Start.X, element.Start.Y);
                }

                switch (element.Type)
                {
                    case MotionMarkScene.ElementType.Line:
                        builder.LineTo(element.End.X, element.End.Y);
                        break;
                    case MotionMarkScene.ElementType.Quadratic:
                        builder.QuadraticTo(element.Control1.X, element.Control1.Y, element.End.X, element.End.Y);
                        break;
                    case MotionMarkScene.ElementType.Cubic:
                        builder.CubicTo(
                            element.Control1.X,
                            element.Control1.Y,
                            element.Control2.X,
                            element.Control2.Y,
                            element.End.X,
                            element.End.Y);
                        break;
                }

                var strokeBreak = element.IsSplit || i == elements.Length - 1;
                if (strokeBreak)
                {
                    _strokeStyle.Width = Math.Max(0.5, element.Width);
                    scene.StrokePath(builder, _strokeStyle, transform, ToRgba(element.Color));
                    builder.Clear();
                }
            }
        }

        return target;
    }

    public void RenderClassic(Graphics graphics, float width, float height)
    {
        var elements = _scene.Elements;
        if (elements.Length == 0)
        {
            return;
        }

        var transform = CreateMotionMarkTransform(width, height);

        var scale = Math.Min(width / MotionMarkScene.CanvasWidth, height / MotionMarkScene.CanvasHeight);
        if (!float.IsFinite(scale) || scale <= 0f)
        {
            scale = 1f;
        }

        foreach (ref readonly var element in elements)
        {
            using var pen = new Pen(element.Color, Math.Max(0.5f, element.Width) * scale)
            {
                LineJoin = System.Drawing.Drawing2D.LineJoin.Round,
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
            };

            var start = TransformPoint(transform, element.Start);
            var end = TransformPoint(transform, element.End);
            switch (element.Type)
            {
                case MotionMarkScene.ElementType.Line:
                    graphics.DrawLine(pen, start, end);
                    break;
                case MotionMarkScene.ElementType.Quadratic:
                {
                    var (control1, control2) = ConvertQuadraticToCubic(element.Start, element.Control1, element.End);
                    var c1 = TransformPoint(transform, control1);
                    var c2 = TransformPoint(transform, control2);
                    graphics.DrawBezier(pen, start, c1, c2, end);
                    break;
                }
                case MotionMarkScene.ElementType.Cubic:
                {
                    var control1 = TransformPoint(transform, element.Control1);
                    var control2 = TransformPoint(transform, element.Control2);
                    graphics.DrawBezier(pen, start, control1, control2, end);
                    break;
                }
            }
        }
    }

    private static Matrix3x2 CreateMotionMarkTransform(float width, float height)
    {
        if (width <= 0 || height <= 0)
        {
            return Matrix3x2.Identity;
        }

        var scale = Math.Min(width / MotionMarkScene.CanvasWidth, height / MotionMarkScene.CanvasHeight);
        if (scale <= 0f)
        {
            scale = 1f;
        }

        var scaledWidth = MotionMarkScene.CanvasWidth * scale;
        var scaledHeight = MotionMarkScene.CanvasHeight * scale;
        var translate = new Vector2((width - scaledWidth) * 0.5f, (height - scaledHeight) * 0.5f);

        return Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(translate);
    }

    private static (PointF C1, PointF C2) ConvertQuadraticToCubic(PointF start, PointF control, PointF end)
    {
        var c1 = new PointF(
            start.X + (2f / 3f) * (control.X - start.X),
            start.Y + (2f / 3f) * (control.Y - start.Y));

        var c2 = new PointF(
            end.X + (2f / 3f) * (control.X - end.X),
            end.Y + (2f / 3f) * (control.Y - end.Y));

        return (c1, c2);
    }

    private static PointF TransformPoint(Matrix3x2 matrix, PointF point)
    {
        var vector = Vector2.Transform(new Vector2(point.X, point.Y), matrix);
        return new PointF(vector.X, vector.Y);
    }

    private static RgbaColor ToRgba(Color color)
        => RgbaColor.FromBytes(color.R, color.G, color.B, color.A);
}
