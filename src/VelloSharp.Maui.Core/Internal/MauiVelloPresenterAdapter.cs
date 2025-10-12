using System;
using Microsoft.Maui;
using System.Diagnostics;
using VelloSharp.Maui.Controls;
using VelloSharp.Maui.Diagnostics;
using VelloSharp.Maui.Events;
using WinFormsIntegration = global::VelloSharp.WinForms.Integration;

namespace VelloSharp.Maui.Internal;

/// <summary>
/// Base presenter facade used by platform handlers to encapsulate native swap chain/device lifetimes.
/// </summary>
internal abstract partial class MauiVelloPresenterAdapter : IDisposable
{
    protected MauiVelloPresenterAdapter(IVelloView view)
    {
        View = view ?? throw new ArgumentNullException(nameof(view));
    }

    protected IVelloView View { get; }

    public static MauiVelloPresenterAdapter Create(IVelloView view, IMauiContext? mauiContext)
        => CreatePlatformAdapter(view, mauiContext) ?? new NullVelloPresenterAdapter(view, "GPU presenter unavailable on this platform.");

    internal static partial MauiVelloPresenterAdapter? CreatePlatformAdapter(IVelloView view, IMauiContext? mauiContext);

    public virtual void Attach(object? platformView) { }

    public virtual void Detach() { }

    public virtual void OnDeviceOptionsChanged() { }

    public virtual void OnPreferredBackendChanged() { }

    public virtual void OnRenderModeChanged() { }

    public virtual void OnRenderLoopDriverChanged() { }

    public virtual void OnDiagnosticsToggled() { }

    public virtual void RequestRender() { }

    public virtual void Suspend() { }

    public virtual void Resume() { }

    public virtual void OnSurfaceConfigurationChanged() { }

    public virtual void OnGraphicsViewSuppressionChanged() { }

    protected void RaisePaintSurface(WinFormsIntegration.VelloPaintSurfaceEventArgs args)
    {
        View.OnPaintSurface(args);
    }

    protected void RaiseRenderSurface(VelloSurfaceRenderEventArgs args)
    {
        View.OnRenderSurface(args);
    }

    protected void RaiseDiagnostics(VelloDiagnosticsSnapshot snapshot)
    {
        View.OnDiagnosticsUpdated(new VelloDiagnosticsChangedEventArgs(snapshot));
    }

    protected void ReportGpuUnavailable(string? reason)
    {
        Debug.WriteLine($"[VelloView] GPU presenter unavailable: {reason}");
        View.OnGpuUnavailable(reason);
    }

    public abstract void Dispose();

    private sealed class NullVelloPresenterAdapter : MauiVelloPresenterAdapter
    {
        private readonly string _reason;

        public NullVelloPresenterAdapter(IVelloView view, string reason)
            : base(view)
        {
            _reason = reason;
            ReportGpuUnavailable(reason);
        }

        public override void RequestRender()
        {
            ReportGpuUnavailable(_reason);
        }

        public override void Dispose()
        {
        }
    }
}
