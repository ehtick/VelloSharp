using System;
using System.Numerics;
using Microsoft.UI.Xaml;
using VelloSharp;
using VelloSharp.Windows.Shared.Contracts;
using VelloSharp.Windows.Shared.Presenters;
using VelloSharp.WinForms.Integration;

namespace UwpVelloGallery;

public sealed partial class MainWindow : Window
{
    private readonly PathBuilder _framePath = new();
    private readonly PathBuilder _barPath = new();
    private readonly DateTimeOffset _start = DateTimeOffset.UtcNow;

    public MainWindow()
    {
        InitializeComponent();

        SwapChain.PaintSurface += OnPaintSurface;
        SwapChain.RenderSurface += OnRenderSurface;
        SwapChain.ContentInvalidated += OnContentInvalidated;
    }

    private void OnPaintSurface(object? sender, VelloPaintSurfaceEventArgs e)
    {
        var session = e.Session;
        var scene = session.Scene;
        scene.Reset();

        var width = session.Width;
        var height = session.Height;

        _framePath.Clear();
        _framePath
            .MoveTo(0, 0)
            .LineTo(width, 0)
            .LineTo(width, height)
            .LineTo(0, height)
            .Close();

        var elapsed = (float)(DateTimeOffset.UtcNow - _start).TotalSeconds;
        var r = 0.16f + 0.24f * MathF.Sin(elapsed * 0.6f);
        var g = 0.28f + 0.18f * MathF.Sin(elapsed * 1.15f + 0.8f);
        var b = 0.58f + 0.34f * MathF.Sin(elapsed * 0.9f + 2.2f);
        var background = new RgbaColor(r, g, b, 1f);

        scene.FillPath(_framePath, FillRule.NonZero, Matrix3x2.Identity, background);

        var barWidth = width * (0.2f + 0.3f * (MathF.Sin(elapsed * 1.6f) + 1f) * 0.5f);
        _barPath.Clear();
        _barPath
            .MoveTo(0, height - 40)
            .LineTo(barWidth, height - 40)
            .LineTo(barWidth, height)
            .LineTo(0, height)
            .Close();

        var accent = new RgbaColor(0.94f, 0.61f, 0.28f, 1f);
        scene.FillPath(_barPath, FillRule.NonZero, Matrix3x2.Identity, accent);
    }

    private void OnRenderSurface(object? sender, VelloSwapChainRenderEventArgs e)
    {
        var fps = e.Delta > TimeSpan.Zero ? 1.0 / e.Delta.TotalSeconds : double.NaN;
        var diagnostics = SwapChain.Diagnostics;

        DiagnosticsLabel.Text =
            $"Backend: {SwapChain.PreferredBackend}\n" +
            $"FPS: {(double.IsFinite(fps) ? fps : 0):F1}\n" +
            $"Presentations: {diagnostics.SwapChainPresentations}\n" +
            $"Surface Resets: {diagnostics.SurfaceConfigurations}\n" +
            $"Last Error: {diagnostics.LastError ?? "None"}";

        if (SwapChain.RenderMode == VelloRenderMode.OnDemand)
        {
            SwapChain.RequestRender();
        }
    }

    private void OnContentInvalidated(object? sender, EventArgs e)
        => SwapChain.RequestRender();
}
