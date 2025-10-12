using System;
using System.Numerics;
using System.Text;
using Microsoft.Maui.Controls;
using VelloSharp;
using VelloSharp.Maui.Events;
using VelloSharp.WinForms.Integration;
using PathBuilder = VelloSharp.PathBuilder;
using LineJoin = VelloSharp.LineJoin;
using LineCap = VelloSharp.LineCap;

namespace MauiVelloGallery;

public partial class MainPage : ContentPage
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
    private bool _hasIssuedInitialRender;
    private bool _sizeCheckScheduled;

    public MainPage()
    {
        InitializeComponent();
        Canvas.IsDiagnosticsEnabled = true;
        BuildGeometries();
        Canvas.Loaded += OnCanvasLoaded;
        Canvas.SizeChanged += OnCanvasSizeChanged;
    }

    private void BuildGeometries()
    {
        _unitSquare.Clear();
        _unitSquare
            .MoveTo(0, 0)
            .LineTo(1, 0)
            .LineTo(1, 1)
            .LineTo(0, 1)
            .Close();

        _pointer.Clear();
        _pointer
            .MoveTo(0, -1)
            .LineTo(0.12, 0.05)
            .LineTo(-0.12, 0.05)
            .Close();

        const int ringSegments = 64;
        const double radius = 1.0;
        _ring.Clear();
        _ring.MoveTo(radius, 0);
        for (var i = 1; i <= ringSegments; i++)
        {
            var theta = Math.PI * 2 * (i / (double)ringSegments);
            _ring.LineTo(Math.Cos(theta) * radius, Math.Sin(theta) * radius);
        }
        _ring.Close();
    }

    private void OnPaintSurface(object? sender, VelloPaintSurfaceEventArgs e)
    {
        if (e.Session.Width <= 0 || e.Session.Height <= 0)
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
        var pixelWidth = (float)e.Session.Width;
        var pixelHeight = (float)e.Session.Height;
        var center = new Vector2(pixelWidth / 2f, pixelHeight / 2f);
        var radius = MathF.Min(pixelWidth, pixelHeight) * 0.32f;

        scene.FillPath(
            _unitSquare,
            FillRule.NonZero,
            Matrix3x2.CreateScale(pixelWidth, pixelHeight),
            RgbaColor.FromBytes(0x10, 0x18, 0x21));

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

        UpdateDiagnostics(e);
    }

    private void UpdateDiagnostics(VelloPaintSurfaceEventArgs e)
    {
        if (e.IsAnimationFrame && e.Delta > TimeSpan.Zero)
        {
            var sample = 1.0 / e.Delta.TotalSeconds;
            if (double.IsFinite(sample))
            {
                _emaFps = _emaFps <= 0 ? sample : (_emaFps * 0.85) + (sample * 0.15);
            }
        }

        FrameLabel.Text = $"Frame {e.FrameId} – Δ {e.Delta.TotalMilliseconds:0.0}ms – {Canvas.PreferredBackend}";
        var builder = new StringBuilder();
        builder.AppendLine(_emaFps > 0 ? $"FPS ≈ {_emaFps:0.0}" : "FPS --");

        var extended = Canvas.Diagnostics.ExtendedProperties;
        if (extended is { Count: > 0 })
        {
            if (extended.TryGetValue("OpenGL.Vendor", out var glVendor) && !string.IsNullOrWhiteSpace(glVendor))
            {
                builder.AppendLine($"GL Vendor: {glVendor}");
            }

            if (extended.TryGetValue("OpenGL.Renderer", out var glRenderer) && !string.IsNullOrWhiteSpace(glRenderer))
            {
                builder.AppendLine($"GL Renderer: {glRenderer}");
            }

            if (extended.TryGetValue("Vulkan.IsAvailable", out var vulkanAvailable) && !string.IsNullOrWhiteSpace(vulkanAvailable))
            {
                if (extended.TryGetValue("Vulkan.InstanceVersion", out var vulkanVersion) && !string.IsNullOrWhiteSpace(vulkanVersion))
                {
                    builder.AppendLine($"Vulkan: {vulkanAvailable} (Instance {vulkanVersion})");
                }
                else
                {
                    builder.AppendLine($"Vulkan: {vulkanAvailable}");
                }
            }

            if (extended.TryGetValue("Device.Manufacturer", out var manufacturer) && !string.IsNullOrWhiteSpace(manufacturer))
            {
                var model = extended.TryGetValue("Device.Model", out var deviceModel) && !string.IsNullOrWhiteSpace(deviceModel)
                    ? deviceModel
                    : null;
                builder.AppendLine(model is null
                    ? $"Device: {manufacturer}"
                    : $"Device: {manufacturer} {model}");
            }
        }

        DiagnosticsLabel.Text = builder.ToString().TrimEnd();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Canvas.GpuUnavailable += OnGpuUnavailable;
        var frame = Canvas.Frame;
        RequestRenderIfSized(Canvas.Width, Canvas.Height, frame.Width, frame.Height);
    }

    private void OnGpuUnavailable(object? sender, string? message)
    {
        DiagnosticsLabel.Text = message is { Length: > 0 }
            ? $"GPU unavailable: {message}"
            : "GPU unavailable on this platform.";
    }

    protected override void OnDisappearing()
    {
        Canvas.GpuUnavailable -= OnGpuUnavailable;
        Canvas.Loaded -= OnCanvasLoaded;
        Canvas.SizeChanged -= OnCanvasSizeChanged;
        base.OnDisappearing();
    }

    private void OnCanvasLoaded(object? sender, EventArgs e)
    {
        var frame = Canvas.Frame;
        RequestRenderIfSized(Canvas.Width, Canvas.Height, frame.Width, frame.Height);
    }

    private void OnCanvasSizeChanged(object? sender, EventArgs e)
    {
        var frame = Canvas.Frame;
        RequestRenderIfSized(Canvas.Width, Canvas.Height, frame.Width, frame.Height);
    }

    private void RequestRenderIfSized(double width, double height, double actualWidth, double actualHeight)
    {
        if (_hasIssuedInitialRender)
        {
            return;
        }

        var platformWidth = -1.0;
        var platformHeight = -1.0;
#if WINDOWS
        if (Canvas.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement fe)
        {
            platformWidth = fe.ActualWidth;
            platformHeight = fe.ActualHeight;
        }
#endif

        var hasSize = (width > 0 && height > 0)
                      || (actualWidth > 0 && actualHeight > 0)
                      || (platformWidth > 0 && platformHeight > 0);
        if (!hasSize)
        {
            if (!_sizeCheckScheduled)
            {
                _sizeCheckScheduled = true;
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
                {
                    _sizeCheckScheduled = false;
                    var frame = Canvas.Frame;
                    RequestRenderIfSized(Canvas.Width, Canvas.Height, frame.Width, frame.Height);
                });
            }
            return;
        }

        _hasIssuedInitialRender = true;
        Canvas.RequestRender();
    }
}
