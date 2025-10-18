using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Winit;
using VelloSharp.Avalonia.Core.Device;
using VelloSharp.Avalonia.Core.Surface.Providers;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal sealed class VelloPlatformRenderInterfaceContext : IPlatformRenderInterfaceContext
{
    private readonly WgpuGraphicsDeviceProvider _graphicsDevice;
    private readonly VelloPlatformOptions _options;
    public VelloPlatformRenderInterfaceContext(
        WgpuGraphicsDeviceProvider graphicsDevice,
        VelloPlatformOptions options)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public bool IsLost => false;

    public IReadOnlyDictionary<Type, object> PublicFeatures => s_emptyFeatures;

    private static readonly IReadOnlyDictionary<Type, object> s_emptyFeatures = new Dictionary<Type, object>();

    public IRenderTarget CreateRenderTarget(IEnumerable<object> surfaces)
    {
        if (surfaces is null)
        {
            throw new ArgumentNullException(nameof(surfaces));
        }

        var surfaceList = surfaces as IList<object> ?? surfaces.ToList();

        var surfaceProvider = surfaceList.OfType<IVelloWinitSurfaceProvider>().FirstOrDefault();
        if (surfaceProvider is not null)
        {
            return new VelloSwapchainRenderTarget(_graphicsDevice, _options, surfaceProvider);
        }

        if (OperatingSystem.IsMacOS())
        {
            var topLevel = surfaceList.OfType<ITopLevelImpl>().FirstOrDefault();
            if (topLevel is not null)
            {
                var nativeProvider = new AvaloniaNativeSurfaceProvider(topLevel);
                return new VelloSwapchainRenderTarget(_graphicsDevice, _options, nativeProvider);
            }
        }

        var nativeSurface = surfaceList.OfType<INativePlatformHandleSurface>().FirstOrDefault();
        if (nativeSurface is not null)
        {
            var provider = TryCreateNativePlatformSurfaceProvider(nativeSurface);
            if (provider is not null)
            {
                return new VelloSwapchainRenderTarget(_graphicsDevice, _options, provider);
            }
        }

        throw new NotSupportedException("Unsupported surface type for the Vello backend.");
    }

    private static IVelloWinitSurfaceProvider? TryCreateNativePlatformSurfaceProvider(
        INativePlatformHandleSurface nativeSurface)
    {
        if (nativeSurface is null)
        {
            return null;
        }

        var descriptor = nativeSurface.HandleDescriptor;

        if (OperatingSystem.IsWindows())
        {
            if (string.Equals(descriptor, "HWND", StringComparison.OrdinalIgnoreCase))
            {
                return new Win32SurfaceProvider(nativeSurface);
            }
        }

        if (OperatingSystem.IsLinux())
        {
            return X11SurfaceProvider.TryCreate(nativeSurface);
        }

        return null;
    }

    public IDrawingContextLayerImpl CreateOffscreenRenderTarget(PixelSize pixelSize, double scaling)
    {
        var dpi = new Vector(96 * scaling, 96 * scaling);
        return new VelloOffscreenRenderTarget(pixelSize, dpi, _options, _graphicsDevice);
    }

    public object? TryGetFeature(Type featureType)
    {
        return null;
    }

    public void Dispose()
    {
    }
}
