namespace VelloSharp.Avalonia.Core.Surface;

/// <summary>
/// Represents a leased render surface and its associated native resources.
/// </summary>
public abstract class RenderSurfaceLease : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="RenderSurfaceLease"/>.
    /// </summary>
    /// <param name="pixelSize">Pixel size of the surface.</param>
    /// <param name="renderScaling">Render scaling applied to the surface.</param>
    protected RenderSurfaceLease(PixelSize pixelSize, double renderScaling)
    {
        PixelSize = pixelSize;
        RenderScaling = renderScaling;
    }

    /// <summary>
    /// Gets the pixel size of the surface.
    /// </summary>
    public PixelSize PixelSize { get; protected set; }

    /// <summary>
    /// Gets the render scaling (DPI multiplier) for the surface.
    /// </summary>
    public double RenderScaling { get; protected set; }

    /// <summary>
    /// Returns the backend-specific render target for the current frame.
    /// </summary>
    public abstract object GetRenderTarget();

    /// <summary>
    /// Called when the surface size or scaling changes.
    /// </summary>
    public virtual void OnSurfaceChanged(PixelSize pixelSize, double renderScaling)
    {
        PixelSize = pixelSize;
        RenderScaling = renderScaling;
    }

    /// <summary>
    /// Releases native resources held by the surface lease.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            Dispose(true);
        }
        finally
        {
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Invoked when <see cref="Dispose()"/> is called.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
    }
}
