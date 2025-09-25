using System;
using System.Numerics;
using VelloSharp;
using VelloSharp.Integration.Avalonia;
using FillRule = VelloSharp.FillRule;

namespace AvaloniaVelloDemo;

public sealed class VelloView : VelloSurfaceView
{
    private readonly PathBuilder _path = new();
    private readonly StrokeStyle _stroke = new()
    {
        Width = 1.5,
        LineJoin = LineJoin.Miter,
        StartCap = LineCap.Butt,
        EndCap = LineCap.Butt,
    };
    private float _time;

    public VelloView()
    {
        RenderParameters = RenderParameters with
        {
            BaseColor = RgbaColor.FromBytes(18, 18, 20),
            Antialiasing = AntialiasingMode.Area,
            Format = RenderFormat.Rgba8,
        };
    }

    protected override void OnRenderFrame(VelloRenderFrameContext context)
    {
        base.OnRenderFrame(context);

        _time += (float)context.DeltaTime.TotalSeconds;
        BuildScene(context.Scene, (int)context.Width, (int)context.Height, _time);
    }

    private void BuildScene(Scene scene, int width, int height, float time)
    {
        var transform = Matrix3x2.Identity;
        DrawGrid(scene, width, height, transform);
        DrawAxis(scene, width, height, transform);
        DrawRotor(scene, width, height, time);
    }

    private void DrawGrid(Scene scene, int width, int height, Matrix3x2 transform)
    {
        const int Step = 64;
        var color = RgbaColor.FromBytes(60, 60, 72, 255);
        for (var x = Step; x < width; x += Step)
        {
            _path.Clear();
            _path.MoveTo(x, 0).LineTo(x, height);
            scene.StrokePath(_path, _stroke, transform, color);
        }

        for (var y = Step; y < height; y += Step)
        {
            _path.Clear();
            _path.MoveTo(0, y).LineTo(width, y);
            scene.StrokePath(_path, _stroke, transform, color);
        }
    }

    private void DrawAxis(Scene scene, int width, int height, Matrix3x2 transform)
    {
        var center = new Vector2(width / 2f, height / 2f);
        var axisColor = RgbaColor.FromBytes(220, 85, 85);
        var axisStroke = new StrokeStyle
        {
            Width = 2.5,
            LineJoin = LineJoin.Miter,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };

        _path.Clear();
        _path.MoveTo(0, center.Y).LineTo(width, center.Y);
        scene.StrokePath(_path, axisStroke, transform, axisColor);

        axisColor = RgbaColor.FromBytes(95, 175, 240);
        _path.Clear();
        _path.MoveTo(center.X, 0).LineTo(center.X, height);
        scene.StrokePath(_path, axisStroke, transform, axisColor);
    }

    private void DrawRotor(Scene scene, int width, int height, float time)
    {
        var center = new Vector2(width / 2f, height / 2f);
        var radius = Math.Min(width, height) * 0.35f;
        var rotation = Matrix3x2.CreateRotation(time * 0.8f, center);

        _path.Clear();
        const int BladeCount = 5;
        for (var i = 0; i < BladeCount; i++)
        {
            var angle = (float)(i * Math.Tau / BladeCount);
            var tip = center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
            var ctrl = center + new Vector2((float)Math.Cos(angle + 0.3f), (float)Math.Sin(angle + 0.3f)) * (radius * 0.4f);

            _path.MoveTo(center.X, center.Y);
            _path.QuadraticTo(ctrl.X, ctrl.Y, tip.X, tip.Y);
            _path.Close();
        }

        var bladeColor = RgbaColor.FromBytes(156, 220, 255, 204);
        scene.FillPath(_path, FillRule.NonZero, rotation, bladeColor);

        var rimStroke = new StrokeStyle
        {
            Width = 4.5,
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };

        _path.Clear();
        const int Segments = 64;
        for (var i = 0; i <= Segments; i++)
        {
            var t = i / (float)Segments;
            var angle = t * MathF.Tau;
            var point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            if (i == 0)
            {
                _path.MoveTo(point.X, point.Y);
            }
            else
            {
                _path.LineTo(point.X, point.Y);
            }
        }
        _path.Close();

        scene.StrokePath(_path, rimStroke, Matrix3x2.Identity, RgbaColor.FromBytes(240, 240, 245, 230));
    }
}
