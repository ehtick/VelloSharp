using System;
using System.Collections.Generic;
using VelloSharp.Composition.Input;
using VelloSharp.Maui.Controls;

namespace VelloSharp.Maui.Input;

/// <summary>
/// MAUI-backed implementation of <see cref="ICompositionInputSource"/> that translates native pointer,
/// keyboard, and focus events into the shared composition pipeline.
/// </summary>
public sealed partial class MauiCompositionInputSource : ICompositionInputSource
{
    private readonly IVelloView _view;
    private readonly object _platformView;
    private ICompositionInputSink? _sink;
    private bool _disposed;

    public MauiCompositionInputSource(IVelloView view, object platformView)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _platformView = platformView ?? throw new ArgumentNullException(nameof(platformView));
        InitializePlatformState(_platformView);
    }

    public void Connect(ICompositionInputSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MauiCompositionInputSource));
        }

        if (_sink is not null)
        {
            throw new InvalidOperationException("An input sink is already connected.");
        }

        _sink = sink;
        ConnectPlatform(_platformView);
    }

    public void Disconnect(ICompositionInputSink sink)
    {
        if (_sink is null || !ReferenceEquals(_sink, sink))
        {
            return;
        }

        DisconnectPlatform(_platformView);
        _sink = null;
    }

    public void RequestPointerCapture(ICompositionInputSink sink, ulong pointerId)
    {
        if (_sink is null || !ReferenceEquals(_sink, sink))
        {
            return;
        }

        RequestPointerCapturePlatform(_platformView, pointerId);
    }

    public void ReleasePointerCapture(ICompositionInputSink sink, ulong pointerId)
    {
        if (_sink is null || !ReferenceEquals(_sink, sink))
        {
            return;
        }

        ReleasePointerCapturePlatform(_platformView, pointerId);
    }

    public void RequestFocus(ICompositionInputSink sink)
    {
        if (_sink is null || !ReferenceEquals(_sink, sink))
        {
            return;
        }

        RequestFocusPlatform(_platformView);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_sink is not null)
        {
            DisconnectPlatform(_platformView);
            _sink = null;
        }

        DisposePlatform(_platformView);
        GC.SuppressFinalize(this);
    }

    internal IVelloView View => _view;

    internal bool ForwardPointerEvent(CompositionPointerEventArgs args)
    {
        var sink = _sink;
        if (sink is null)
        {
            return false;
        }

        sink.ProcessPointerEvent(args);
        return args.Handled;
    }

    internal bool ForwardKeyEvent(CompositionKeyEventArgs args)
    {
        var sink = _sink;
        if (sink is null)
        {
            return false;
        }

        sink.ProcessKeyEvent(args);
        return args.Handled;
    }

    internal bool ForwardTextInput(CompositionTextInputEventArgs args)
    {
        var sink = _sink;
        if (sink is null)
        {
            return false;
        }

        sink.ProcessTextInput(args);
        return args.Handled;
    }

    internal void ForwardFocusChanged(bool isFocused)
    {
        _sink?.ProcessFocusChanged(isFocused);
    }

    partial void InitializePlatformState(object platformView);

    partial void ConnectPlatform(object platformView);

    partial void DisconnectPlatform(object platformView);

    partial void DisposePlatform(object platformView);

    partial void RequestPointerCapturePlatform(object platformView, ulong pointerId);

    partial void ReleasePointerCapturePlatform(object platformView, ulong pointerId);

    partial void RequestFocusPlatform(object platformView);
}
