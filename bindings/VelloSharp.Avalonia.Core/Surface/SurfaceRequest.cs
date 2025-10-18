namespace VelloSharp.Avalonia.Core.Surface;

/// <summary>
/// Describes the parameters required to prepare a render surface for a frame.
/// </summary>
/// <param name="PixelSize">Desired pixel size of the surface.</param>
/// <param name="RenderScaling">Effective render scaling (DPI multiplier).</param>
/// <param name="PlatformSurface">
/// Platform-specific surface reference, such as an Avalonia <c>INativePlatformHandleSurface</c>.
/// </param>
public readonly record struct SurfaceRequest(
    PixelSize PixelSize,
    double RenderScaling,
    object PlatformSurface);
