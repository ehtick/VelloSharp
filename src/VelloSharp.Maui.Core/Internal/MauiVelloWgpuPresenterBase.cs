using System;
using System.Collections.Generic;
using System.Diagnostics;
using VelloSharp;
using VelloSharp.Windows;
using VelloSharp.Maui.Controls;
using VelloSharp.Maui.Diagnostics;
using VelloSharp.Maui.Events;
using VelloSharp.Maui.Rendering;
using WinFormsIntegration = global::VelloSharp.WinForms.Integration;
using WindowsGraphicsDeviceOptions = VelloSharp.Windows.VelloGraphicsDeviceOptions;

namespace VelloSharp.Maui.Internal;

internal abstract class MauiVelloWgpuPresenterBase : MauiVelloPresenterAdapter
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private VelloSurfaceContext? _context;
    private VelloSurface? _surface;
    private VelloSurfaceRenderer? _renderer;
    private SurfaceHandle _surfaceHandle;
    private VelloGraphicsDevice? _graphicsDevice;
    private WindowsGraphicsDeviceOptions? _lastDeviceOptions;
    private bool _hasHandle;
    private uint _surfaceWidth;
    private uint _surfaceHeight;
    private long _frameId;
    private TimeSpan _lastTimestamp;

    protected MauiVelloWgpuPresenterBase(IVelloView view)
        : base(view)
    {
    }

    protected abstract SurfaceHandle CreateSurfaceHandle();

    protected virtual void ReleaseNativeResources()
    {
    }

    protected virtual IReadOnlyDictionary<string, string>? GetExtendedDiagnostics()
        => null;

    protected void ResetSurface()
    {
        _hasHandle = false;
        _surface?.Dispose();
        _surface = null;
        _renderer?.Dispose();
        _renderer = null;
        _graphicsDevice?.Dispose();
        _graphicsDevice = null;
        _lastDeviceOptions = null;
        _surfaceHandle = default;
    }

    protected bool RenderFrame(
        uint width,
        uint height,
        bool isAnimationFrame,
        object? platformContext = null,
        object? platformSurface = null)
    {
        if (width == 0 || height == 0)
        {
            return false;
        }

        if (!_hasHandle)
        {
            if (!TryAcquireSurfaceHandle())
            {
                return false;
            }
        }

        var deviceOptions = GetWindowsDeviceOptions();

        try
        {
            EnsureSurface(width, height, deviceOptions);
            EnsureGraphicsDevice(width, height, deviceOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VelloView] Failed to ensure surface: {ex}");
            ReportGpuUnavailable(ex.Message);
            return false;
        }

        if (_surface is null || _renderer is null || _graphicsDevice is null)
        {
            return false;
        }

        var now = _stopwatch.Elapsed;
        var delta = _lastTimestamp == TimeSpan.Zero ? TimeSpan.Zero : now - _lastTimestamp;

        using var session = _graphicsDevice.BeginSession(width, height);
        var scene = session.Scene;

        RaisePaintSurface(new WinFormsIntegration.VelloPaintSurfaceEventArgs(session, now, delta, ++_frameId, isAnimationFrame));
        session.Complete();

        var renderParams = new RenderParams(
            width,
            height,
            deviceOptions.BackgroundColor,
            deviceOptions.GetAntialiasingMode(),
            deviceOptions.Format);

        _renderer.Render(_surface, scene, renderParams);

        _lastTimestamp = now;

        var snapshot = new VelloDiagnosticsSnapshot(
            FramesPerSecond: delta > TimeSpan.Zero ? 1.0 / delta.TotalSeconds : 0.0,
            SwapChainResets: 0,
            KeyedMutexContention: 0,
            LastError: null,
            ExtendedProperties: GetExtendedDiagnostics());

        View.Diagnostics.UpdateFrame(delta, snapshot);
        RaiseDiagnostics(snapshot);

        RaiseRenderSurface(new VelloSharp.Maui.Events.VelloSurfaceRenderEventArgs(
            now,
            delta,
            _frameId,
            width,
            height,
            platformContext,
            platformSurface));

        return true;
    }

    private void EnsureSurface(uint width, uint height, WindowsGraphicsDeviceOptions options)
    {
        _context ??= new VelloSurfaceContext();

        var requiresRecreate = _surface is null
            || _renderer is null
            || _lastDeviceOptions is null
            || !_lastDeviceOptions.Equals(options);

        if (requiresRecreate)
        {
            _surface?.Dispose();
            _renderer?.Dispose();

            var descriptor = new SurfaceDescriptor
            {
                Width = width,
                Height = height,
                PresentMode = (PresentMode)options.PresentMode,
                Handle = _surfaceHandle,
            };

            _surface = new VelloSurface(_context, descriptor);
            _renderer = new VelloSurfaceRenderer(_surface, options.RendererOptions);
            _surfaceWidth = width;
            _surfaceHeight = height;
            _lastDeviceOptions = options;
            return;
        }

        if (width != _surfaceWidth || height != _surfaceHeight)
        {
            _surface!.Resize(width, height);
            _surfaceWidth = width;
            _surfaceHeight = height;
        }

        _lastDeviceOptions = options;
    }

    private bool TryAcquireSurfaceHandle()
    {
        try
        {
            var handle = CreateSurfaceHandle();
            _surfaceHandle = handle;
            _hasHandle = true;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VelloView] Unable to acquire surface handle: {ex.Message}");
            ReportGpuUnavailable(ex.Message);
            return false;
        }
    }

    private void EnsureGraphicsDevice(uint width, uint height, WindowsGraphicsDeviceOptions options)
    {
        if (_graphicsDevice is null)
        {
            _graphicsDevice = new VelloGraphicsDevice(width, height, options);
            return;
        }

        if (!_graphicsDevice.Options.Equals(options))
        {
            _graphicsDevice.Dispose();
            _graphicsDevice = new VelloGraphicsDevice(width, height, options);
        }
    }

    public override void Dispose()
    {
        try
        {
            _renderer?.Dispose();
            _surface?.Dispose();
            _context?.Dispose();
            _graphicsDevice?.Dispose();
        }
        finally
        {
            _renderer = null;
            _surface = null;
            _context = null;
            _graphicsDevice = null;
            _lastDeviceOptions = null;
            _hasHandle = false;
            _surfaceHandle = default;
            ReleaseNativeResources();
        }
    }

    private WindowsGraphicsDeviceOptions GetWindowsDeviceOptions()
    {
        var baseOptions = WindowsGraphicsDeviceOptions.Default;
        var source = View.DeviceOptions;
        if (source is null)
        {
            return baseOptions;
        }

        return baseOptions with
        {
            PreferDiscreteAdapter = source.PreferDiscreteAdapter,
            MsaaSampleCount = source.MsaaSampleCount,
            DiagnosticsLabel = source.DiagnosticsLabel,
        };
    }
}
