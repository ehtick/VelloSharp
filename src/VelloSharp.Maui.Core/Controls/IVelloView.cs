using System;
using Microsoft.Maui;
using VelloSharp.Maui.Diagnostics;
using VelloSharp.Maui.Events;
using VelloSharp.Maui.Rendering;
using VelloSharp.WinForms.Integration;

namespace VelloSharp.Maui.Controls;

/// <summary>
/// Shared contract between the MAUI view and platform handlers so property and event updates stay strongly typed.
/// </summary>
public interface IVelloView : IView
{
    VelloGraphicsDeviceOptions DeviceOptions { get; }

    VelloRenderBackend PreferredBackend { get; }

    VelloRenderMode RenderMode { get; }

    RenderLoopDriver RenderLoopDriver { get; }

    bool IsDiagnosticsEnabled { get; }

    bool UseTextureView { get; }

    bool SuppressGraphicsViewCompositor { get; }

    bool IsInDesignMode { get; }

    VelloViewDiagnostics Diagnostics { get; }

    void InvalidateSurface();

    void OnPaintSurface(VelloPaintSurfaceEventArgs args);

    void OnRenderSurface(VelloSurfaceRenderEventArgs args);

    void OnDiagnosticsUpdated(VelloDiagnosticsChangedEventArgs args);

    void OnGpuUnavailable(string? message);
}
