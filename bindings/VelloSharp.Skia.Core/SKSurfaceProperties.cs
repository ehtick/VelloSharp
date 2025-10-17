using System;

namespace SkiaSharp;

public sealed class SKSurfaceProperties : IDisposable
{
    public SKSurfaceProperties(SKPixelGeometry pixelGeometry)
        : this(SKSurfacePropsFlags.None, pixelGeometry)
    {
    }

    public SKSurfaceProperties(uint flags, SKPixelGeometry pixelGeometry)
        : this((SKSurfacePropsFlags)flags, pixelGeometry)
    {
    }

    public SKSurfaceProperties(SKSurfacePropsFlags flags, SKPixelGeometry pixelGeometry)
    {
        PixelGeometry = pixelGeometry;
        Flags = flags;
    }

    public SKSurfacePropsFlags Flags { get; }

    public SKPixelGeometry PixelGeometry { get; }

    public bool IsUseDeviceIndependentFonts => Flags.HasFlag(SKSurfacePropsFlags.UseDeviceIndependentFonts);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
