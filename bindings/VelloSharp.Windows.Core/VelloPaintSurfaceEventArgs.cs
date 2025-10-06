using System;
using VelloSharp.Windows;

namespace VelloSharp.WinForms.Integration;

public sealed partial class VelloPaintSurfaceEventArgs : EventArgs
{
    internal VelloPaintSurfaceEventArgs(
        VelloGraphicsSession session,
        TimeSpan timestamp,
        TimeSpan delta,
        long frameId,
        bool isAnimationFrame)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Timestamp = timestamp;
        Delta = delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        FrameId = frameId;
        IsAnimationFrame = isAnimationFrame;
    }

    public VelloGraphicsSession Session { get; }

    public TimeSpan Timestamp { get; }

    public TimeSpan Delta { get; }

    public long FrameId { get; }

    public bool IsAnimationFrame { get; }
}
