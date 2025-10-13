using System;
using System.Numerics;
using Microsoft.UI.Xaml;
using VelloSharp;
using VelloSharp.Windows.Shared.Contracts;
using VelloSharp.Windows.Shared.Presenters;
using VelloSharp.WinForms.Integration;

namespace WinUIVelloGallery;

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
        var r = 0.18f + 0.22f * MathF.Sin(elapsed * 0.7f);
        var g = 0.24f + 0.18f * MathF.Sin(elapsed * 1.05f + 1.2f);
        var b = 0.55f + 0.35f * MathF.Sin(elapsed * 0.82f + 2.6f);
        var background = new RgbaColor(r, g, b, 1f);

        scene.FillPath(_framePath, FillRule.NonZero, Matrix3x2.Identity, background);

        var barWidth = width * (0.25f + 0.25f * (MathF.Sin(elapsed * 1.8f) + 1f) * 0.5f);
        _barPath.Clear();
        _barPath
            .MoveTo(0, height - 36)
            .LineTo(barWidth, height - 36)
            .LineTo(barWidth, height)
            .LineTo(0, height)
            .Close();

        var accent = new RgbaColor(0.95f, 0.78f, 0.25f, 1f);
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
