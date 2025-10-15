using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Platform;
using VelloSharp.Avalonia.Vello;
using System.Runtime.Versioning;

namespace VelloSharp.Avalonia.Browser;

[SupportedOSPlatform("browser")]
internal sealed class VelloBrowserRenderInterfaceContext : IPlatformRenderInterfaceContext
{
    private static readonly IReadOnlyDictionary<Type, object> s_emptyFeatures =
        new Dictionary<Type, object>();
    private static readonly object s_featuresLock = new();
    private static IReadOnlyDictionary<Type, object>? s_cachedFeatures;

    private readonly VelloPlatformOptions _options;

    public VelloBrowserRenderInterfaceContext(VelloPlatformOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public bool IsLost => false;

    public IReadOnlyDictionary<Type, object> PublicFeatures
    {
        get
        {
            lock (s_featuresLock)
            {
                s_cachedFeatures = VelloBrowserRenderLoopManager.GetRenderLoopFeatures(
                    s_cachedFeatures ?? s_emptyFeatures);
                return s_cachedFeatures;
            }
        }
    }

    public IRenderTarget CreateRenderTarget(IEnumerable<object> surfaces)
    {
        if (surfaces is null)
        {
            throw new ArgumentNullException(nameof(surfaces));
        }

        var surfaceList = surfaces as IList<object> ?? surfaces.ToList();
        if (surfaceList.Count == 0)
        {
            throw new NotSupportedException("Browser WebGPU renderer requires at least one surface.");
        }

        var topLevelImpl = surfaceList.OfType<ITopLevelImpl>().FirstOrDefault();
        if (topLevelImpl is null)
        {
            throw new NotSupportedException("No compatible browser top-level surface was provided.");
        }

        return new VelloBrowserRenderTarget(topLevelImpl, _options);
    }

    public IDrawingContextLayerImpl CreateOffscreenRenderTarget(PixelSize pixelSize, double scaling)
    {
        throw new NotSupportedException("Offscreen render targets are not supported by the Vello browser renderer.");
    }

    public object? TryGetFeature(Type featureType) => null;

    public void Dispose()
    {
    }
}
