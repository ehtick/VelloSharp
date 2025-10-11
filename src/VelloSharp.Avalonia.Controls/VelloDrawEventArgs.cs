using System;
using System.Numerics;
using Avalonia;
using VelloSharp;
using VelloSharp.Avalonia.Vello.Rendering;

namespace VelloSharp.Avalonia.Controls;

/// <summary>
/// Provides data for a Vello drawing pass.
/// </summary>
public sealed class VelloDrawEventArgs : EventArgs
{
    private readonly IVelloApiLease _lease;
    private readonly Rect _bounds;
    private readonly TimeSpan _totalTime;
    private readonly TimeSpan _deltaTime;
    private Matrix3x2 _globalTransform;
    private bool _transformInitialized;

    internal VelloDrawEventArgs(
        IVelloApiLease lease,
        Rect bounds,
        TimeSpan totalTime,
        TimeSpan deltaTime)
    {
        _lease = lease;
        _bounds = bounds;
        _totalTime = totalTime;
        _deltaTime = deltaTime;
    }

    /// <summary>
    /// Gets the underlying lease that provides access to the Vello renderer for this draw. The lease remains owned by the control and must not be disposed by callers.
    /// </summary>
    public IVelloApiLease Lease => _lease;

    /// <summary>
    /// Gets the scene that is currently being recorded.
    /// </summary>
    public Scene Scene => _lease.Scene;

    /// <summary>
    /// Gets the render parameters associated with the current draw.
    /// </summary>
    public RenderParams RenderParams => _lease.RenderParams;

    /// <summary>
    /// Gets the transform applied to the drawing context.
    /// </summary>
    public Matrix Transform => _lease.Transform;

    /// <summary>
    /// Gets the transform as a <see cref="Matrix3x2"/> for use with Vello primitives.
    /// </summary>
    public Matrix3x2 GlobalTransform
    {
        get
        {
            if (!_transformInitialized)
            {
                _globalTransform = _lease.Transform.ToMatrix3x2();
                _transformInitialized = true;
            }

            return _globalTransform;
        }
    }

    /// <summary>
    /// Gets the bounds of the control that initiated the draw.
    /// </summary>
    public Rect Bounds => _bounds;

    /// <summary>
    /// Gets the total time reported by the control.
    /// </summary>
    public TimeSpan TotalTime => _totalTime;

    /// <summary>
    /// Gets the delta time since the previous draw.
    /// </summary>
    public TimeSpan DeltaTime => _deltaTime;

    /// <summary>
    /// Attempts to lease the platform graphics objects for direct wgpu access.
    /// </summary>
    /// <returns>An <see cref="IVelloPlatformGraphicsLease"/> or <c>null</c> when unavailable.</returns>
    public IVelloPlatformGraphicsLease? TryLeasePlatformGraphics() => _lease.TryLeasePlatformGraphics();

    /// <summary>
    /// Schedules a callback that will be executed on the swapchain surface prior to compositing.
    /// </summary>
    public void ScheduleWgpuSurfaceRender(Action<WgpuSurfaceRenderContext> renderAction)
    {
        ArgumentNullException.ThrowIfNull(renderAction);
        _lease.ScheduleWgpuSurfaceRender(renderAction);
    }
}
