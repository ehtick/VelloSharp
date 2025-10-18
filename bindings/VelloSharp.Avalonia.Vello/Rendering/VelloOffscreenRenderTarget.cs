using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.Platform;
using VelloSharp;
using VelloSharp.Avalonia.Core.Device;
using VelloSharp.Avalonia.Core.Options;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal sealed class VelloOffscreenRenderTarget : IDrawingContextLayerImpl
{
    private static readonly RgbaColor s_transparent = RgbaColor.FromBytes(0, 0, 0, 0);

    private readonly object _syncRoot = new();
    private readonly PixelSize _pixelSize;
    private readonly Vector _dpi;
    private readonly VelloPlatformOptions _options;
    private readonly WgpuGraphicsDeviceProvider? _graphicsDeviceProvider;

    private Scene? _scene;
    private RenderParams _renderParams;
    private bool _pendingRender;
    private bool _disposed;
    private int _version;
    private CpuLayerResources? _cpuResources;

    public VelloOffscreenRenderTarget(
        PixelSize pixelSize,
        Vector dpi,
        VelloPlatformOptions options,
        WgpuGraphicsDeviceProvider? graphicsDeviceProvider = null)
    {
        _pixelSize = pixelSize;
        _dpi = dpi;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _graphicsDeviceProvider = graphicsDeviceProvider;
        _renderParams = CreateRenderParams(pixelSize);
    }

    public Vector Dpi => _dpi;

    public PixelSize PixelSize => _pixelSize;

    public int Version => Volatile.Read(ref _version);

    public bool CanBlit => true;

    public bool IsCorrupted => false;

    public IDrawingContextImpl CreateDrawingContext(bool useScaledDrawing)
    {
        lock (_syncRoot)
        {
            EnsureNotDisposed();

            _scene?.Dispose();
            var scene = new Scene();
            _scene = scene;
            _renderParams = CreateRenderParams(_pixelSize);
            _pendingRender = true;

            return new VelloDrawingContextImpl(
                scene,
                _pixelSize,
                _options,
                OnContextCompleted,
                skipInitialClip: false,
                supportsWgpuSurfaceCallbacks: false);
        }
    }

    public void Blit(IDrawingContextImpl context)
    {
        if (context is VelloDrawingContextImpl velloContext && TryScheduleGpuBlit(velloContext))
        {
            return;
        }

        var bitmap = EnsureCpuRender();
        if (context is VelloDrawingContextImpl velloDrawing)
        {
            var rect = new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
            velloDrawing.DrawBitmap(bitmap, 1.0, rect, rect);
        }
        else
        {
            // Unknown context type – no optimized blit path available.
            using var framebuffer = bitmap.Lock();
            // No-op: bitmap data is ready for consumer via locking.
        }
    }

    public void Save(string fileName, int? quality = null)
    {
        if (fileName is null)
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        var bitmap = EnsureCpuRender();
        bitmap.Save(fileName, quality);
        Volatile.Write(ref _version, bitmap.Version);
    }

    public void Save(Stream stream, int? quality = null)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        var bitmap = EnsureCpuRender();
        bitmap.Save(stream, quality);
        Volatile.Write(ref _version, bitmap.Version);
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _scene?.Dispose();
            _scene = null;
            _cpuResources?.Return();
            _cpuResources = null;
        }
    }

    private void OnContextCompleted(VelloDrawingContextImpl context)
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _renderParams = AdjustRenderParams(context.RenderParams);
            _pendingRender = true;
        }
    }

    private VelloBitmapImpl EnsureCpuRender()
    {
        Scene? scene;
        RenderParams renderParams;
        CpuLayerResources resources;

        lock (_syncRoot)
        {
            EnsureNotDisposed();
            resources = GetCpuResources();

            if (!_pendingRender)
            {
                return resources.Bitmap;
            }

            scene = _scene;
            renderParams = _renderParams;
            _pendingRender = false;
        }

        var span = resources.Buffer;
        span.Clear();

        if (scene is not null)
        {
            resources.Renderer.Render(scene, renderParams, span, resources.Stride);
        }

        using (resources.Bitmap.Lock())
        {
            Volatile.Write(ref _version, resources.Bitmap.Version);
        }

        return resources.Bitmap;
    }

    private bool TryScheduleGpuBlit(VelloDrawingContextImpl context)
    {
        if (_graphicsDeviceProvider is null)
        {
            return false;
        }

        Scene? scene;
        RenderParams renderParams;

        lock (_syncRoot)
        {
            EnsureNotDisposed();

            if (!_pendingRender)
            {
                return false;
            }

            scene = _scene;
            if (scene is null)
            {
                _pendingRender = false;
                Interlocked.Increment(ref _version);
                return true;
            }

            renderParams = _renderParams;
        }

        try
        {
            context.ScheduleWgpuSurfaceRender(renderContext =>
            {
                GraphicsDeviceLease? lease = null;
                try
                {
                    lease = _graphicsDeviceProvider.Acquire(CreateDeviceOptionsForLease(_options));
                    if (!lease.TryGetWgpuResources(out var resources))
                    {
                        return;
                    }

                    var renderer = resources.Renderer;
                    var surfaceParams = renderContext.RenderParams with
                    {
                        BaseColor = s_transparent,
                        Format = DetermineRenderFormat(renderContext.SurfaceFormat),
                        Antialiasing = _options.ResolveAntialiasing(renderContext.RenderParams.Antialiasing),
                    };

                    renderer.RenderSurface(scene, renderContext.TargetView, surfaceParams, renderContext.SurfaceFormat);
                }
                finally
                {
                    lease?.Dispose();
                }
            });

            lock (_syncRoot)
            {
                _pendingRender = false;
            }

            Interlocked.Increment(ref _version);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private CpuLayerResources GetCpuResources()
    {
        if (_cpuResources is not null)
        {
            return _cpuResources;
        }

        var resources = CpuLayerResources.Rent(_pixelSize, _dpi, _options.RendererOptions);
        _cpuResources = resources;
        return resources;
    }

    private RenderParams CreateRenderParams(PixelSize size)
    {
        var width = (uint)Math.Max(1, size.Width);
        var height = (uint)Math.Max(1, size.Height);
        var antialiasing = _options.ResolveAntialiasing(_options.Antialiasing);

        return new RenderParams(width, height, s_transparent)
        {
            Antialiasing = antialiasing,
            Format = RenderFormat.Rgba8,
        };
    }

    private RenderParams AdjustRenderParams(RenderParams renderParams)
    {
        var width = Math.Max(1u, renderParams.Width);
        var height = Math.Max(1u, renderParams.Height);
        var antialiasing = _options.ResolveAntialiasing(renderParams.Antialiasing);

        return renderParams with
        {
            Width = width,
            Height = height,
            BaseColor = s_transparent,
            Antialiasing = antialiasing,
            Format = RenderFormat.Rgba8,
        };
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VelloOffscreenRenderTarget));
        }
    }

    private static GraphicsDeviceOptions CreateDeviceOptionsForLease(VelloPlatformOptions options)
    {
        var rendererOptions = options.RendererOptions;
        var features = new GraphicsFeatureSet(
            EnableCpuFallback: rendererOptions.UseCpu,
            EnableMsaa8: rendererOptions.SupportMsaa8,
            EnableMsaa16: rendererOptions.SupportMsaa16,
            EnableAreaAa: rendererOptions.SupportArea,
            EnableOpacityLayers: true,
            MaxGpuResourceBytes: null,
            EnableValidationLayers: false);

        return new GraphicsDeviceOptions(
            GraphicsBackendKind.VelloWgpu,
            features,
            new GraphicsPresentationOptions(options.PresentMode, options.ClearColor, options.FramesPerSecond),
            rendererOptions);
    }

    private static RenderFormat DetermineRenderFormat(WgpuTextureFormat format) => format switch
    {
        WgpuTextureFormat.Rgba8Unorm or WgpuTextureFormat.Rgba8UnormSrgb => RenderFormat.Rgba8,
        WgpuTextureFormat.Bgra8Unorm or WgpuTextureFormat.Bgra8UnormSrgb => RenderFormat.Bgra8,
        _ => RenderFormat.Rgba8,
    };

    private sealed class CpuLayerResources
    {
        private static readonly object s_poolLock = new();
        private static readonly Dictionary<CpuResourceKey, Stack<CpuLayerResources>> s_pool = new();

        private bool _returned;

        private CpuLayerResources(CpuResourceKey key, Renderer renderer, VelloBitmapImpl bitmap)
        {
            Key = key;
            Renderer = renderer;
            Bitmap = bitmap;
        }

        private CpuResourceKey Key { get; }

        public Renderer Renderer { get; }

        public VelloBitmapImpl Bitmap { get; }

        public int Stride => Math.Max(1, Key.Width) * 4;

        public Span<byte> Buffer
        {
            get
            {
                var pixels = Bitmap.GetPixelsUnsafe();
                return pixels.AsSpan(0, Stride * Math.Max(1, Key.Height));
            }
        }

        public static CpuLayerResources Rent(PixelSize size, Vector dpi, RendererOptions rendererOptions)
        {
            var key = new CpuResourceKey(size.Width, size.Height, ToScaledInt(dpi.X), ToScaledInt(dpi.Y));

            lock (s_poolLock)
            {
                if (s_pool.TryGetValue(key, out var stack) && stack.Count > 0)
                {
                    var resource = stack.Pop();
                    resource._returned = false;
                    return resource;
                }
            }

            var renderer = new Renderer(
                (uint)Math.Max(1, size.Width),
                (uint)Math.Max(1, size.Height),
                rendererOptions);
            var bitmap = VelloBitmapImpl.Create(size, dpi);

            return new CpuLayerResources(key, renderer, bitmap);
        }

        public void Return()
        {
            lock (s_poolLock)
            {
                if (_returned)
                {
                    return;
                }

                if (!s_pool.TryGetValue(Key, out var stack))
                {
                    stack = new Stack<CpuLayerResources>();
                    s_pool.Add(Key, stack);
                }

                stack.Push(this);
                _returned = true;
            }
        }

        private static int ToScaledInt(double value) => (int)Math.Round(value * 100.0);

        private readonly record struct CpuResourceKey(int Width, int Height, int DpiXScaled, int DpiYScaled);
    }
}
