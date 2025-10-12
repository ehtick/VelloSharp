#if IOS || MACCATALYST
using System;
using System.Collections.Generic;
using CoreGraphics;
using UIKit;
using VelloSharp.Composition.Input;
using VelloSharp.Maui.Internal;

namespace VelloSharp.Maui.Input;

public sealed partial class MauiCompositionInputSource
{
    private readonly Dictionary<ulong, CGPoint> _activeTouches = new();
    private MauiMetalView? _metalView;

    partial void InitializePlatformState(object platformView)
    {
        _metalView = platformView as MauiMetalView ?? throw new InvalidOperationException("The MAUI VelloView requires an MTKView-backed platform implementation.");
    }

    partial void ConnectPlatform(object platformView)
    {
        if (_metalView is null)
        {
            return;
        }

        _metalView.InputSource = this;
    }

    partial void DisconnectPlatform(object platformView)
    {
        if (_metalView is null)
        {
            return;
        }

        _metalView.InputSource = null;
        _activeTouches.Clear();
    }

    partial void DisposePlatform(object platformView)
    {
        _activeTouches.Clear();
        if (_metalView is not null)
        {
            _metalView.InputSource = null;
            _metalView = null;
        }
    }

    partial void RequestPointerCapturePlatform(object platformView, ulong pointerId)
    {
        // Pointer capture is not supported on iOS/MacCatalyst; no-op.
    }

    partial void ReleasePointerCapturePlatform(object platformView, ulong pointerId)
    {
        // Pointer capture is not supported on iOS/MacCatalyst; no-op.
    }

    partial void RequestFocusPlatform(object platformView)
    {
        _metalView?.BecomeFirstResponder();
    }

    internal void HandleTouches(IReadOnlyList<UITouch> touches, UIEvent? evt, MauiTouchPhase phase)
    {
        foreach (var touch in touches)
        {
            HandleTouch(touch, phase);
        }
    }

    private void HandleTouch(UITouch touch, MauiTouchPhase phase)
    {
        var pointerId = ToPointerId(touch);
        var location = touch.LocationInView(_metalView);
        var timestamp = TimeSpan.FromSeconds(touch.Timestamp);
        var deviceKind = ToDeviceKind(touch);

        switch (phase)
        {
            case MauiTouchPhase.Began:
                _activeTouches[pointerId] = location;

                ForwardPointerEvent(new CompositionPointerEventArgs(
                    PointerEventType.Enter,
                    deviceKind,
                    InputModifiers.None,
                    PointerButton.None,
                    pointerId,
                    location.X,
                    location.Y,
                    0,
                    0,
                    0,
                    0,
                    timestamp));

                ForwardPointerEvent(new CompositionPointerEventArgs(
                    PointerEventType.Down,
                    deviceKind,
                    InputModifiers.PrimaryButton,
                    PointerButton.Primary,
                    pointerId,
                    location.X,
                    location.Y,
                    0,
                    0,
                    0,
                    0,
                    timestamp));
                break;

            case MauiTouchPhase.Moved:
                var delta = GetDelta(pointerId, location);

                ForwardPointerEvent(new CompositionPointerEventArgs(
                    PointerEventType.Move,
                    deviceKind,
                    InputModifiers.PrimaryButton,
                    PointerButton.Primary,
                    pointerId,
                    location.X,
                    location.Y,
                    delta.dx,
                    delta.dy,
                    0,
                    0,
                    timestamp));
                break;

            case MauiTouchPhase.Ended:
                _activeTouches.Remove(pointerId);

                ForwardPointerEvent(new CompositionPointerEventArgs(
                    PointerEventType.Up,
                    deviceKind,
                    InputModifiers.None,
                    PointerButton.Primary,
                    pointerId,
                    location.X,
                    location.Y,
                    0,
                    0,
                    0,
                    0,
                    timestamp));

                ForwardPointerEvent(new CompositionPointerEventArgs(
                    PointerEventType.Leave,
                    deviceKind,
                    InputModifiers.None,
                    PointerButton.None,
                    pointerId,
                    location.X,
                    location.Y,
                    0,
                    0,
                    0,
                    0,
                    timestamp));
                break;

            case MauiTouchPhase.Cancelled:
                _activeTouches.Remove(pointerId);

                ForwardPointerEvent(new CompositionPointerEventArgs(
                    PointerEventType.Cancel,
                    deviceKind,
                    InputModifiers.None,
                    PointerButton.Primary,
                    pointerId,
                    location.X,
                    location.Y,
                    0,
                    0,
                    0,
                    0,
                    timestamp));

                ForwardPointerEvent(new CompositionPointerEventArgs(
                    PointerEventType.Leave,
                    deviceKind,
                    InputModifiers.None,
                    PointerButton.None,
                    pointerId,
                    location.X,
                    location.Y,
                    0,
                    0,
                    0,
                    0,
                    timestamp));
                break;
        }
    }

    private (double dx, double dy) GetDelta(ulong pointerId, CGPoint current)
    {
        if (_activeTouches.TryGetValue(pointerId, out var previous))
        {
            var delta = (current.X - previous.X, current.Y - previous.Y);
            _activeTouches[pointerId] = current;
            return delta;
        }

        _activeTouches[pointerId] = current;
        return (0, 0);
    }

    private static ulong ToPointerId(UITouch touch)
        => (ulong)(uint)touch.Handle.GetHashCode();

    private static PointerDeviceKind ToDeviceKind(UITouch touch)
        => touch.Type switch
        {
#if IOS || MACCATALYST
            UITouchType.IndirectPointer => PointerDeviceKind.Mouse,
            UITouchType.Indirect => PointerDeviceKind.Mouse,
#endif
            UITouchType.Stylus => PointerDeviceKind.Pen,
            _ => PointerDeviceKind.Touch,
        };
}

internal enum MauiTouchPhase
{
    Began,
    Moved,
    Ended,
    Cancelled,
}
#endif
