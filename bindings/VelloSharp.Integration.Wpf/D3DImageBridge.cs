using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using D3DImage = System.Windows.Interop.D3DImage;
using VelloSharp;
using VelloSharp.Windows;

namespace VelloSharp.Wpf.Integration;

internal sealed class D3DImageBridge : IDisposable
{
    private const ulong WriterMutexKey = 0;
    private const ulong ReaderMutexKey = 1;

    private readonly object _sync = new();
    private readonly WindowsGpuDiagnostics _diagnostics;

    private WindowsGpuContextLease? _lease;
    private SharedGpuTexture? _sharedTexture;
    private D3D9ExDeviceManagerLease? _d3d9Lease;
    private D3DImageBackBuffer? _backBuffer;
    private D3DImage? _image;
    private IntPtr _currentBackBufferPointer = IntPtr.Zero;
    private WindowsSurfaceSize _pixelSize = WindowsSurfaceSize.Empty;
    private bool _useKeyedMutex;
    private bool _requestedUseKeyedMutex;
    private bool _writerMutexHeld;
    private bool _recordedFallback;
    private bool _disposed;

    internal D3DImageBridge(WindowsGpuDiagnostics diagnostics)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public SharedGpuTexture? SharedTexture => _sharedTexture;

    public WindowsSurfaceSize PixelSize => _pixelSize;

    public WgpuTextureFormat TextureFormat => _sharedTexture?.Format ?? SharedGpuTexture.SharedFormat;

    public bool EnsureResources(
        WindowsGpuContextLease lease,
        uint width,
        uint height,
        bool useKeyedMutex,
        string? label,
        Action? beforeReset = null)
    {
        if (lease is null)
        {
            throw new ArgumentNullException(nameof(lease));
        }

        lock (_sync)
        {
            ThrowIfDisposed();

            if (_sharedTexture is not null &&
                _pixelSize.Width == width &&
                _pixelSize.Height == height &&
                _requestedUseKeyedMutex == useKeyedMutex)
            {
                return true;
            }

            if (_sharedTexture is not null)
            {
                beforeReset?.Invoke();
            }

            ReleaseResourcesLocked();

            _pixelSize = new WindowsSurfaceSize(width, height);

            if (width == 0 || height == 0)
            {
                lease.Dispose();
                return false;
            }

            SharedGpuTexture? shared = null;
            D3D9ExDeviceManagerLease? d3dLease = null;
            D3DImageBackBuffer? backBuffer = null;

            try
            {
                shared = SharedGpuTexture.Create(lease.Context.Device, width, height, useKeyedMutex, label);
                var keyedAvailable = shared.SupportsKeyedMutex && useKeyedMutex;
                if (useKeyedMutex && !keyedAvailable)
                {
                    _diagnostics.RecordKeyedMutexFallback("Shared texture does not expose a keyed mutex. Falling back to flush synchronisation.");
                }

                d3dLease = D3D9ExDeviceManager.Acquire(shared.AdapterLuid, _diagnostics);
                backBuffer = d3dLease.Manager.AcquireBackBuffer(shared);

                _lease = lease;
                _sharedTexture = shared;
                _d3d9Lease = d3dLease;
                _backBuffer = backBuffer;
                _requestedUseKeyedMutex = useKeyedMutex;
                _useKeyedMutex = keyedAvailable;
                _recordedFallback = !keyedAvailable;
                shared = null;
                d3dLease = null;
                backBuffer = null;
            }
            catch
            {
                backBuffer?.Dispose();
                d3dLease?.Dispose();
                shared?.Dispose();
                lease.Dispose();
                _pixelSize = WindowsSurfaceSize.Empty;
                throw;
            }

            if (_image is not null)
            {
                UpdateBackBufferLocked(_image, forceReset: true);
            }

            return true;
        }
    }

    public void AttachImage(D3DImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        lock (_sync)
        {
            ThrowIfDisposed();
            _image = image;
            if (_backBuffer is not null)
            {
                UpdateBackBufferLocked(image, forceReset: true);
            }
            else
            {
                ClearImage(image);
            }
        }
    }

    public bool BeginDraw(uint timeoutMilliseconds, out SharedGpuTexture? texture)
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            texture = _sharedTexture;
            if (texture is null)
            {
                return false;
            }

            if (_useKeyedMutex)
            {
                var acquired = texture.TryAcquireKeyedMutex(WriterMutexKey, timeoutMilliseconds, out var timedOut);
                if (!acquired)
                {
                    if (timedOut)
                    {
                        _diagnostics.RecordKeyedMutexTimeout();
                    }

                    return false;
                }

                _writerMutexHeld = true;
            }

            return true;
        }
    }

    public void EndDraw(bool successful)
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            if (_useKeyedMutex && _writerMutexHeld)
            {
                try
                {
                    _sharedTexture?.ReleaseKeyedMutex(successful ? ReaderMutexKey : WriterMutexKey);
                }
                catch (Exception ex)
                {
                    _diagnostics.RecordSharedTextureFailure($"Failed to release keyed mutex: {ex.Message}");
                }
                finally
                {
                    _writerMutexHeld = false;
                }
            }
            else if (!_useKeyedMutex && successful && _sharedTexture is { } texture)
            {
                texture.FlushWriters();
            }
        }
    }

    public bool Present(Int32Rect dirtyRect, bool forceResetBackBuffer)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_image is null || _backBuffer is null)
            {
                return false;
            }

            if (_useKeyedMutex && _sharedTexture is { } texture)
            {
                if (!texture.TryAcquireKeyedMutex(ReaderMutexKey, 0, out var timedOut))
                {
                    if (timedOut)
                    {
                        _diagnostics.RecordKeyedMutexTimeout();
                    }

                    return false;
                }

                try
                {
                    UpdateBackBufferLocked(_image, forceResetBackBuffer, dirtyRect);
                }
                finally
                {
                    try
                    {
                        texture.ReleaseKeyedMutex(WriterMutexKey);
                    }
                    catch (Exception ex)
                    {
                        _diagnostics.RecordSharedTextureFailure($"Failed to release keyed mutex: {ex.Message}");
                    }
                }

                return true;
            }

            if (_sharedTexture is { } fallbackTexture)
            {
                fallbackTexture.FlushWriters();
                if (!_recordedFallback)
                {
                    _diagnostics.RecordKeyedMutexFallback("Using flush-based synchronisation for shared texture presentation.");
                    _recordedFallback = true;
                }
            }

            UpdateBackBufferLocked(_image, forceResetBackBuffer, dirtyRect);
            _d3d9Lease?.Manager.Flush();
            return true;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            ReleaseResourcesLocked();
        }

        GC.SuppressFinalize(this);
    }

    private void ReleaseResourcesLocked()
    {
        _writerMutexHeld = false;
        _useKeyedMutex = false;
        _requestedUseKeyedMutex = false;
        _recordedFallback = false;

        if (_image is not null)
        {
            ClearImage(_image);
        }

        var backBuffer = _backBuffer;
        _backBuffer = null;

        var d3d9Lease = _d3d9Lease;
        _d3d9Lease = null;

        var sharedTexture = _sharedTexture;
        _sharedTexture = null;

        backBuffer?.Dispose();
        d3d9Lease?.Dispose();
        sharedTexture?.Dispose();

        _lease?.Dispose();
        _lease = null;

        _pixelSize = WindowsSurfaceSize.Empty;
    }

    private void UpdateBackBufferLocked(D3DImage image, bool forceReset, Int32Rect? dirtyRect = null)
    {
        if (_backBuffer is null)
        {
            ClearImage(image);
            return;
        }

        var rect = dirtyRect ?? new Int32Rect(0, 0, (int)_pixelSize.Width, (int)_pixelSize.Height);

        image.Lock();
        try
        {
            var surfacePointer = _backBuffer.SurfacePointer;
            if (forceReset || _currentBackBufferPointer != surfacePointer)
            {
                image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, surfacePointer);
                _currentBackBufferPointer = surfacePointer;
            }

            if (rect.Width > 0 && rect.Height > 0)
            {
                image.AddDirtyRect(rect);
            }
        }
        finally
        {
            image.Unlock();
        }
    }

    private void ClearImage(D3DImage image)
    {
        image.Lock();
        try
        {
            image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
            _currentBackBufferPointer = IntPtr.Zero;
        }
        finally
        {
            image.Unlock();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(D3DImageBridge));
        }
    }
}





