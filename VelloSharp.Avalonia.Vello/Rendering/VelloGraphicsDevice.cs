using System;
using VelloSharp;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal sealed class VelloGraphicsDevice : IDisposable
{
    private readonly object _syncRoot = new();
    private WgpuInstance? _instance;
    private WgpuAdapter? _adapter;
    private WgpuDevice? _device;
    private WgpuQueue? _queue;
    private WgpuRenderer? _renderer;
    private RendererOptions _options;
    private bool _disposed;

    public (WgpuInstance Instance, WgpuAdapter Adapter, WgpuDevice Device, WgpuQueue Queue, WgpuRenderer Renderer) Acquire(RendererOptions options)
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();

            if (_renderer is null || !OptionsEqual(_options, options))
            {
                Recreate(options);
            }

            return (_instance!, _adapter!, _device!, _queue!, _renderer!);
        }
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
        _options = options;
    }

    private static bool OptionsEqual(RendererOptions left, RendererOptions right)
    {
        return left.UseCpu == right.UseCpu
               && left.SupportArea == right.SupportArea
               && left.SupportMsaa8 == right.SupportMsaa8
               && left.SupportMsaa16 == right.SupportMsaa16
               && left.InitThreads == right.InitThreads
               && Equals(left.PipelineCache?.Handle, right.PipelineCache?.Handle);
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
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VelloGraphicsDevice));
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
