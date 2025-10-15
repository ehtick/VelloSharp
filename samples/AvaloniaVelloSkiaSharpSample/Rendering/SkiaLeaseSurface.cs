using System;
using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Threading;
using SkiaSharp;
using VelloSharp;
using VelloSharp.Avalonia.Vello.Rendering;
using VelloSharp.Rendering;
using AvaloniaVelloSkiaSharpSample.Services;
using VelloImage = VelloSharp.Image;

namespace AvaloniaVelloSkiaSharpSample.Rendering;

public sealed class SkiaLeaseSurface : Control
{
    public static readonly StyledProperty<ISkiaLeaseRenderer?> RendererProperty =
        AvaloniaProperty.Register<SkiaLeaseSurface, ISkiaLeaseRenderer?>(nameof(Renderer));

    public static readonly StyledProperty<SkiaBackendDescriptor?> BackendDescriptorProperty =
        AvaloniaProperty.Register<SkiaLeaseSurface, SkiaBackendDescriptor?>(nameof(BackendDescriptor));

    static SkiaLeaseSurface()
    {
        AffectsRender<SkiaLeaseSurface>(RendererProperty);
        RendererProperty.Changed.AddClassHandler<SkiaLeaseSurface>((surface, args) => surface.OnRendererChanged(args));
        BackendDescriptorProperty.Changed.AddClassHandler<SkiaLeaseSurface>((surface, args) => surface.OnBackendDescriptorChanged(args));
    }

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private Renderer? _renderer;
    private RenderParams _renderParams = new(1, 1, RgbaColor.FromBytes(0, 0, 0, 0))
    {
        Format = RenderFormat.Rgba8,
    };
    private uint _renderWidth = 1;
    private uint _renderHeight = 1;

    private SKSurface? _surface;
    private int _surfaceWidth;
    private int _surfaceHeight;

    private byte[]? _cpuBuffer;
    private ulong _frameCounter;
    private ISkiaLeaseRendererInvalidation? _invalidationSource;

    public ISkiaLeaseRenderer? Renderer
    {
        get => GetValue(RendererProperty);
        set => SetValue(RendererProperty, value);
    }

    public SkiaBackendDescriptor? BackendDescriptor
    {
        get => GetValue(BackendDescriptorProperty);
        set => SetValue(BackendDescriptorProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        context.Custom(new SkiaLeaseDrawOperation(this, Bounds));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DetachRendererInvalidation();
        DisposeResources();
    }

    private void DisposeResources()
    {
        _renderer?.Dispose();
        _renderer = null;
        _renderWidth = 1;
        _renderHeight = 1;

        _surface?.Dispose();
        _surface = null;
        _surfaceWidth = 0;
        _surfaceHeight = 0;
        _cpuBuffer = null;
    }

    private void OnRendererChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property != RendererProperty)
        {
            return;
        }

        var oldRenderer = args.GetOldValue<ISkiaLeaseRenderer?>();
        var newRenderer = args.GetNewValue<ISkiaLeaseRenderer?>();

        if (oldRenderer is ISkiaLeaseRendererInvalidation oldInvalidation)
        {
            oldInvalidation.RenderInvalidated -= OnRendererInvalidated;
            if (ReferenceEquals(_invalidationSource, oldInvalidation))
            {
                _invalidationSource = null;
            }
        }

        if (newRenderer is ISkiaLeaseRendererInvalidation newInvalidation)
        {
            _invalidationSource = newInvalidation;
            newInvalidation.RenderInvalidated += OnRendererInvalidated;
        }

        InvalidateVisual();
    }

    private void OnBackendDescriptorChanged(AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property != BackendDescriptorProperty)
        {
            return;
        }

        DisposeResources();
        InvalidateVisual();
    }

    private void OnRendererInvalidated(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }

    private void DetachRendererInvalidation()
    {
        if (_invalidationSource is null)
        {
            return;
        }

        _invalidationSource.RenderInvalidated -= OnRendererInvalidated;
        _invalidationSource = null;
    }

    private void EnsureRenderer(uint width, uint height)
    {
        if (_renderer is not null && _renderWidth == width && _renderHeight == height)
        {
            return;
        }

        _renderer?.Dispose();
        _renderer = new Renderer(width, height);
        _renderParams = new RenderParams(width, height, RgbaColor.FromBytes(0, 0, 0, 0))
        {
            Format = RenderFormat.Rgba8,
        };
        _renderWidth = width;
        _renderHeight = height;
    }

    private void EnsureSurface(int width, int height)
    {
        if (_surface is not null && _surfaceWidth == width && _surfaceHeight == height)
        {
            return;
        }

        _surface?.Dispose();
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        _surface = SKSurface.Create(info);
        _surfaceWidth = width;
        _surfaceHeight = height;
    }

    private Span<byte> EnsureCpuBuffer(int width, int height, out int stride)
    {
        stride = Math.Max(1, width) * 4;
        var required = stride * Math.Max(1, height);
        if (_cpuBuffer is null || _cpuBuffer.Length < required)
        {
            _cpuBuffer = new byte[required];
        }

        return _cpuBuffer.AsSpan(0, required);
    }

    private static Matrix3x2 ToMatrix3x2(Matrix matrix) =>
        new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22, (float)matrix.M31, (float)matrix.M32);

    private sealed class SkiaLeaseDrawOperation : ICustomDrawOperation
    {
        private readonly SkiaLeaseSurface _owner;
        private readonly Rect _bounds;

        public SkiaLeaseDrawOperation(SkiaLeaseSurface owner, Rect bounds)
        {
            _owner = owner;
            _bounds = bounds;
        }

        public Rect Bounds => _bounds;

        public void Dispose()
        {
        }

        public bool HitTest(Point p) => _bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) => ReferenceEquals(this, other);

        public void Render(ImmediateDrawingContext context)
        {
            var owner = _owner;
            var renderer = owner.Renderer;
            if (renderer is null)
            {
                return;
            }

            var backendDescriptor = owner.BackendDescriptor ?? SkiaBackendService.GpuDescriptor;

            var featureObject = context.TryGetFeature(typeof(IVelloApiLeaseFeature));
            if (featureObject is not IVelloApiLeaseFeature feature)
            {
                return;
            }

            var bounds = _bounds;
            var scaling = owner.VisualRoot?.RenderScaling ?? 1.0;
            if (scaling <= 0)
            {
                scaling = 1.0;
            }

            var pixelWidth = Math.Max(1, (int)Math.Ceiling(bounds.Width * scaling));
            var pixelHeight = Math.Max(1, (int)Math.Ceiling(bounds.Height * scaling));
            if (pixelWidth <= 0 || pixelHeight <= 0)
            {
                return;
            }

            owner.EnsureRenderer((uint)pixelWidth, (uint)pixelHeight);
            owner.EnsureSurface(pixelWidth, pixelHeight);

            var surface = owner._surface;
            var velloRenderer = owner._renderer;
            if (surface is null || velloRenderer is null)
            {
                return;
            }

            var canvas = surface.Canvas;
            canvas.Save();
            canvas.Clear(SKColors.Transparent);

            try
            {
                var contextData = new SkiaLeaseRenderContext(
                    surface,
                    canvas,
                    surface.Info,
                    bounds,
                    scaling,
                    owner._stopwatch.Elapsed,
                    owner._frameCounter++,
                    backendDescriptor);

                renderer.Render(contextData);
            }
            finally
            {
                canvas.Restore();
                canvas.Flush();
            }

            var buffer = owner.EnsureCpuBuffer(pixelWidth, pixelHeight, out var stride);
            var renderParams = owner._renderParams with
            {
                Width = (uint)pixelWidth,
                Height = (uint)pixelHeight,
            };

            using var lease = feature.Lease();
            if (lease is null)
            {
                return;
            }

            if (backendDescriptor.Kind == SkiaBackendKind.Gpu)
            {
                var platformLease = lease.TryLeasePlatformGraphics();
                if (platformLease is not null)
                {
                    var scene = surface.Scene;
                    var gpuParams = renderParams with { Format = RenderFormat.Bgra8 };

                    lease.ScheduleWgpuSurfaceRender(renderContext =>
                    {
                        using (platformLease)
                        {
                            var gpuParams = renderContext.RenderParams with
                            {
                                Width = (uint)pixelWidth,
                                Height = (uint)pixelHeight,
                                Format = RenderFormat.Bgra8,
                            };

                            platformLease.Renderer.RenderSurface(
                                scene,
                                renderContext.TargetView,
                                gpuParams,
                                renderContext.SurfaceFormat);
                        }
                    });

                    return;
                }
            }

            velloRenderer.Render(surface.Scene, renderParams, buffer, stride);

            using var image = VelloImage.FromPixels(buffer, pixelWidth, pixelHeight, RenderFormat.Rgba8, ImageAlphaMode.Straight, stride);

            var scale = scaling <= 0 ? 1f : (float)(1.0 / scaling);
            var localTransform = Matrix3x2.CreateScale(scale, scale)
                               * Matrix3x2.CreateTranslation((float)bounds.X, (float)bounds.Y);
            var finalTransform = ToMatrix3x2(lease.Transform) * localTransform;

            lease.Scene.DrawImage(image, finalTransform);
        }
    }
}
