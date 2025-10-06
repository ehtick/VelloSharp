using System;
using VelloSharp;

namespace VelloSharp.Windows;

public sealed class WindowsGpuContext : IDisposable
{
    private static readonly object SharedSync = new();
    private static WindowsGpuContext? SharedInstance;
    private static int SharedRefCount;

    private readonly object _sync = new();
    private readonly VelloGraphicsDeviceOptions _options;
    private readonly RendererOptions _baseRendererOptions;
    private readonly WgpuInstance _instance;
    private readonly WindowsGpuResourcePool _resourcePool;

    private WgpuAdapter? _adapter;
    private WgpuDevice? _device;
    private WgpuQueue? _queue;
    private WgpuRenderer? _renderer;
    private WgpuFeature _deviceFeatures;
    private bool _disposed;

    public WindowsGpuDiagnostics Diagnostics { get; } = new();

    private WindowsGpuContext(VelloGraphicsDeviceOptions options)
    {
        _options = options;
        _baseRendererOptions = options.RendererOptions ?? new RendererOptions();
        var instanceOptions = new WgpuInstanceOptions
        {
            Backends = WgpuBackend.Dx12,
        };
        _instance = new WgpuInstance(instanceOptions);
        _resourcePool = new WindowsGpuResourcePool();
    }

    public static WindowsGpuContextLease Acquire(VelloGraphicsDeviceOptions? options = null)
    {
        var normalized = options ?? VelloGraphicsDeviceOptions.Default;

        lock (SharedSync)
        {
            if (SharedInstance is null)
            {
                SharedInstance = new WindowsGpuContext(normalized);
                SharedRefCount = 1;
                return new WindowsGpuContextLease(SharedInstance);
            }

            SharedInstance.EnsureCompatibleOptions(normalized);
            SharedRefCount++;
            return new WindowsGpuContextLease(SharedInstance);
        }
    }

    internal void Release()
    {
        lock (SharedSync)
        {
            if (!ReferenceEquals(SharedInstance, this))
            {
                return;
            }

            if (SharedRefCount > 0)
            {
                SharedRefCount--;
            }

            if (SharedRefCount == 0)
            {
                SharedInstance.DisposeInternal();
                SharedInstance = null;
            }
        }
    }

    public VelloGraphicsDeviceOptions Options => _options;

    internal WindowsGpuResourcePool ResourcePool => _resourcePool;

    public WgpuAdapter Adapter
    {
        get
        {
            EnsureNotDisposed();
            if (_adapter is null)
            {
                throw new InvalidOperationException("The GPU adapter has not been initialised yet. Create a swap chain surface first.");
            }

            return _adapter;
        }
    }

    public WgpuDevice Device
    {
        get
        {
            EnsureNotDisposed();
            EnsureDevice();
            return _device!;
        }
    }

    public WgpuFeature DeviceFeatures
    {
        get
        {
            EnsureNotDisposed();
            EnsureDevice();
            return _deviceFeatures;
        }
    }

    public WgpuQueue Queue
    {
        get
        {
            EnsureNotDisposed();
            EnsureDevice();
            return _queue!;
        }
    }

    public WgpuRenderer Renderer
    {
        get
        {
            EnsureNotDisposed();
            EnsureDevice();
            return _renderer!;
        }
    }

    public WindowsSwapChainSurface CreateSwapChainSurface(WindowsSurfaceDescriptor surfaceDescriptor, uint width, uint height)
    {
        if (surfaceDescriptor.IsEmpty)
        {
            throw new ArgumentException("Surface descriptor is empty.", nameof(surfaceDescriptor));
        }

        EnsureNotDisposed();

        var descriptor = new SurfaceDescriptor
        {
            Width = Math.Max(width, 1),
            Height = Math.Max(height, 1),
            PresentMode = _options.PresentMode,
            Handle = CreateSurfaceHandle(surfaceDescriptor),
        };

        var surface = WgpuSurface.Create(_instance, descriptor);

        try
        {
            EnsureAdapter(surface);
            var format = DetermineSurfaceFormat(surface);
            EnsureDevice();

            return new WindowsSwapChainSurface(this, surface, format, _options.PresentMode, descriptor.Width, descriptor.Height);
        }
        catch
        {
            surface.Dispose();
            throw;
        }
    }

    private static SurfaceHandle CreateSurfaceHandle(WindowsSurfaceDescriptor descriptor)
        => descriptor.Kind switch
        {
            WindowsSurfaceKind.Win32Hwnd => SurfaceHandle.FromWin32((IntPtr)descriptor.PrimaryHandle, (IntPtr)descriptor.SecondaryHandle),
            WindowsSurfaceKind.SwapChainPanel => SurfaceHandle.FromSwapChainPanel((IntPtr)descriptor.PrimaryHandle),
            WindowsSurfaceKind.CoreWindow => SurfaceHandle.FromCoreWindow((IntPtr)descriptor.PrimaryHandle),
            _ => throw new NotSupportedException($"Unsupported surface kind: {descriptor.Kind}."),
        };

    internal void ConfigureSurface(WgpuSurface surface, uint width, uint height, PresentMode presentMode, WgpuTextureFormat format)
    {
        ArgumentNullException.ThrowIfNull(surface);

        EnsureNotDisposed();
        EnsureDevice();

        lock (_sync)
        {
            var configuration = new WgpuSurfaceConfiguration
            {
                Usage = WgpuTextureUsage.RenderAttachment,
                Format = format,
                Width = Math.Max(width, 1),
                Height = Math.Max(height, 1),
                PresentMode = presentMode,
                AlphaMode = WgpuCompositeAlphaMode.Auto,
                ViewFormats = new[] { format },
            };

            surface.Configure(_device!, configuration);
            Diagnostics.RecordSurfaceConfiguration(configuration.Width, configuration.Height);
        }
    }

    internal void RecordPresentation() => Diagnostics.RecordPresentation();

    internal void RecordDeviceReset(string? reason = null) => Diagnostics.RecordDeviceReset(reason);

    private void EnsureAdapter()
        => EnsureAdapter(null);

    private void EnsureAdapter(WgpuSurface? surface)
    {
        lock (_sync)
        {
            if (_adapter is not null)
            {
                return;
            }

            var requestOptions = new WgpuRequestAdapterOptions
            {
                PowerPreference = _options.PreferDiscreteAdapter ? WgpuPowerPreference.HighPerformance : WgpuPowerPreference.LowPower,
                CompatibleSurface = surface,
            };

            _adapter = _instance.RequestAdapter(requestOptions);
        }
    }

    private void EnsureDevice()
    {
        EnsureAdapter();

        lock (_sync)
        {
            if (_device is not null)
            {
                return;
            }

            if (_adapter is null)
            {
                throw new InvalidOperationException("Cannot create a GPU device before an adapter has been initialised.");
            }

            var adapterFeatures = _adapter.GetFeatures();
            var requestPipelineCache = _options.EnablePipelineCaching && adapterFeatures.HasFlag(WgpuFeature.PipelineCache);

            while (true)
            {
                var descriptor = new WgpuDeviceDescriptor
                {
                    Label = _options.DiagnosticsLabel ?? "vello.windows.device",
                    RequiredFeatures = requestPipelineCache ? WgpuFeature.PipelineCache : WgpuFeature.None,
                    Limits = WgpuLimitsPreset.Default,
                };

                try
                {
                    _device = _adapter.RequestDevice(descriptor);
                    _deviceFeatures = _device.GetFeatures();
                    _queue = _device.GetQueue();

                    var supportsPipelineCache = requestPipelineCache && _deviceFeatures.HasFlag(WgpuFeature.PipelineCache);
                    var pipelineCache = supportsPipelineCache
                        ? _resourcePool.EnsurePipelineCache(_device, _deviceFeatures, _options, Diagnostics)
                        : null;

                    var rendererOptions = ComposeRendererOptions(pipelineCache);
                    _renderer = new WgpuRenderer(_device, rendererOptions);
                    break;
                }
                catch (InvalidOperationException) when (requestPipelineCache)
                {
                    Diagnostics.RecordDeviceReset("Pipeline cache feature unsupported; retrying without optional features.");
                    requestPipelineCache = false;
                }
            }
        }
    }

    private WgpuTextureFormat DetermineSurfaceFormat(WgpuSurface surface)
    {
        EnsureAdapter(surface);
        return _options.GetSwapChainFormat();
    }

    private RendererOptions ComposeRendererOptions(WgpuPipelineCache? pipelineCache)
    {
        var (supportMsaa8, supportMsaa16) = _options.GetMsaaSupportFlags(_baseRendererOptions);

        var options = new RendererOptions(
            _baseRendererOptions.UseCpu,
            _baseRendererOptions.SupportArea,
            supportMsaa8,
            supportMsaa16,
            _baseRendererOptions.InitThreads,
            _baseRendererOptions.PipelineCache);

        if (pipelineCache is not null && _options.EnablePipelineCaching)
        {
            options = options.WithPipelineCache(pipelineCache);
        }

        return options;
    }

    private void EnsureCompatibleOptions(VelloGraphicsDeviceOptions options)
    {
        if (options.Format != _options.Format)
        {
            throw new InvalidOperationException("The Windows GPU context has already been initialised with a different render format.");
        }

        if (options.ColorSpace != _options.ColorSpace)
        {
            throw new InvalidOperationException("The Windows GPU context has already been initialised with a different color space.");
        }

        if (options.PresentMode != _options.PresentMode)
        {
            throw new InvalidOperationException("The Windows GPU context has already been initialised with a different present mode.");
        }

        if (options.GetAntialiasingMode() != _options.GetAntialiasingMode())
        {
            throw new InvalidOperationException("The Windows GPU context has already been initialised with a different antialiasing mode.");
        }

        if (options.EnablePipelineCaching != _options.EnablePipelineCaching)
        {
            throw new InvalidOperationException("The Windows GPU context has already been initialised with different pipeline caching semantics.");
        }
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WindowsGpuContext));
        }
    }

    public void Dispose()
    {
        lock (SharedSync)
        {
            DisposeInternal();
            if (ReferenceEquals(SharedInstance, this))
            {
                SharedInstance = null;
                SharedRefCount = 0;
            }
        }
    }

    private void DisposeInternal()
    {
        if (_disposed)
        {
            return;
        }

        lock (_sync)
        {
            _renderer?.Dispose();
            _renderer = null;

            _queue?.Dispose();
            _queue = null;

            _device?.Dispose();
            _device = null;

            _adapter?.Dispose();
            _adapter = null;

            _resourcePool.Reset();
            _instance.Dispose();
            _disposed = true;
            Diagnostics.RecordDeviceReset();
        }
    }
}

