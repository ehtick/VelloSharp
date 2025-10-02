using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Winit;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal sealed class VelloPlatformRenderInterfaceContext : IPlatformRenderInterfaceContext
{
    private readonly VelloGraphicsDevice _graphicsDevice;
    private readonly VelloPlatformOptions _options;
    public VelloPlatformRenderInterfaceContext(
        VelloGraphicsDevice graphicsDevice,
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

        var surfaceProvider = surfaces.OfType<IVelloWinitSurfaceProvider>().FirstOrDefault();
        if (surfaceProvider is not null)
        {
            return new VelloSwapchainRenderTarget(_graphicsDevice, _options, surfaceProvider);
        }

        throw new NotSupportedException("Unsupported surface type for the Vello backend.");
    }

    public IDrawingContextLayerImpl CreateOffscreenRenderTarget(PixelSize pixelSize, double scaling)
    {
        throw new NotSupportedException("Offscreen render targets are not supported by the Vello backend yet.");
    }

    public object? TryGetFeature(Type featureType)
    {
        return null;
    }

    public void Dispose()
    {
    }
}
