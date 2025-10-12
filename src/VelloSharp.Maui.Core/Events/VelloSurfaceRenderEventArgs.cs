using System;

namespace VelloSharp.Maui.Events;

/// <summary>
/// Event arguments raised after a frame has been presented. Platform-specific payloads are exposed through <see cref="PlatformContext"/> and <see cref="PlatformSurface"/>.
/// </summary>
public sealed class VelloSurfaceRenderEventArgs : EventArgs
{
    public VelloSurfaceRenderEventArgs(
        TimeSpan timestamp,
        TimeSpan delta,
        long frameId,
        double width,
        double height,
        object? platformContext = null,
        object? platformSurface = null)
    {
        Timestamp = timestamp;
        Delta = delta;
        FrameId = frameId;
        Width = width;
        Height = height;
        PlatformContext = platformContext;
        PlatformSurface = platformSurface;
    }

    public TimeSpan Timestamp { get; }

    public TimeSpan Delta { get; }

    public long FrameId { get; }

    public double Width { get; }

    public double Height { get; }

    public object? PlatformContext { get; }

    public object? PlatformSurface { get; }
}
