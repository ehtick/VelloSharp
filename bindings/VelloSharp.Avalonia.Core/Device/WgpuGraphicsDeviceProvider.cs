using VelloSharp.Avalonia.Core.Options;

namespace VelloSharp.Avalonia.Core.Device;

/// <summary>
/// Provides cached access to Vello's WebGPU renderer stack.
/// </summary>
public sealed class WgpuGraphicsDeviceProvider : IGraphicsDeviceProvider
{
    private readonly object _syncRoot = new();
    private readonly Func<GraphicsDeviceOptions, RendererOptions> _optionsResolver;

    private WgpuInstance? _instance;
    private WgpuAdapter? _adapter;
    private WgpuDevice? _device;
    private WgpuQueue? _queue;
    private WgpuRenderer? _renderer;
    private RendererOptions? _activeOptions;
    private bool _disposed;

    public WgpuGraphicsDeviceProvider(Func<GraphicsDeviceOptions, RendererOptions>? optionsResolver = null)
    {
        _optionsResolver = optionsResolver ?? ResolveDefaultRendererOptions;
    }

    /// <inheritdoc />
    public GraphicsDeviceLease Acquire(GraphicsDeviceOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.Backend != GraphicsBackendKind.VelloWgpu)
        {
            throw new ArgumentException("WGPU provider only supports the VelloWgpu backend.", nameof(options));
        }

        lock (_syncRoot)
        {
            ThrowIfDisposed();

            var rendererOptions = ResolveRendererOptions(options);
            if (_renderer is null || !_activeOptions.HasValue || !OptionsEqual(_activeOptions.Value, rendererOptions))
            {
                Recreate(rendererOptions);
            }

            var resources = new WgpuDeviceResources(
                _instance!,
                _adapter!,
                _device!,
                _queue!,
                _renderer!,
                rendererOptions);

            return new GraphicsDeviceLease(
                options.Backend,
                resources,
                resources.Queue,
                options.Features);
        }
    }

    private RendererOptions ResolveRendererOptions(GraphicsDeviceOptions options)
    {
        if (options.BackendOptions is RendererOptions rendererOptions)
        {
            return rendererOptions;
        }

        return _optionsResolver(options);
    }

    private static RendererOptions ResolveDefaultRendererOptions(GraphicsDeviceOptions options)
    {
        var features = options.Features;
        return new RendererOptions(
            useCpu: features.EnableCpuFallback,
            supportArea: features.EnableAreaAa,
            supportMsaa8: features.EnableMsaa8,
            supportMsaa16: features.EnableMsaa16);
    }

    private void Recreate(RendererOptions options)
    {
        DisposeCore();

        _instance = new WgpuInstance();
        _adapter = _instance.RequestAdapter(new WgpuRequestAdapterOptions
        {
            PowerPreference = WgpuPowerPreference.HighPerformance,
        });

        _device = _adapter.RequestDevice(new WgpuDeviceDescriptor
        {
            Limits = WgpuLimitsPreset.Default,
            RequiredFeatures = WgpuFeature.None,
        });

        _queue = _device.GetQueue();
        _renderer = new WgpuRenderer(_device, options);
        _activeOptions = options;
    }

    private static bool OptionsEqual(RendererOptions left, RendererOptions right)
    {
        var leftCache = left.PipelineCache;
        var rightCache = right.PipelineCache;

        var cacheEqual = leftCache.HasValue == rightCache.HasValue
                         && (!leftCache.HasValue || leftCache.GetValueOrDefault().Equals(rightCache.GetValueOrDefault()));

        return left.UseCpu == right.UseCpu
               && left.SupportArea == right.SupportArea
               && left.SupportMsaa8 == right.SupportMsaa8
               && left.SupportMsaa16 == right.SupportMsaa16
               && left.InitThreads == right.InitThreads
               && cacheEqual;
    }

    private void DisposeCore()
    {
        _renderer?.Dispose();
        _renderer = null;

        _queue?.Dispose();
        _queue = null;

        _device?.Dispose();
        _device = null;

        _adapter?.Dispose();
        _adapter = null;

        _instance?.Dispose();
        _instance = null;

        _activeOptions = null;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WgpuGraphicsDeviceProvider));
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            DisposeCore();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
