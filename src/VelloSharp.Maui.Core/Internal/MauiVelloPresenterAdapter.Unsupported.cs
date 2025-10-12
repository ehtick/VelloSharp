using System;
using VelloSharp.Maui.Controls;

namespace VelloSharp.Maui.Internal;

internal sealed class UnsupportedVelloPresenterAdapter : MauiVelloPresenterAdapter
{
    private readonly string _reason;
    private object? _platformView;

    public UnsupportedVelloPresenterAdapter(IVelloView view, string reason)
        : base(view)
    {
        _reason = reason;
    }

    public override void Attach(object? platformView)
    {
        _platformView = platformView;
        ReportGpuUnavailable(_reason);
    }

    public override void Detach()
    {
        _platformView = null;
    }

    public override void RequestRender()
    {
        ReportGpuUnavailable(_reason);
    }

    public override void Dispose()
    {
        _platformView = null;
    }
}
