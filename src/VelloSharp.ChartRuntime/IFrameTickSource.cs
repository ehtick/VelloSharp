using System;

namespace VelloSharp.ChartRuntime;

/// <summary>
/// Represents a driver that can emit frame ticks aligned to a rendering loop.
/// </summary>
public interface IFrameTickSource : IDisposable
{
    /// <summary>
    /// Raised when a frame tick is ready to be processed.
    /// </summary>
    event Action Tick;

    /// <summary>
    /// Requests that the next frame tick be emitted.
    /// </summary>
    void RequestTick();
}
