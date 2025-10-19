using System;
using System.Numerics;
using VelloSharp.Avalonia.Vello.Rendering;

namespace Avalonia.Skia;

/// <summary>
/// Provides opt-in helpers for controlling how <see cref="ISkiaSharpApiLease"/> renders.
/// </summary>
public static class SkiaSharpLeaseOptions
{
    /// <summary>
    /// Enables host-scene rendering for all subsequent Skia leases until the returned scope is disposed.
    /// </summary>
    public static IDisposable EnableHostSceneByDefault()
    {
        var previous = SkiaSharpLeaseDefaults.UseHostSceneByDefault;
        SkiaSharpLeaseDefaults.UseHostSceneByDefault = true;
        return new DisposableAction(() => SkiaSharpLeaseDefaults.UseHostSceneByDefault = previous);
    }

    /// <summary>
    /// Enables GPU interop for all subsequent Skia leases until the returned scope is disposed.
    /// </summary>
    public static IDisposable EnableGpuInteropByDefault()
    {
        var previous = SkiaSharpLeaseDefaults.UseGpuInteropByDefault;
        SkiaSharpLeaseDefaults.UseGpuInteropByDefault = true;
        return new DisposableAction(() => SkiaSharpLeaseDefaults.UseGpuInteropByDefault = previous);
    }

    /// <summary>
    /// Requests that the next Skia lease render directly into the host Vello scene.
    /// </summary>
    /// <param name="width">The pixel width of the lease target.</param>
    /// <param name="height">The pixel height of the lease target.</param>
    /// <param name="localTransform">Transform applied before the host scene transform.</param>
    /// <returns>A disposable scope that restores the previous lease configuration.</returns>
    public static IDisposable UseHostScene(int width, int height, Matrix3x2 localTransform)
        => SkiaLeaseRequestScope.Activate(
            new SkiaLeaseRequest(
                width,
                height,
                localTransform,
                UseHostScene: true,
                UseGpuInterop: SkiaSharpLeaseDefaults.UseGpuInteropByDefault));

    /// <summary>
    /// Requests that the next Skia lease record into an isolated scene managed by the shim.
    /// </summary>
    /// <param name="width">The pixel width of the lease target.</param>
    /// <param name="height">The pixel height of the lease target.</param>
    /// <param name="localTransform">Transform applied to the shim scene before append.</param>
    /// <returns>A disposable scope that restores the previous lease configuration.</returns>
    public static IDisposable UseSelfManagedScene(int width, int height, Matrix3x2 localTransform)
        => SkiaLeaseRequestScope.Activate(
            new SkiaLeaseRequest(
                width,
                height,
                localTransform,
                UseHostScene: false,
                UseGpuInterop: SkiaSharpLeaseDefaults.UseGpuInteropByDefault));

    /// <summary>
    /// Requests that the next Skia lease enable GPU interop for its scene.
    /// </summary>
    public static IDisposable UseGpuInterop(int width, int height, Matrix3x2 localTransform, bool useHostScene)
        => SkiaLeaseRequestScope.Activate(
            new SkiaLeaseRequest(
                width,
                height,
                localTransform,
                UseHostScene: useHostScene,
                UseGpuInterop: true));

    private sealed class DisposableAction : IDisposable
    {
        private readonly Action _onDispose;
        private bool _disposed;

        public DisposableAction(Action onDispose)
        {
            _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _onDispose();
        }
    }
}
