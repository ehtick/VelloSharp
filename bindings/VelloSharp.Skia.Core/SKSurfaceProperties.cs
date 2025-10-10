namespace SkiaSharp;

public sealed class SKSurfaceProperties : IDisposable
{
    public SKSurfaceProperties(SKPixelGeometry pixelGeometry)
    {
        PixelGeometry = pixelGeometry;
        ShimNotImplemented.Throw($"{nameof(SKSurfaceProperties)}.ctor", "surface property propagation");
    }

    public SKPixelGeometry PixelGeometry { get; }

    public void Dispose()
    {
        // TODO: propagate surface property disposal to Vello surfaces when supported.
    }
}
