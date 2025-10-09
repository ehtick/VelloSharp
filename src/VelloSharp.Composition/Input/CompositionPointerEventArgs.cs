using System;

namespace VelloSharp.Composition.Input;

public sealed class CompositionPointerEventArgs : EventArgs
{
    public CompositionPointerEventArgs(
        PointerEventType eventType,
        PointerDeviceKind deviceKind,
        InputModifiers modifiers,
        PointerButton button,
        ulong pointerId,
        double x,
        double y,
        double deltaX,
        double deltaY,
        double wheelDeltaX,
        double wheelDeltaY,
        TimeSpan timestamp)
    {
        EventType = eventType;
        DeviceKind = deviceKind;
        Modifiers = modifiers;
        Button = button;
        PointerId = pointerId;
        X = x;
        Y = y;
        DeltaX = deltaX;
        DeltaY = deltaY;
        WheelDeltaX = wheelDeltaX;
        WheelDeltaY = wheelDeltaY;
        Timestamp = timestamp;
    }

    public PointerEventType EventType { get; }

    public PointerDeviceKind DeviceKind { get; }

    public InputModifiers Modifiers { get; }

    public PointerButton Button { get; }

    public ulong PointerId { get; }

    public double X { get; }

    public double Y { get; }

    public double DeltaX { get; }

    public double DeltaY { get; }

    public double WheelDeltaX { get; }

    public double WheelDeltaY { get; }

    public TimeSpan Timestamp { get; }

    public bool Handled { get; set; }
}
