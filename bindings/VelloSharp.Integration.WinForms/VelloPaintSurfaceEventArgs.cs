using System;
using VelloSharp.WinForms;

namespace VelloSharp.WinForms.Integration;

public sealed class VelloPaintSurfaceEventArgs : EventArgs
{
    private VelloGraphics? _graphics;

    internal VelloPaintSurfaceEventArgs(VelloGraphicsSession session, TimeSpan timestamp, TimeSpan delta, long frameId, bool isAnimationFrame)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Timestamp = timestamp;
        Delta = delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        FrameId = frameId;
        IsAnimationFrame = isAnimationFrame;
    }

    public VelloGraphicsSession Session { get; }

    public VelloGraphics Graphics => _graphics ??= new VelloGraphics(Session);

    public TimeSpan Timestamp { get; }

    public TimeSpan Delta { get; }

    public long FrameId { get; }

    public bool IsAnimationFrame { get; }
}
