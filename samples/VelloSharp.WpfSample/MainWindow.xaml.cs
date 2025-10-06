using System;
using System.Numerics;
using System.Windows;
using VelloSharp;
using VelloSharp.Wpf.Integration;
using VelloPaintSurfaceEventArgs = VelloSharp.WinForms.Integration.VelloPaintSurfaceEventArgs;

namespace VelloSharp.WpfSample;

public partial class MainWindow : Window
{
    private readonly PathBuilder _unitSquare = new();
    private readonly PathBuilder _pointer = new();
    private readonly PathBuilder _ring = new();
    private readonly StrokeStyle _ringStroke = new()
    {
        Width = 16,
        LineJoin = LineJoin.Round,
        StartCap = LineCap.Round,
        EndCap = LineCap.Round,
    };

    private double _angle;
    private double _pulsePhase;
    private double _emaFps;
    private bool _updatingRenderMode;

    public MainWindow()
    {
        InitializeComponent();

        BuildUnitSquare();
        BuildPointerGeometry();
        BuildRingGeometry();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        VelloViewControl.PaintSurface += OnPaintSurface;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        VelloViewControl.RequestRender();
        UpdateRenderModeUi();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        VelloViewControl.PaintSurface -= OnPaintSurface;
    }

    private void OnPaintSurface(object? sender, VelloPaintSurfaceEventArgs e)
    {
        var width = e.Session.Width;
        var height = e.Session.Height;
        if (width == 0 || height == 0)
        {
            return;
        }

        if (e.IsAnimationFrame && e.Delta > TimeSpan.Zero)
        {
            const double rotationSpeed = Math.PI * 0.65;
            _angle = (_angle + rotationSpeed * e.Delta.TotalSeconds) % (Math.PI * 2.0);
            _pulsePhase += e.Delta.TotalSeconds * 0.5;
        }

        var scene = e.Session.Scene;
        var pixelWidth = (float)width;
        var pixelHeight = (float)height;
        var center = new Vector2(pixelWidth / 2f, pixelHeight / 2f);
        var radius = MathF.Min(pixelWidth, pixelHeight) * 0.32f;

        var backgroundTransform = Matrix3x2.CreateScale(pixelWidth, pixelHeight);
        scene.FillPath(_unitSquare, FillRule.NonZero, backgroundTransform, RgbaColor.FromBytes(0x12, 0x16, 0x1C));

        var pointerTransform = Matrix3x2.CreateScale(radius) *
                               Matrix3x2.CreateRotation((float)_angle) *
                               Matrix3x2.CreateTranslation(center);
        scene.FillPath(_pointer, FillRule.NonZero, pointerTransform, RgbaColor.FromBytes(0x3A, 0xB8, 0xFF));

        var counterTransform = Matrix3x2.CreateScale(radius * 0.6f) *
                               Matrix3x2.CreateRotation((float)(_angle + Math.PI)) *
                               Matrix3x2.CreateTranslation(center);
        scene.FillPath(_pointer, FillRule.NonZero, counterTransform, RgbaColor.FromBytes(0xF4, 0x5E, 0x8C));

        var pulse = 0.65 + (Math.Sin(_pulsePhase * 2 * Math.PI) * 0.35);
        _ringStroke.Width = 12 + (pulse * 6);
        var ringTransform = Matrix3x2.CreateScale(radius * 1.35f) * Matrix3x2.CreateTranslation(center);
        scene.StrokePath(_ring, _ringStroke, ringTransform, RgbaColor.FromBytes(0x81, 0xFF, 0xF9));

        UpdateMetrics(e);
    }

    private void OnContinuousToggleChecked(object sender, RoutedEventArgs e)
    {
        if (_updatingRenderMode)
        {
            return;
        }

        VelloViewControl.RenderMode = VelloRenderMode.Continuous;
        VelloViewControl.RequestRender();
        UpdateRenderModeUi();
    }

    private void OnContinuousToggleUnchecked(object sender, RoutedEventArgs e)
    {
        if (_updatingRenderMode)
        {
            return;
        }

        VelloViewControl.RenderMode = VelloRenderMode.OnDemand;
        UpdateRenderModeUi();
    }

    private void OnRenderOnceClick(object sender, RoutedEventArgs e)
    {
        VelloViewControl.RequestRender();
    }

    private void UpdateMetrics(VelloPaintSurfaceEventArgs e)
    {
        if (e.IsAnimationFrame && e.Delta > TimeSpan.Zero)
        {
            var sample = 1.0 / e.Delta.TotalSeconds;
            if (double.IsFinite(sample))
            {
                _emaFps = _emaFps <= 0 ? sample : (_emaFps * 0.85) + (sample * 0.15);
            }
        }

        FpsText.Text = _emaFps > 0 ? $"FPS: {_emaFps:0.0}" : "FPS: --";
        FrameInfoText.Text = $"Frame {e.FrameId}    dt {e.Delta.TotalMilliseconds:0.0} ms    T {e.Timestamp.TotalSeconds:0.00}s";
        SurfaceInfoText.Text = $"Surface: {e.Session.Width} x {e.Session.Height}";
        UpdateRenderModeUi();
    }

    private void UpdateRenderModeUi()
    {
        if (_updatingRenderMode || ContinuousToggle is null || RenderOnceButton is null || ModeText is null)
        {
            return;
        }

        _updatingRenderMode = true;
        try
        {
            var mode = VelloViewControl.RenderMode;
            var isContinuous = mode == VelloRenderMode.Continuous;
            if (ContinuousToggle.IsChecked != isContinuous)
            {
                ContinuousToggle.IsChecked = isContinuous;
            }

            RenderOnceButton.IsEnabled = !isContinuous;
            ModeText.Text = $"Mode: {mode} | Driver: {VelloViewControl.RenderLoopDriver}";
        }
        finally
        {
            _updatingRenderMode = false;
        }
    }

    private void BuildUnitSquare()
    {
        _unitSquare.Clear();
        _unitSquare.MoveTo(0, 0)
                    .LineTo(1, 0)
                    .LineTo(1, 1)
                    .LineTo(0, 1)
                    .Close();
    }

    private void BuildPointerGeometry()
    {
        _pointer.Clear();
        _pointer.MoveTo(0, -1)
                .LineTo(0.18, 0.36)
                .LineTo(0, 0.12)
                .LineTo(-0.18, 0.36)
                .Close();
    }

    private void BuildRingGeometry()
    {
        const int segments = 64;
        _ring.Clear();
        for (var i = 0; i < segments; i++)
        {
            var angle = (float)(i * (2 * Math.PI) / segments);
            var x = MathF.Cos(angle);
            var y = MathF.Sin(angle);
            if (i == 0)
            {
                _ring.MoveTo(x, y);
            }
            else
            {
                _ring.LineTo(x, y);
            }
        }

        _ring.Close();
    }
}
