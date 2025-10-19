using System;
using Avalonia.Metadata;
using Avalonia.Platform;
using SkiaSharp;

namespace Avalonia.Skia;

/// <summary>
/// Provides access to SkiaSharp-compatible drawing leases when the Vello backend is active.
/// </summary>
[Unstable]
public interface ISkiaSharpApiLeaseFeature
{
    /// <summary>
    /// Begins a SkiaSharp API lease for the current drawing context.
    /// </summary>
    /// <returns>An <see cref="ISkiaSharpApiLease"/> that must be disposed to finalize rendering.</returns>
    ISkiaSharpApiLease Lease();
}

/// <summary>
/// Represents an active lease that exposes SkiaSharp drawing primitives.
/// </summary>
[Unstable]
public interface ISkiaSharpApiLease : IDisposable
{
    /// <summary>
    /// Gets the Skia canvas that should receive draw commands.
    /// </summary>
    SKCanvas SkCanvas { get; }

    /// <summary>
    /// Gets the optional Skia GPU context associated with the lease.
    /// </summary>
    GRContext? GrContext { get; }

    /// <summary>
    /// Gets the surface that backs the leased canvas.
    /// </summary>
    SKSurface? SkSurface { get; }

    /// <summary>
    /// Gets the effective opacity applied to draw operations.
    /// </summary>
    double CurrentOpacity { get; }

    /// <summary>
    /// Attempts to lease the underlying platform graphics context when available.
    /// </summary>
    /// <returns>A platform graphics lease when supported; otherwise <c>null</c>.</returns>
    ISkiaSharpPlatformGraphicsApiLease? TryLeasePlatformGraphicsApi();
}

/// <summary>
/// Provides access to the platform graphics context associated with a Skia lease.
/// </summary>
[Unstable]
public interface ISkiaSharpPlatformGraphicsApiLease : IDisposable
{
    /// <summary>
    /// Gets the platform graphics context for the current lease.
    /// </summary>
    IPlatformGraphicsContext Context { get; }
}
