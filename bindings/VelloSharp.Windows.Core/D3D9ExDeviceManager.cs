using System;
using System.Collections.Generic;
using SharpGen.Runtime;
using VelloSharp;
using Vortice.Direct3D9;
using D3D9 = Vortice.Direct3D9.D3D9;

namespace VelloSharp.Windows;

internal sealed class D3D9ExDeviceManager : IDisposable
{
    private static readonly object CacheSync = new();
    private static readonly Dictionary<AdapterLuid, CacheEntry> Cache = new();

    private readonly object _sync = new();
    private readonly AdapterLuid _adapterLuid;
    private readonly IDirect3D9Ex _d3d;
    private readonly IDirect3DDevice9Ex _device;
    private readonly WindowsGpuDiagnostics _diagnostics;
    private bool _disposed;

    private D3D9ExDeviceManager(AdapterLuid adapterLuid, IDirect3D9Ex d3d, IDirect3DDevice9Ex device, WindowsGpuDiagnostics diagnostics)
    {
        _adapterLuid = adapterLuid;
        _d3d = d3d;
        _device = device;
        _diagnostics = diagnostics;
    }

    public AdapterLuid AdapterLuid => _adapterLuid;

    public static D3D9ExDeviceManagerLease Acquire(AdapterLuid targetLuid, WindowsGpuDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        lock (CacheSync)
        {
            if (Cache.TryGetValue(targetLuid, out var entry))
            {
                entry.RefCount += 1;
                return new D3D9ExDeviceManagerLease(targetLuid, entry.Manager);
            }

            var manager = CreateInternal(targetLuid, diagnostics);
            Cache[targetLuid] = new CacheEntry(manager);
            return new D3D9ExDeviceManagerLease(targetLuid, manager);
        }
    }

    private static D3D9ExDeviceManager CreateInternal(AdapterLuid targetLuid, WindowsGpuDiagnostics diagnostics)
    {
        Result result = D3D9.Direct3DCreate9Ex(out var d3d);
        if (result.Failure)
        {
            var message = $"Direct3DCreate9Ex failed: 0x{result.Code:X8}";
            diagnostics.RecordSharedTextureFailure(message);
            throw new InvalidOperationException(message);
        }

        try
        {
            var adapterIndex = FindAdapterIndex(d3d, targetLuid, diagnostics);
            if (adapterIndex < 0)
            {
                diagnostics.RecordSharedTextureFailure("No Direct3D9 adapter matches the shared texture adapter LUID.");
                throw new InvalidOperationException("No Direct3D9 adapter matches the shared texture adapter LUID.");
            }

            var presentParameters = new PresentParameters
            {
                BackBufferWidth = 1,
                BackBufferHeight = 1,
                BackBufferFormat = Format.A8R8G8B8,
                BackBufferCount = 1,
                SwapEffect = SwapEffect.Discard,
                DeviceWindowHandle = IntPtr.Zero,
                Windowed = true,
                PresentationInterval = PresentInterval.Immediate,
            };

            var device = d3d.CreateDeviceEx(
                adapterIndex,
                DeviceType.Hardware,
                IntPtr.Zero,
                CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded | CreateFlags.FpuPreserve,
                presentParameters);

            if (device is null)
            {
                const string message = "IDirect3D9Ex.CreateDeviceEx returned null.";
                diagnostics.RecordSharedTextureFailure(message);
                throw new InvalidOperationException(message);
            }

            return new D3D9ExDeviceManager(targetLuid, d3d, device, diagnostics);
        }
        catch
        {
            d3d.Dispose();
            throw;
        }
    }

    private static int FindAdapterIndex(IDirect3D9Ex d3d, AdapterLuid targetLuid, WindowsGpuDiagnostics diagnostics)
    {
        int adapterCount = d3d.AdapterCount;
        AdapterLuid? firstObserved = null;

        for (int adapter = 0; adapter < adapterCount; adapter++)
        {
            var luid = d3d.GetAdapterLuid(adapter);
            var current = new AdapterLuid((int)(luid >> 32), (uint)(luid & 0xFFFF_FFFF));
            firstObserved ??= current;
            if (current == targetLuid)
            {
                return adapter;
            }
        }

        diagnostics.RecordAdapterMismatch(targetLuid, firstObserved ?? default);
        return -1;
    }

    internal static void Release(AdapterLuid adapterLuid)
    {
        D3D9ExDeviceManager? toDispose = null;

        lock (CacheSync)
        {
            if (!Cache.TryGetValue(adapterLuid, out var entry))
            {
                return;
            }

            entry.RefCount -= 1;
            if (entry.RefCount <= 0)
            {
                Cache.Remove(adapterLuid);
                toDispose = entry.Manager;
            }
        }

        toDispose?.Dispose();
    }

    public D3DImageBackBuffer AcquireBackBuffer(SharedGpuTexture sharedTexture)
    {
        ArgumentNullException.ThrowIfNull(sharedTexture);

        if (_adapterLuid != sharedTexture.AdapterLuid)
        {
            _diagnostics.RecordAdapterMismatch(_adapterLuid, sharedTexture.AdapterLuid);
            throw new InvalidOperationException("Shared texture was created on a different adapter.");
        }

        lock (_sync)
        {
            var sharedHandle = sharedTexture.SharedHandle;
            var texture = _device.CreateTexture(
                (int)sharedTexture.Width,
                (int)sharedTexture.Height,
                1,
                Usage.RenderTarget,
                Format.A8R8G8B8,
                Pool.Default,
                ref sharedHandle);

            if (texture is null)
            {
                const string message = "IDirect3DDevice9Ex.CreateTexture failed to return a texture.";
                _diagnostics.RecordSharedTextureFailure(message);
                throw new InvalidOperationException(message);
            }

            var surface = texture.GetSurfaceLevel(0);
            return new D3DImageBackBuffer(texture, surface, sharedTexture.KeyedMutex);
        }
    }

    public void Flush() { }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _device.Dispose();
        _d3d.Dispose();
        _disposed = true;
    }

    private sealed class CacheEntry
    {
        public CacheEntry(D3D9ExDeviceManager manager)
        {
            Manager = manager;
        }

        public D3D9ExDeviceManager Manager { get; }
        public int RefCount = 1;
    }
}

internal sealed class D3D9ExDeviceManagerLease : IDisposable
{
    private readonly AdapterLuid _adapterLuid;
    private bool _disposed;

    internal D3D9ExDeviceManagerLease(AdapterLuid adapterLuid, D3D9ExDeviceManager manager)
    {
        _adapterLuid = adapterLuid;
        Manager = manager;
    }

    public D3D9ExDeviceManager Manager { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        D3D9ExDeviceManager.Release(_adapterLuid);
    }
}

internal sealed class D3DImageBackBuffer : IDisposable
{
    public IDirect3DTexture9 Texture { get; }
    public IDirect3DSurface9 Surface { get; }
    public IntPtr KeyedMutex { get; }

    public IntPtr SurfacePointer => Surface.NativePointer;

    public D3DImageBackBuffer(IDirect3DTexture9 texture, IDirect3DSurface9 surface, IntPtr keyedMutex)
    {
        Texture = texture ?? throw new ArgumentNullException(nameof(texture));
        Surface = surface ?? throw new ArgumentNullException(nameof(surface));
        KeyedMutex = keyedMutex;
    }

    public void Dispose()
    {
        Surface.Dispose();
        Texture.Dispose();
    }
}









