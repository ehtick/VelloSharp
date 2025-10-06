using System;
using System.Numerics;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using MotionMark.SceneShared;
using VelloSharp;
using VelloSharp.Windows;
using VelloSharp.Wpf.Integration;
using VelloPaintSurfaceEventArgs = VelloSharp.WinForms.Integration.VelloPaintSurfaceEventArgs;

namespace VelloSharp.WpfSample;

public partial class MotionMarkPage : UserControl
{
    private MotionMarkScene _scene = new();
    private readonly PathBuilder _pathBuilder = new();
    private readonly StrokeStyle _strokeStyle = new()
    {
        LineJoin = LineJoin.Bevel,
        StartCap = LineCap.Round,
        EndCap = LineCap.Round,
        MiterLimit = 4.0,
    };

    private int _complexity = 6;
    private bool _animate = true;
    private int _lastElementTarget;
    private double _emaFps;

    public MotionMarkPage()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        MotionMarkView.RenderSurface += OnRenderSurface;
        MotionMarkView.PaintSurface += OnPaintSurfaceFallback;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        MotionMarkView.RequestRender();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        MotionMarkView.RenderSurface -= OnRenderSurface;
        MotionMarkView.PaintSurface -= OnPaintSurfaceFallback;
    }

    private void OnRenderSurface(object? sender, VelloSurfaceRenderEventArgs e)
    {
        if (!IsEnabled)
        {
            return;
        }

        RenderFrame(e.Scene, e.PixelSize, e.Timestamp, e.Delta, e.IsAnimationFrame);
        e.RenderScene(e.Scene, e.RenderParams);
    }

    private void OnPaintSurfaceFallback(object? sender, VelloPaintSurfaceEventArgs e)
    {
        if (MotionMarkView.PreferredBackend == VelloRenderBackend.Gpu)
        {
            return;
        }

        var pixelSize = new WindowsSurfaceSize(e.Session.Width, e.Session.Height);
        RenderFrame(e.Session.Scene, pixelSize, e.Timestamp, e.Delta, e.IsAnimationFrame);
    }

    private void RenderFrame(Scene scene, WindowsSurfaceSize pixelSize, TimeSpan timestamp, TimeSpan delta, bool isAnimationFrame)
    {
        scene.Reset();

        var target = _scene.PrepareFrame(_complexity);
        _lastElementTarget = target;
        UpdateFps(delta, isAnimationFrame);

        var width = (float)pixelSize.Width;
        var height = (float)pixelSize.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

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

        UpdateStatusText();

        if (!_animate)
        {
            MotionMarkView.RenderMode = VelloRenderMode.OnDemand;
        }
    }

    private void UpdateFps(TimeSpan delta, bool isAnimationFrame)
    {
        if (!isAnimationFrame || delta <= TimeSpan.Zero)
        {
            return;
        }

        var sample = 1.0 / delta.TotalSeconds;
        if (!double.IsFinite(sample))
        {
            return;
        }

        _emaFps = _emaFps <= 0 ? sample : (_emaFps * 0.9) + (sample * 0.1);
        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        if (StatusText is null)
        {
            return;
        }

        var fpsText = _emaFps > 0 ? $"{_emaFps:0.0}" : "--";
        StatusText.Text = $"Elements: {_lastElementTarget:N0}    FPS: {fpsText}";
    }

    private void OnComplexityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _complexity = (int)Math.Round(e.NewValue);
        if (ComplexityValue is not null)
        {
            ComplexityValue.Text = _complexity.ToString();
        }

        _scene = new MotionMarkScene();
        MotionMarkView.RequestRender();
    }

    private void OnAnimateToggled(object sender, RoutedEventArgs e)
    {
        _animate = AnimateToggle.IsChecked == true;
        MotionMarkView.RenderMode = _animate ? VelloRenderMode.Continuous : VelloRenderMode.OnDemand;
        if (!_animate)
        {
            MotionMarkView.RequestRender();
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

    private static RgbaColor ToRgba(System.Drawing.Color color)
        => RgbaColor.FromBytes(color.R, color.G, color.B, color.A);
}
