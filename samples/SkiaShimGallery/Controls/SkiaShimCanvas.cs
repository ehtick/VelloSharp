extern alias RealSkia;

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Platform;
using System.Numerics;
using SkiaGallery.SharedScenes;
using VelloSharp;
using VelloRendering = VelloSharp.Avalonia.Vello.Rendering;
using VelloSharp.Integration.Skia;
using VelloSharp.Rendering;
using SkiaSharp;
using System.Reflection;
using ShimCanvas = SkiaSharp.SKCanvas;
using ShimSurface = SkiaSharp.SKSurface;
using ShimImageInfo = SkiaSharp.SKImageInfo;
using ShimColorType = SkiaSharp.SKColorType;
using ShimAlphaType = SkiaSharp.SKAlphaType;
using RealSkiaSurface = RealSkia::SkiaSharp.SKSurface;
using RealSkiaRect = RealSkia::SkiaSharp.SKRect;
using RealSkiaImageInfo = RealSkia::SkiaSharp.SKImageInfo;
using RealSkiaColorType = RealSkia::SkiaSharp.SKColorType;
using RealSkiaAlphaType = RealSkia::SkiaSharp.SKAlphaType;
using RealSkiaGRContext = RealSkia::SkiaSharp.GRContext;
using VelloImage = VelloSharp.Image;

namespace SkiaShimGallery.Controls;

public sealed class SkiaShimCanvas : Control
{
    public static readonly StyledProperty<ISkiaGalleryScene?> SceneProperty =
        AvaloniaProperty.Register<SkiaShimCanvas, ISkiaGalleryScene?>(nameof(Scene));

    static SkiaShimCanvas()
    {
        AffectsRender<SkiaShimCanvas>(SceneProperty);
    }

    private Renderer? _renderer;
    private RenderParams _renderParams = new(1, 1, RgbaColor.FromBytes(0, 0, 0, 0))
    {
        Format = RenderFormat.Bgra8,
    };
    private uint _renderWidth = 1;
    private uint _renderHeight = 1;

    private ShimSurface? _shimSurface;
    private int _shimWidth;
    private int _shimHeight;

    private RealSkiaSurface? _skiaSurface;
    private int _skiaSurfaceWidth;
    private int _skiaSurfaceHeight;
    private IntPtr _skiaSurfaceGrContextHandle;

    private byte[]? _cpuBuffer;

    public ISkiaGalleryScene? Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DisposeResources();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Scene is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        context.Custom(new ShimDrawOperation(this, Scene, Bounds));
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

    private void EnsureShimSurface(int width, int height)
    {
        if (_shimSurface is not null && _shimWidth == width && _shimHeight == height)
        {
            return;
        }

        _shimSurface?.Dispose();
        var info = new ShimImageInfo(width, height, ShimColorType.Bgra8888, ShimAlphaType.Premul);
        _shimSurface = ShimSurface.Create(info);
        _shimWidth = width;
        _shimHeight = height;
    }

    private void EnsureSkiaSurface(RealSkiaGRContext? grContext, int width, int height)
    {
        var contextHandle = grContext?.Handle ?? IntPtr.Zero;
        if (_skiaSurface is not null &&
            _skiaSurfaceWidth == width &&
            _skiaSurfaceHeight == height &&
            _skiaSurfaceGrContextHandle == contextHandle)
        {
            return;
        }

        DisposeSkiaSurface();

        if (grContext is not null)
        {
            var gpuInfo = new RealSkiaImageInfo(width, height, RealSkiaColorType.Bgra8888, RealSkiaAlphaType.Premul);
            var gpuSurface = RealSkiaSurface.Create(grContext, true, gpuInfo);
            if (gpuSurface is not null)
            {
                _skiaSurface = gpuSurface;
                _skiaSurfaceWidth = width;
                _skiaSurfaceHeight = height;
                _skiaSurfaceGrContextHandle = contextHandle;
                return;
            }
        }

        var cpuInfo = new RealSkiaImageInfo(width, height, RealSkiaColorType.Bgra8888, RealSkiaAlphaType.Premul);
        _skiaSurface = RealSkiaSurface.Create(cpuInfo);
        _skiaSurfaceWidth = width;
        _skiaSurfaceHeight = height;
        _skiaSurfaceGrContextHandle = IntPtr.Zero;
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

    private static bool TryGetSkiaLease(
        ImmediateDrawingContext context,
        Type? featureType,
        out object? leaseInstance,
        out IDisposable? leaseDisposable)
    {
        leaseInstance = null;
        leaseDisposable = null;

        if (featureType is null)
        {
            return false;
        }

        var feature = context.TryGetFeature(featureType);
        if (feature is null)
        {
            return false;
        }

        var leaseMethod = featureType.GetMethod("Lease", Type.EmptyTypes) ?? featureType.GetMethod("Lease");
        if (leaseMethod is null)
        {
            return false;
        }

        var leaseObj = leaseMethod.Invoke(feature, Array.Empty<object>());
        if (leaseObj is not IDisposable disposable)
        {
            (leaseObj as IDisposable)?.Dispose();
            return false;
        }

        leaseInstance = leaseObj;
        leaseDisposable = disposable;
        return true;
    }

    private static T? GetPropertyValue<T>(object instance, string propertyName)
        where T : class
    {
        var prop = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop is null)
        {
            return null;
        }

        var value = prop.GetValue(instance);
        return value as T;
    }

    private void RenderScene(ShimCanvas canvas, ISkiaGalleryScene scene, double logicalWidth, double logicalHeight, double scaling)
    {
        canvas.Clear(SKColors.Transparent);
        canvas.Save();
        canvas.Scale((float)scaling);

        var info = new ShimImageInfo(
            Math.Max(1, (int)Math.Round(logicalWidth)),
            Math.Max(1, (int)Math.Round(logicalHeight)),
            ShimColorType.Bgra8888,
            ShimAlphaType.Premul);

        scene.Render(canvas, info);
        canvas.Restore();
    }

    private void DisposeResources()
    {
        _renderer?.Dispose();
        _renderer = null;
        _renderWidth = 1;
        _renderHeight = 1;

        _shimSurface?.Dispose();
        _shimSurface = null;
        _shimWidth = 0;
        _shimHeight = 0;

        DisposeSkiaSurface();
    }

    private void DisposeSkiaSurface()
    {
        _skiaSurface?.Dispose();
        _skiaSurface = null;
        _skiaSurfaceWidth = 0;
        _skiaSurfaceHeight = 0;
        _skiaSurfaceGrContextHandle = IntPtr.Zero;
    }

    private static Matrix3x2 ToMatrix3x2(Matrix matrix) =>
        new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22, (float)matrix.M31, (float)matrix.M32);

    private sealed class ShimDrawOperation : ICustomDrawOperation
    {
        private readonly SkiaShimCanvas _owner;
        private readonly ISkiaGalleryScene _scene;
        private readonly Rect _bounds;

        public ShimDrawOperation(SkiaShimCanvas owner, ISkiaGalleryScene scene, Rect bounds)
        {
            _owner = owner;
            _scene = scene;
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
            var bounds = _bounds;
            var width = bounds.Width;
            var height = bounds.Height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var scaling = owner.VisualRoot?.RenderScaling ?? 1.0;
            var pixelWidth = Math.Max(1, (int)Math.Ceiling(width * scaling));
            var pixelHeight = Math.Max(1, (int)Math.Ceiling(height * scaling));

            if (context.TryGetFeature<VelloRendering.IVelloApiLeaseFeature>(out var velloFeature) && velloFeature is not null)
            {
                RenderWithVello(velloFeature, owner, bounds, width, height, scaling, pixelWidth, pixelHeight);
                return;
            }

            var avaloniaLeaseType = Type.GetType("Avalonia.Skia.ISkiaSharpApiLeaseFeature, Avalonia.Skia");
            if (!TryGetSkiaLease(context, avaloniaLeaseType, out var leaseInstance, out var leaseDisposable))
            {
                return;
            }

            using var _ = leaseDisposable;
            if (leaseInstance is null)
            {
                return;
            }

            var hasSurface = GetPropertyValue<RealSkiaSurface>(leaseInstance, "SkSurface") is not null;
            if (!hasSurface)
            {
                return;
            }

            var grContext = GetPropertyValue<RealSkiaGRContext>(leaseInstance, "GrContext");
            var targetCanvas = GetPropertyValue<RealSkia::SkiaSharp.SKCanvas>(leaseInstance, "SkCanvas");
            if (targetCanvas is null)
            {
                return;
            }

            owner.EnsureRenderer((uint)pixelWidth, (uint)pixelHeight);
            owner.EnsureShimSurface(pixelWidth, pixelHeight);
            owner.EnsureSkiaSurface(grContext, pixelWidth, pixelHeight);

            var renderer = owner._renderer;
            var shimSurface = owner._shimSurface;
            var skiaSurface = owner._skiaSurface;
            if (renderer is null || shimSurface is null || skiaSurface is null)
            {
                return;
            }

            owner.RenderScene(shimSurface.Canvas, _scene, width, height, scaling);

            SkiaRenderBridge.Render(skiaSurface, renderer, shimSurface.Scene, owner._renderParams);
            skiaSurface.Canvas.Flush();
            var clipRect = new RealSkiaRect(
                (float)(bounds.X * scaling),
                (float)(bounds.Y * scaling),
                (float)((bounds.X + bounds.Width) * scaling),
                (float)((bounds.Y + bounds.Height) * scaling));

            targetCanvas.Save();
            targetCanvas.ClipRect(clipRect);
            targetCanvas.DrawSurface(skiaSurface, clipRect.Left, clipRect.Top);
            targetCanvas.Restore();
        }

        private void RenderWithVello(
            VelloRendering.IVelloApiLeaseFeature feature,
            SkiaShimCanvas owner,
            Rect bounds,
            double width,
            double height,
            double scaling,
            int pixelWidth,
            int pixelHeight)
        {
            using var lease = feature.Lease();
            if (lease is null)
            {
                return;
            }

            owner.EnsureRenderer((uint)pixelWidth, (uint)pixelHeight);
            owner.EnsureShimSurface(pixelWidth, pixelHeight);

            var renderer = owner._renderer;
            var shimSurface = owner._shimSurface;
            if (renderer is null || shimSurface is null)
            {
                return;
            }

            owner.RenderScene(shimSurface.Canvas, _scene, width, height, scaling);

            var buffer = owner.EnsureCpuBuffer(pixelWidth, pixelHeight, out var stride);
            var cpuParams = owner._renderParams with
            {
                Width = (uint)pixelWidth,
                Height = (uint)pixelHeight,
                Format = RenderFormat.Rgba8,
            };
            renderer.Render(shimSurface.Scene, cpuParams, buffer, stride);

            using var image = VelloImage.FromPixels(buffer, pixelWidth, pixelHeight, RenderFormat.Rgba8, ImageAlphaMode.Straight, stride);

            var scale = scaling <= 0 ? 1f : (float)(1.0 / scaling);
            var localTransform = Matrix3x2.CreateScale(scale, scale)
                                * Matrix3x2.CreateTranslation((float)bounds.X, (float)bounds.Y);
            var finalTransform = ToMatrix3x2(lease.Transform) * localTransform;

            lease.Scene.DrawImage(image, finalTransform);
        }
    }
}
