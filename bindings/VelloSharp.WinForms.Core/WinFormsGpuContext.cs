using System;
using VelloSharp;

namespace VelloSharp.WinForms;

public sealed class WinFormsGpuContext : IDisposable
{
    private static readonly object SharedSync = new();
    private static WinFormsGpuContext? SharedInstance;
    private static int SharedRefCount;

    private readonly object _sync = new();
    private readonly VelloGraphicsDeviceOptions _options;
    private readonly RendererOptions _baseRendererOptions;
    private readonly WgpuInstance _instance;
    private readonly WinFormsGpuResourcePool _resourcePool;

    private WgpuAdapter? _adapter;
    private WgpuDevice? _device;
    private WgpuQueue? _queue;
    private WgpuRenderer? _renderer;
    private WgpuFeature _deviceFeatures;
    private bool _disposed;

    public WinFormsGpuDiagnostics Diagnostics { get; } = new();

    private WinFormsGpuContext(VelloGraphicsDeviceOptions options)
    {
        _options = options;
        _baseRendererOptions = options.RendererOptions ?? new RendererOptions();
        _instance = new WgpuInstance();
        _resourcePool = new WinFormsGpuResourcePool();
    }

    public static WinFormsGpuContextLease Acquire(VelloGraphicsDeviceOptions? options = null)
    {
        var normalized = options ?? VelloGraphicsDeviceOptions.Default;

        lock (SharedSync)
        {
            if (SharedInstance is null)
            {
                SharedInstance = new WinFormsGpuContext(normalized);
                SharedRefCount = 1;
                return new WinFormsGpuContextLease(SharedInstance);
            }

            SharedInstance.EnsureCompatibleOptions(normalized);
            SharedRefCount++;
            return new WinFormsGpuContextLease(SharedInstance);
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

    internal WinFormsGpuResourcePool ResourcePool => _resourcePool;

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

    public WinFormsSwapChainSurface CreateSwapChainSurface(IntPtr hwnd, uint width, uint height)
    {
        if (hwnd == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(hwnd));
        }

        EnsureNotDisposed();

        var descriptor = new SurfaceDescriptor
        {
            Width = Math.Max(width, 1),
            Height = Math.Max(height, 1),
            PresentMode = _options.PresentMode,
            Handle = SurfaceHandle.FromWin32(hwnd),
        };

        var surface = WgpuSurface.Create(_instance, descriptor);

        try
        {
            EnsureAdapter(surface);
            var format = DetermineSurfaceFormat(surface);
            EnsureDevice();

            return new WinFormsSwapChainSurface(this, surface, format, _options.PresentMode, Math.Max(width, 1), Math.Max(height, 1));
        }
        catch
        {
            surface.Dispose();
            throw;
        }
    }

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

    private void EnsureAdapter(WgpuSurface surface)
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
            var requiredFeatures = WgpuFeature.None;
            if (_options.EnablePipelineCaching && adapterFeatures.HasFlag(WgpuFeature.PipelineCache))
            {
                requiredFeatures |= WgpuFeature.PipelineCache;
            }

            var descriptor = new WgpuDeviceDescriptor
            {
                Label = _options.DiagnosticsLabel ?? "vello.winforms.device",
                RequiredFeatures = requiredFeatures,
                Limits = WgpuLimitsPreset.Default,
            };

            _device = _adapter.RequestDevice(descriptor);
            _deviceFeatures = _device.GetFeatures();
            _queue = _device.GetQueue();

            var pipelineCache = _resourcePool.EnsurePipelineCache(_device, _deviceFeatures, _options, Diagnostics);
            var rendererOptions = ComposeRendererOptions(pipelineCache);
            _renderer = new WgpuRenderer(_device, rendererOptions);
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
            throw new InvalidOperationException("The WinForms GPU context has already been initialised with a different render format.");
        }

        if (options.ColorSpace != _options.ColorSpace)
        {
            throw new InvalidOperationException("The WinForms GPU context has already been initialised with a different color space.");
        }

        if (options.PresentMode != _options.PresentMode)
        {
            throw new InvalidOperationException("The WinForms GPU context has already been initialised with a different present mode.");
        }

        if (options.GetAntialiasingMode() != _options.GetAntialiasingMode())
        {
            throw new InvalidOperationException("The WinForms GPU context has already been initialised with a different antialiasing mode.");
        }

        if (options.EnablePipelineCaching != _options.EnablePipelineCaching)
        {
            throw new InvalidOperationException("The WinForms GPU context has already been initialised with different pipeline caching semantics.");
        }
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WinFormsGpuContext));
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
