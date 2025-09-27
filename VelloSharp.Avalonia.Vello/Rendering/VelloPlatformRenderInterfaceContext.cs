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
    private readonly IPlatformRenderInterfaceContext _fallback;
    private bool _disposed;

    public VelloPlatformRenderInterfaceContext(
        VelloGraphicsDevice graphicsDevice,
        VelloPlatformOptions options,
        IPlatformRenderInterfaceContext fallback)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
    }

    public bool IsLost => _fallback.IsLost;

    public IReadOnlyDictionary<Type, object> PublicFeatures => _fallback.PublicFeatures;

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

        return _fallback.CreateRenderTarget(surfaces);
    }

    public IDrawingContextLayerImpl CreateOffscreenRenderTarget(PixelSize pixelSize, double scaling)
    {
        return _fallback.CreateOffscreenRenderTarget(pixelSize, scaling);
    }

    public object? TryGetFeature(Type featureType)
    {
        return (_fallback as IOptionalFeatureProvider)?.TryGetFeature(featureType);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _fallback.Dispose();
        _disposed = true;
    }
}
