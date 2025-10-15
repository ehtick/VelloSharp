using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia;
using VelloSharp;

namespace VelloSharp.Avalonia.Vello.Rendering;

/// <summary>
/// Represents a transferable lease over a recorded Vello scene produced by <see cref="VelloDrawingContextImpl"/>.
/// The lease keeps the scene alive until rendering completes, supporting both synchronous and deferred submission.
/// </summary>
internal sealed class SceneLease : IDisposable
{
    private readonly VelloDrawingContextImpl _owner;
    private readonly IReadOnlyList<Action<WgpuSurfaceRenderContext>>? _wgpuSurfaceCallbacks;
    private bool _disposed;

    internal SceneLease(
        VelloDrawingContextImpl owner,
        Scene scene,
        RenderParams renderParams,
        Matrix transform,
        IReadOnlyList<Action<WgpuSurfaceRenderContext>>? wgpuSurfaceCallbacks)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Scene = scene ?? throw new ArgumentNullException(nameof(scene));
        RenderParams = renderParams;
        Transform = transform;
        _wgpuSurfaceCallbacks = wgpuSurfaceCallbacks;
    }

    /// <summary>
    /// Gets the recorded scene.
    /// </summary>
    public Scene Scene { get; }

    /// <summary>
    /// Gets the render parameters associated with the scene.
    /// </summary>
    public RenderParams RenderParams { get; private set; }

    /// <summary>
    /// Gets the transform that was active when the scene was leased.
    /// </summary>
    public Matrix Transform { get; }

    /// <summary>
    /// Gets the pending wgpu surface callbacks scheduled for this scene, when available.
    /// </summary>
    public IReadOnlyList<Action<WgpuSurfaceRenderContext>>? WgpuSurfaceCallbacks => _wgpuSurfaceCallbacks;

    /// <summary>
    /// Updates the render parameters after runtime adjustments (e.g., antialiasing, format).
    /// </summary>
    /// <param name="renderParams">The adjusted render parameters.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void UpdateRenderParams(RenderParams renderParams)
    {
        RenderParams = renderParams;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Scene.Dispose();
        _owner.OnSceneLeaseDisposed(this);
    }
}
