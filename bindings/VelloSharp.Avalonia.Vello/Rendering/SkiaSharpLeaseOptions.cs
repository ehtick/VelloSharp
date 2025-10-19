using System;
using System.Numerics;

namespace Avalonia.Skia;

/// <summary>
/// Provides opt-in helpers for controlling how <see cref="ISkiaSharpApiLease"/> renders.
/// </summary>
public static class SkiaSharpLeaseOptions
{
    /// <summary>
    /// Requests that the next Skia lease render directly into the host Vello scene.
    /// </summary>
    /// <param name="width">The pixel width of the lease target.</param>
    /// <param name="height">The pixel height of the lease target.</param>
    /// <param name="localTransform">Transform applied before the host scene transform.</param>
    /// <returns>A disposable scope that restores the previous lease configuration.</returns>
    public static IDisposable UseHostScene(int width, int height, Matrix3x2 localTransform)
        => VelloSharp.Avalonia.Vello.Rendering.SkiaLeaseRequestScope.Activate(new VelloSharp.Avalonia.Vello.Rendering.SkiaLeaseRequest(width, height, localTransform, true));

    /// <summary>
    /// Requests that the next Skia lease record into an isolated scene managed by the shim.
    /// </summary>
    /// <param name="width">The pixel width of the lease target.</param>
    /// <param name="height">The pixel height of the lease target.</param>
    /// <param name="localTransform">Transform applied to the shim scene before append.</param>
    /// <returns>A disposable scope that restores the previous lease configuration.</returns>
    public static IDisposable UseSelfManagedScene(int width, int height, Matrix3x2 localTransform)
        => VelloSharp.Avalonia.Vello.Rendering.SkiaLeaseRequestScope.Activate(new VelloSharp.Avalonia.Vello.Rendering.SkiaLeaseRequest(width, height, localTransform, false));
}
