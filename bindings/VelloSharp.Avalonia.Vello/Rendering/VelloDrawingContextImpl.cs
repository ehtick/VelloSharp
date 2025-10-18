using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Avalonia.Utilities;
using VelloSharp;
using VelloSharp.Avalonia.Vello.Geometry;
using VelloSharp.Avalonia.Core.Device;
using VelloSharp.Avalonia.Core.Options;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal sealed class VelloDrawingContextImpl : IDrawingContextImpl
{
    private readonly Scene _scene;
    private readonly PixelSize _targetSize;
    private readonly Action<VelloDrawingContextImpl> _onCompleted;
    private readonly VelloPlatformOptions _options;
    private readonly bool _supportsWgpuSurfaceCallbacks;
    private readonly WgpuGraphicsDeviceProvider? _graphicsDeviceProvider;
    private bool _disposed;
    private readonly Stack<Matrix> _transformStack = new();
    private readonly Stack<LayerEntry> _layerStack = new();
    private readonly List<IDisposable> _deferredDisposables = new();
    private List<Action<WgpuSurfaceRenderContext>>? _wgpuSurfaceCallbacks;
    private int _clipDepth;
    private int _opacityDepth;
    private int _layerDepth;
    private readonly Stack<byte> _opacityMaskLayerEntries = new();
    private RenderOptions _renderOptions;
    private readonly Stack<RenderOptions> _renderOptionsStack = new();
    private bool _skipInitialClip;
    private VelloLeaseFeature? _leaseFeature;
    private bool _apiLeaseActive;
    private bool _platformGraphicsLeaseActive;
    private bool _sceneLeased;
    private SceneLease? _activeSceneLease;
    private static readonly LayerBlend s_defaultLayerBlend = new(LayerMix.Normal, LayerCompose.SrcOver);
    private static readonly LayerBlend s_clipLayerBlend = new(LayerMix.Clip, LayerCompose.SrcOver);
    private static readonly LayerBlend s_destOutLayerBlend = new(LayerMix.Normal, LayerCompose.DestOut);
    private static readonly VelloSharp.SolidColorBrush s_opaqueWhiteBrush = new(new RgbaColor(1f, 1f, 1f, 1f));
    private static readonly global::Avalonia.Vector s_intermediateDpi = new(96, 96);
    private static readonly RenderOptions s_defaultRenderOptions = new()
    {
        BitmapInterpolationMode = BitmapInterpolationMode.HighQuality,
        BitmapBlendingMode = BitmapBlendingMode.SourceOver,
        EdgeMode = EdgeMode.Unspecified,
        TextRenderingMode = TextRenderingMode.Unspecified,
        RequiresFullOpacityHandling = null,
    };
    private static readonly PropertyInfo? s_imageBrushBitmapProperty =
        typeof(IImageBrushSource).GetProperty("Bitmap", BindingFlags.Instance | BindingFlags.NonPublic);

    public VelloDrawingContextImpl(
        Scene scene,
        PixelSize targetSize,
        VelloPlatformOptions options,
        Action<VelloDrawingContextImpl> onCompleted,
        bool skipInitialClip = false,
        bool supportsWgpuSurfaceCallbacks = false,
        WgpuGraphicsDeviceProvider? graphicsDeviceProvider = null)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _targetSize = targetSize;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _onCompleted = onCompleted ?? throw new ArgumentNullException(nameof(onCompleted));
        _skipInitialClip = skipInitialClip;
        _graphicsDeviceProvider = graphicsDeviceProvider;
        _supportsWgpuSurfaceCallbacks = supportsWgpuSurfaceCallbacks && graphicsDeviceProvider is not null;
        Transform = Matrix.Identity;
        RenderParams = new RenderParams((uint)Math.Max(1, targetSize.Width), (uint)Math.Max(1, targetSize.Height), options.ClearColor)
        {
            Antialiasing = AntialiasingMode.Area,
            Format = RenderFormat.Bgra8,
        };
        _renderOptions = s_defaultRenderOptions;
    }

    public Matrix Transform { get; set; }

    public RenderParams RenderParams { get; private set; }

    public Scene Scene => _scene;

    internal void ScheduleWgpuSurfaceRender(Action<WgpuSurfaceRenderContext> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (!_supportsWgpuSurfaceCallbacks || _graphicsDeviceProvider is null)
        {
            throw new PlatformNotSupportedException("WGPU surface render callbacks are not supported on this platform.");
        }

        if (_sceneLeased)
        {
            throw new InvalidOperationException("WGPU surface render callbacks must be scheduled before leasing the scene.");
        }

        (_wgpuSurfaceCallbacks ??= new List<Action<WgpuSurfaceRenderContext>>()).Add(callback);
    }

    internal List<Action<WgpuSurfaceRenderContext>>? TakeWgpuSurfaceRenderCallbacks()
    {
        var callbacks = _wgpuSurfaceCallbacks;
        _wgpuSurfaceCallbacks = null;
        return callbacks;
    }

    internal SceneLease LeaseScene()
    {
        EnsureNotDisposed();

        if (_sceneLeased)
        {
            throw new InvalidOperationException("The current scene has already been leased.");
        }

        var lease = new SceneLease(this, _scene, RenderParams, Transform, TakeWgpuSurfaceRenderCallbacks());
        _sceneLeased = true;
        _activeSceneLease = lease;
        return lease;
    }

    internal void OnSceneLeaseDisposed(SceneLease lease)
    {
        if (ReferenceEquals(_activeSceneLease, lease))
        {
            _activeSceneLease = null;
        }

        _sceneLeased = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _onCompleted(this);
        }
        finally
        {
            _disposed = true;
            foreach (var disposable in _deferredDisposables)
            {
                disposable.Dispose();
            }

            _deferredDisposables.Clear();
        }
    }

    public void Clear(Color color)
    {
        var brush = new VelloSharp.SolidColorBrush(ToRgbaColor(color, 1.0));
        var builder = new PathBuilder();
        builder.AddRectangle(new Rect(0, 0, _targetSize.Width, _targetSize.Height));
        _scene.FillPath(builder, VelloSharp.FillRule.NonZero, Matrix3x2.Identity, brush);
        RenderParams = RenderParams with { BaseColor = ToRgbaColor(color, 1.0) };
    }

    public void DrawBitmap(IBitmapImpl source, double opacity, Rect sourceRect, Rect destRect)
    {
        EnsureNotDisposed();

        if (source is not VelloBitmapImpl bitmap)
        {
            throw new NotSupportedException("The provided bitmap implementation is not compatible with the Vello renderer.");
        }

        if (sourceRect.Width <= 0 || sourceRect.Height <= 0 || destRect.Width <= 0 || destRect.Height <= 0)
        {
            return;
        }

        using var image = bitmap.CreateVelloImage();
        var brush = new ImageBrush(image)
        {
            Alpha = (float)opacity,
            Quality = ToImageQuality(GetEffectiveBitmapInterpolationMode()),
        };

        var scaleX = destRect.Width / sourceRect.Width;
        var scaleY = destRect.Height / sourceRect.Height;

        var transform = Matrix3x2.CreateTranslation((float)(-sourceRect.X), (float)(-sourceRect.Y))
                         * Matrix3x2.CreateScale((float)scaleX, (float)scaleY)
                         * Matrix3x2.CreateTranslation((float)destRect.X, (float)destRect.Y)
                         * ToMatrix3x2(Transform);

        var (layerBlend, skipDraw) = ResolveBitmapBlendingMode(GetEffectiveBitmapBlendingMode());
        if (skipDraw)
        {
            return;
        }

        if (layerBlend.HasValue)
        {
            var blendPath = CreateRectanglePath(destRect);
            PushSceneLayer(blendPath, 1f, layerBlend.Value, ToMatrix3x2(Transform));
        }

        try
        {
            _scene.DrawImage(brush, transform);
        }
        finally
        {
            if (layerBlend.HasValue)
            {
                PopLayerEntry();
            }
        }
    }

    public void DrawBitmap(IBitmapImpl source, IBrush opacityMask, Rect opacityMaskRect, Rect destRect)
    {
        EnsureNotDisposed();

        if (destRect.Width <= 0 || destRect.Height <= 0)
        {
            return;
        }

        if (opacityMaskRect.Width <= 0 || opacityMaskRect.Height <= 0)
        {
            return;
        }

        PushOpacityMask(opacityMask, opacityMaskRect);
        try
        {
            if (source is not VelloBitmapImpl bitmap)
            {
                throw new NotSupportedException("The provided bitmap implementation is not compatible with the Vello renderer.");
            }

            var sourceRect = new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
            DrawBitmap(source, 1.0, sourceRect, destRect);
        }
        finally
        {
            PopOpacityMask();
        }
    }

    public void DrawLine(IPen? pen, Point p1, Point p2)
    {
        if (pen is null)
        {
            return;
        }

        var bounds = new Rect(new Point(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y)), new Point(Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y)));

        if (!TryCreateStroke(pen, bounds, out var style, out var strokeBrush, out var brushTransform))
        {
            return;
        }

        var builder = new PathBuilder();
        builder.MoveTo(p1.X, p1.Y);
        builder.LineTo(p2.X, p2.Y);
        ApplyStroke(builder, style, strokeBrush, brushTransform);
    }

    public void DrawGeometry(IBrush? brush, IPen? pen, IGeometryImpl geometry)
    {
        if (geometry is not VelloGeometryImplBase velloGeometry)
        {
            throw new NotSupportedException("Only Vello geometry implementations are supported in this limited context.");
        }

        var pathBuilder = BuildPath(velloGeometry);
        var fillRule = ToVelloFillRule(velloGeometry.EffectiveFillRule);
        var transform = ToMatrix3x2(Transform);

        var bounds = velloGeometry.Bounds;

        if (brush is not null && TryCreateBrush(brush, bounds, out var velloBrush, out var brushTransform))
        {
            _scene.FillPath(pathBuilder, fillRule, transform, velloBrush, brushTransform);
        }

        if (pen is not null && TryCreateStroke(pen, bounds, out var strokeStyle, out var strokeBrush, out var strokeBrushTransform))
        {
            _scene.StrokePath(pathBuilder, strokeStyle, transform, strokeBrush, strokeBrushTransform);
        }
    }

    public void DrawRectangle(IBrush? brush, IPen? pen, RoundedRect rect, BoxShadows boxShadows = default)
    {
        if (brush is null && pen is null)
        {
            return;
        }

        var builder = new PathBuilder();
        builder.AddRoundedRectangle(rect);

        var bounds = rect.Rect;

        if (brush is not null && TryCreateBrush(brush, bounds, out var velloBrush, out var brushTransform))
        {
            _scene.FillPath(builder, VelloSharp.FillRule.NonZero, ToMatrix3x2(Transform), velloBrush, brushTransform);
        }

        if (pen is not null && TryCreateStroke(pen, bounds, out var strokeStyle, out var strokeBrush, out var strokeBrushTransform))
        {
            _scene.StrokePath(builder, strokeStyle, ToMatrix3x2(Transform), strokeBrush, strokeBrushTransform);
        }

        if (boxShadows.Count > 0)
        {
            DrawBoxShadows(rect, boxShadows);
        }
    }

    public void DrawRegion(IBrush? brush, IPen? pen, IPlatformRenderInterfaceRegion region)
    {
        if (brush is null && pen is null)
        {
            return;
        }

        if (region is not VelloRegionImpl velloRegion)
        {
            return;
        }

        if (!velloRegion.TryCreatePath(out var pathBuilder, out var regionBounds) || pathBuilder is null)
        {
            return;
        }

        var transform = ToMatrix3x2(Transform);

        if (brush is not null && TryCreateBrush(brush, regionBounds, out var fillBrush, out var fillTransform))
        {
            _scene.FillPath(pathBuilder, VelloSharp.FillRule.NonZero, transform, fillBrush, fillTransform);
        }

        if (pen is not null && TryCreateStroke(pen, regionBounds, out var strokeStyle, out var strokeBrush, out var strokeTransform))
        {
            _scene.StrokePath(pathBuilder, strokeStyle, transform, strokeBrush, strokeTransform);
        }
    }

    public void DrawEllipse(IBrush? brush, IPen? pen, Rect rect)
    {
        if (brush is null && pen is null)
        {
            return;
        }

        var builder = new PathBuilder();
        builder.AddEllipse(rect);

        if (brush is not null && TryCreateBrush(brush, rect, out var velloBrush, out var brushTransform))
        {
            _scene.FillPath(builder, VelloSharp.FillRule.NonZero, ToMatrix3x2(Transform), velloBrush, brushTransform);
        }

        if (pen is not null && TryCreateStroke(pen, rect, out var strokeStyle, out var strokeBrush, out var strokeBrushTransform))
        {
            _scene.StrokePath(builder, strokeStyle, ToMatrix3x2(Transform), strokeBrush, strokeBrushTransform);
        }
    }

    public void DrawGlyphRun(IBrush? foreground, IGlyphRunImpl glyphRun)
    {
        EnsureNotDisposed();

        if (glyphRun is not VelloGlyphRunImpl velloGlyphRun)
        {
            throw new NotSupportedException("Glyph run implementation is not compatible with the Vello renderer.");
        }

        var bounds = glyphRun.Bounds;

        if (foreground is null || !TryCreateBrush(foreground, bounds, out var velloBrush, out _))
        {
            return;
        }

        var font = VelloFontManager.GetFont(velloGlyphRun.GlyphTypeface);
        var glyphs = velloGlyphRun.GlyphsSpan;
        if (glyphs.IsEmpty)
        {
            return;
        }

        var simulations = (velloGlyphRun.GlyphTypeface as VelloGlyphTypeface)?.FontSimulations ?? FontSimulations.None;

        var transform = Matrix3x2.CreateTranslation(
                (float)velloGlyphRun.BaselineOrigin.X,
                (float)velloGlyphRun.BaselineOrigin.Y)
            * ToMatrix3x2(Transform);

        var options = new GlyphRunOptions
        {
            FontSize = (float)velloGlyphRun.FontRenderingEmSize,
            Brush = velloBrush,
            Transform = transform,
            BrushAlpha = 1f,
            Hint = ShouldHintText(),
            Style = GlyphRunStyle.Fill,
        };

        if (simulations.HasFlag(FontSimulations.Oblique))
        {
            var skew = Matrix3x2.CreateSkew(VelloGlyphTypeface.FauxItalicSkew, 0);
            options.GlyphTransform = options.GlyphTransform.HasValue
                ? options.GlyphTransform.Value * skew
                : skew;
        }

        _scene.DrawGlyphRun(font, glyphs, options);

        if (simulations.HasFlag(FontSimulations.Bold))
        {
            var strokeWidth = Math.Max(1f, options.FontSize * (float)VelloGlyphTypeface.FauxBoldStrokeScale);
            var strokeOptions = new GlyphRunOptions
            {
                FontSize = options.FontSize,
                Brush = options.Brush,
                BrushAlpha = options.BrushAlpha,
                Transform = options.Transform,
                GlyphTransform = options.GlyphTransform,
                Hint = options.Hint,
                Style = GlyphRunStyle.Stroke,
                Stroke = new StrokeStyle
                {
                    Width = strokeWidth,
                    StartCap = LineCap.Butt,
                    EndCap = LineCap.Butt,
                    LineJoin = LineJoin.Miter,
                },
            };

            _scene.DrawGlyphRun(font, glyphs, strokeOptions);
        }
    }

    public IDrawingContextLayerImpl CreateLayer(PixelSize size) =>
        new VelloOffscreenRenderTarget(size, s_intermediateDpi, _options, _graphicsDeviceProvider);

    public void PushClip(Rect clip)
    {
        _clipDepth++;

        if (TrySkipClip())
        {
            return;
        }

        if (clip.Width <= 0 || clip.Height <= 0)
        {
            _layerStack.Push(LayerEntry.Noop());
            return;
        }

        var builder = new PathBuilder();
        builder.AddRectangle(clip);
        PushSceneLayer(builder, 1f, s_clipLayerBlend, ToMatrix3x2(Transform));
    }

    public void PushClip(RoundedRect clip)
    {
        _clipDepth++;

        if (TrySkipClip())
        {
            return;
        }

        if (clip.Rect.Width <= 0 || clip.Rect.Height <= 0)
        {
            _layerStack.Push(LayerEntry.Noop());
            return;
        }

        var builder = new PathBuilder();
        builder.AddRoundedRectangle(clip);
        PushSceneLayer(builder, 1f, s_clipLayerBlend, ToMatrix3x2(Transform));
    }

    public void PushClip(IPlatformRenderInterfaceRegion region)
    {
        _clipDepth++;

        if (TrySkipClip())
        {
            return;
        }

        if (region is not VelloRegionImpl velloRegion || velloRegion.IsEmpty)
        {
            _layerStack.Push(LayerEntry.Noop());
            return;
        }

        var builder = new PathBuilder();
        foreach (var rect in velloRegion.Rects)
        {
            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0)
            {
                continue;
            }

            builder.AddRectangle(new Rect(rect.Left, rect.Top, width, height));
        }

        if (builder.Count == 0)
        {
            _layerStack.Push(LayerEntry.Noop());
            return;
        }

        PushSceneLayer(builder, 1f, s_clipLayerBlend, Matrix3x2.Identity);
    }

    public void PopClip()
    {
        PopLayer(ref _clipDepth);
    }

    public void PushLayer(Rect bounds)
    {
        _layerDepth++;

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            _layerStack.Push(LayerEntry.Noop());
            return;
        }

        var builder = new PathBuilder();
        builder.AddRectangle(bounds);
        PushSceneLayer(builder, 1f, s_defaultLayerBlend, ToMatrix3x2(Transform));
    }

    public void PopLayer()
    {
        PopLayer(ref _layerDepth);
    }

    public void PushOpacity(double opacity, Rect? bounds)
    {
        _opacityDepth++;

        var alpha = (float)Math.Clamp(opacity, 0.0, 1.0);
        if (alpha <= 0f)
        {
            _layerStack.Push(LayerEntry.Noop());
            return;
        }

        var pathBounds = bounds ?? new Rect(0, 0, Math.Max(1, _targetSize.Width), Math.Max(1, _targetSize.Height));
        if (pathBounds.Width <= 0 || pathBounds.Height <= 0)
        {
            _layerStack.Push(LayerEntry.Noop());
            return;
        }

        var builder = new PathBuilder();
        builder.AddRectangle(pathBounds);

        var transform = bounds.HasValue ? ToMatrix3x2(Transform) : Matrix3x2.Identity;
        PushSceneLayer(builder, alpha, s_defaultLayerBlend, transform);
    }

    public void PopOpacity()
    {
        PopLayer(ref _opacityDepth);
    }

    public void PushOpacityMask(IBrush mask, Rect bounds)
    {
        _opacityDepth++;

        byte entriesPushed = 0;

        if (mask is null || bounds.Width <= 0 || bounds.Height <= 0)
        {
            _layerStack.Push(LayerEntry.Noop());
            _opacityMaskLayerEntries.Push(1);
            return;
        }

        if (!TryCreateBrush(mask, bounds, out var maskBrush, out var maskTransform))
        {
            _layerStack.Push(LayerEntry.Noop());
            _opacityMaskLayerEntries.Push(1);
            return;
        }

        var transform = ToMatrix3x2(Transform);

        var maskPath = CreateRectanglePath(bounds);
        if (maskPath.Count == 0)
        {
            _layerStack.Push(LayerEntry.Noop());
            _opacityMaskLayerEntries.Push(1);
            return;
        }

        _scene.PushLuminanceMaskLayer(maskPath, transform, 1f);
        _layerStack.Push(LayerEntry.Scene());
        entriesPushed++;

        var maskFillPath = CreateRectanglePath(bounds);
        _scene.FillPath(maskFillPath, VelloSharp.FillRule.NonZero, transform, maskBrush, maskTransform);

        var contentPath = CreateRectanglePath(bounds);
        PushSceneLayer(contentPath, 1f, s_defaultLayerBlend, transform);
        entriesPushed++;

        _opacityMaskLayerEntries.Push(entriesPushed);
    }

    public void PopOpacityMask()
    {
        if (_opacityDepth > 0)
        {
            _opacityDepth--;
        }

        var entriesToPop = _opacityMaskLayerEntries.Count > 0
            ? _opacityMaskLayerEntries.Pop()
            : (byte)0;

        for (var i = 0; i < entriesToPop; i++)
        {
            PopLayerEntry();
        }
    }

    public void PushGeometryClip(IGeometryImpl clip)
    {
        _clipDepth++;

        if (TrySkipClip())
        {
            return;
        }

        if (clip is not VelloGeometryImplBase geometry)
        {
            _layerStack.Push(LayerEntry.Noop());
            return;
        }

        var builder = BuildPath(geometry);
        if (builder.Count == 0)
        {
            _layerStack.Push(LayerEntry.Noop());
            return;
        }

        PushSceneLayer(builder, 1f, s_clipLayerBlend, ToMatrix3x2(Transform));
    }

    public void PopGeometryClip()
    {
        PopLayer(ref _clipDepth);
    }

    public void PushRenderOptions(RenderOptions renderOptions)
    {
        _renderOptionsStack.Push(_renderOptions);
        _renderOptions = _renderOptions.MergeWith(renderOptions);
    }

    public void PopRenderOptions()
    {
        if (_renderOptionsStack.Count > 0)
        {
            _renderOptions = _renderOptionsStack.Pop();
        }
    }

    public object? GetFeature(Type t)
    {
        if (t == typeof(IVelloApiLeaseFeature))
        {
            _leaseFeature ??= new VelloLeaseFeature(this);
            return _leaseFeature;
        }

        return null;
    }

    private void BeginApiLease()
    {
        EnsureNotDisposed();

        if (_apiLeaseActive)
        {
            throw new InvalidOperationException("The Vello API is already leased.");
        }

        _apiLeaseActive = true;
    }

    private void EndApiLease()
    {
        _apiLeaseActive = false;
        _platformGraphicsLeaseActive = false;
    }

    private IVelloPlatformGraphicsLease? TryCreatePlatformGraphicsLease(Action onLeaseDisposed)
    {
        if (_graphicsDeviceProvider is null)
        {
            return null;
        }

        if (_platformGraphicsLeaseActive)
        {
            throw new InvalidOperationException("The Vello platform graphics API is already leased.");
        }

        var deviceOptions = CreateDeviceOptionsForLease();
        var deviceLease = _graphicsDeviceProvider.Acquire(deviceOptions);
        if (!deviceLease.TryGetWgpuResources(out var resources))
        {
            return null;
        }

        _platformGraphicsLeaseActive = true;
        return new VelloPlatformGraphicsLease(this, onLeaseDisposed, resources.Instance, resources.Adapter, resources.Device, resources.Queue, resources.Renderer);
    }

    private sealed class VelloLeaseFeature : IVelloApiLeaseFeature
    {
        private readonly VelloDrawingContextImpl _context;

        public VelloLeaseFeature(VelloDrawingContextImpl context)
        {
            _context = context;
        }

        public IVelloApiLease Lease()
        {
            _context.BeginApiLease();
            return new ApiLease(_context);
        }

        private sealed class ApiLease : IVelloApiLease
        {
            private readonly VelloDrawingContextImpl _context;
            private bool _disposed;
            private IVelloPlatformGraphicsLease? _platformLease;

            public ApiLease(VelloDrawingContextImpl context)
            {
                _context = context;
            }

            private void EnsureNotDisposed()
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(ApiLease));
                }
            }

            public Scene Scene
            {
                get
                {
                    EnsureNotDisposed();
                    return _context.Scene;
                }
            }

            public RenderParams RenderParams
            {
                get
                {
                    EnsureNotDisposed();
                    return _context.RenderParams;
                }
            }

            public Matrix Transform
            {
                get
                {
                    EnsureNotDisposed();
                    return _context.Transform;
                }
            }

            public IVelloPlatformGraphicsLease? TryLeasePlatformGraphics()
            {
                EnsureNotDisposed();

                _platformLease ??= _context.TryCreatePlatformGraphicsLease(OnPlatformLeaseDisposed);
                return _platformLease;
            }

            public void ScheduleWgpuSurfaceRender(Action<WgpuSurfaceRenderContext> renderAction)
            {
                EnsureNotDisposed();
                _context.ScheduleWgpuSurfaceRender(renderAction);
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _platformLease?.Dispose();
                _platformLease = null;
                _context.EndApiLease();
                _disposed = true;
            }

            private void OnPlatformLeaseDisposed()
            {
                _platformLease = null;
            }
        }
    }

    private sealed class VelloPlatformGraphicsLease : IVelloPlatformGraphicsLease
    {
        private readonly VelloDrawingContextImpl _context;
        private readonly Action _onDispose;
        private bool _disposed;

        public VelloPlatformGraphicsLease(
            VelloDrawingContextImpl context,
            Action onDispose,
            WgpuInstance instance,
            WgpuAdapter adapter,
            WgpuDevice device,
            WgpuQueue queue,
            WgpuRenderer renderer)
        {
            _context = context;
            _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
            Instance = instance;
            Adapter = adapter;
            Device = device;
            Queue = queue;
            Renderer = renderer;
        }

        public WgpuInstance Instance { get; }

        public WgpuAdapter Adapter { get; }

        public WgpuDevice Device { get; }

        public WgpuQueue Queue { get; }

        public WgpuRenderer Renderer { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _context._platformGraphicsLeaseActive = false;
            _disposed = true;
            _onDispose();
        }
    }

    private GraphicsDeviceOptions CreateDeviceOptionsForLease()
    {
        var rendererOptions = _options.RendererOptions;
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
            new GraphicsPresentationOptions(_options.PresentMode, _options.ClearColor, _options.FramesPerSecond),
            rendererOptions);
    }

    private void ApplyStroke(PathBuilder builder, StrokeStyle style, VelloSharp.Brush brush, Matrix3x2? brushTransform)
    {
        _scene.StrokePath(builder, style, ToMatrix3x2(Transform), brush, brushTransform);
    }

    private void DrawBoxShadows(RoundedRect rect, BoxShadows boxShadows)
    {
        foreach (var shadow in boxShadows)
        {
            if (shadow == default || shadow.Color.A == 0)
            {
                continue;
            }

            if (shadow.IsInset)
            {
                DrawInsetShadow(rect, shadow);
            }
            else
            {
                DrawDropShadow(rect, shadow);
            }
        }
    }

    private void DrawDropShadow(RoundedRect rect, BoxShadow shadow)
    {
        var shadowRect = shadow.Spread != 0 ? rect.Inflate(shadow.Spread, shadow.Spread) : rect;
        shadowRect = TranslateRoundedRect(shadowRect, new global::Avalonia.Vector(shadow.OffsetX, shadow.OffsetY));

        var targetRect = shadowRect.Rect;
        if (targetRect.Width <= 0 || targetRect.Height <= 0)
        {
            return;
        }

        var stdDev = CalculateBlurStdDeviation(shadow.Blur);
        var clipBounds = ExpandRectForBlur(shadowRect.Rect, stdDev);
        if (clipBounds.Width <= 0 || clipBounds.Height <= 0)
        {
            return;
        }

        var transform = ToMatrix3x2(Transform);

        var clipPath = CreateRectanglePath(clipBounds);
        PushSceneLayer(clipPath, 1f, s_defaultLayerBlend, transform);
        try
        {
            var color = ToRgbaColor(shadow.Color, 1.0);
            if (color.A <= 0f)
            {
                return;
            }

            var radius = CalculateUniformCornerRadius(shadowRect);
            var origin = new Vector2((float)targetRect.X, (float)targetRect.Y);
            var size = new Vector2(
                (float)Math.Max(0, targetRect.Width),
                (float)Math.Max(0, targetRect.Height));

            _scene.DrawBlurredRoundedRect(origin, size, transform, color, radius, stdDev);

            var subtractPath = CreateRoundedRectPath(rect);
            PushSceneLayer(subtractPath, 1f, s_destOutLayerBlend, transform);
            try
            {
                _scene.FillPath(subtractPath, VelloSharp.FillRule.NonZero, transform, s_opaqueWhiteBrush);
            }
            finally
            {
                PopLayerEntry();
            }
        }
        finally
        {
            PopLayerEntry();
        }
    }

    private void DrawInsetShadow(RoundedRect rect, BoxShadow shadow)
    {
        var inner = shadow.Spread != 0 ? rect.Deflate(shadow.Spread, shadow.Spread) : rect;
        inner = TranslateRoundedRect(inner, new global::Avalonia.Vector(shadow.OffsetX, shadow.OffsetY));

        var innerRect = inner.Rect;
        if (innerRect.Width <= 0 || innerRect.Height <= 0)
        {
            return;
        }

        var color = ToRgbaColor(shadow.Color, 1.0);
        if (color.A <= 0f)
        {
            return;
        }

        var stdDev = CalculateBlurStdDeviation(shadow.Blur);
        var transform = ToMatrix3x2(Transform);

        var clipPath = CreateRoundedRectPath(rect);
        PushSceneLayer(clipPath, 1f, s_defaultLayerBlend, transform);
        try
        {
            var radius = CalculateUniformCornerRadius(inner);
            var origin = new Vector2((float)innerRect.X, (float)innerRect.Y);
            var size = new Vector2(
                (float)Math.Max(0, innerRect.Width),
                (float)Math.Max(0, innerRect.Height));

            _scene.DrawBlurredRoundedRect(origin, size, transform, color, radius, stdDev);

            var subtractPath = CreateRoundedRectPath(inner);
            PushSceneLayer(subtractPath, 1f, s_destOutLayerBlend, transform);
            try
            {
                _scene.FillPath(subtractPath, VelloSharp.FillRule.NonZero, transform, s_opaqueWhiteBrush);
            }
            finally
            {
                PopLayerEntry();
            }
        }
        finally
        {
            PopLayerEntry();
        }
    }

    private static PathBuilder BuildPath(VelloGeometryImplBase geometry)
    {
        var builder = new PathBuilder();
        foreach (var command in geometry.GetCommandsSnapshot())
        {
            switch (command.Verb)
            {
                case VelloPathVerb.MoveTo:
                    builder.MoveTo(command.X0, command.Y0);
                    break;
                case VelloPathVerb.LineTo:
                    builder.LineTo(command.X0, command.Y0);
                    break;
                case VelloPathVerb.QuadTo:
                    builder.QuadraticTo(command.X0, command.Y0, command.X1, command.Y1);
                    break;
                case VelloPathVerb.CubicTo:
                    builder.CubicTo(command.X0, command.Y0, command.X1, command.Y1, command.X2, command.Y2);
                    break;
                case VelloPathVerb.Close:
                    builder.Close();
                    break;
            }
        }
        return builder;
    }

    private bool TryCreateBrush(IBrush brush, Rect? targetBounds, out VelloSharp.Brush velloBrush, out Matrix3x2? brushTransform)
    {
        brushTransform = null;

        switch (brush)
        {
            case ISolidColorBrush solid:
                velloBrush = new VelloSharp.SolidColorBrush(ToRgbaColor(solid.Color, solid.Opacity));
                return true;
            case IGradientBrush gradient when gradient.GradientStops is { Count: > 0 }:
                return TryCreateGradientBrush(gradient, targetBounds, out velloBrush, out brushTransform);
            case IImageBrush imageBrush:
                return TryCreateImageBrush(imageBrush, targetBounds, out velloBrush, out brushTransform);
            case ISceneBrush sceneBrush:
            {
                using var content = sceneBrush.CreateContent();
                if (content is null)
                {
                    break;
                }

                return TryCreateSceneBrush(content, targetBounds, out velloBrush, out brushTransform);
            }
            case ISceneBrushContent sceneBrushContent:
                return TryCreateSceneBrush(sceneBrushContent, targetBounds, out velloBrush, out brushTransform);
        }

        velloBrush = new VelloSharp.SolidColorBrush(ToRgbaColor(Colors.Transparent, 0));
        return false;
    }

    private bool TryCreateStroke(IPen pen, Rect? targetBounds, out StrokeStyle strokeStyle, out VelloSharp.Brush strokeBrush, out Matrix3x2? brushTransform)
    {
        brushTransform = null;
        strokeBrush = null!;

        if (pen.Brush is null || !TryCreateBrush(pen.Brush, targetBounds, out var fillBrush, out var transform))
        {
            strokeStyle = default!;
            return false;
        }

        var style = new StrokeStyle
        {
            Width = pen.Thickness,
            MiterLimit = pen.MiterLimit,
            StartCap = ConvertLineCap(pen.LineCap),
            EndCap = ConvertLineCap(pen.LineCap),
            LineJoin = ConvertLineJoin(pen.LineJoin),
            DashPhase = pen.DashStyle?.Offset ?? 0,
            DashPattern = pen.DashStyle?.Dashes is { Count: > 0 } dashes ? dashes.ToArray() : null,
        };

        strokeStyle = style;
        strokeBrush = fillBrush;
        brushTransform = transform;
        return true;
    }

    private static RgbaColor ToRgbaColor(Color color, double opacity)
    {
        var alpha = (float)(color.A / 255.0 * opacity);
        var r = (float)(color.R / 255.0);
        var g = (float)(color.G / 255.0);
        var b = (float)(color.B / 255.0);
        return new RgbaColor(r, g, b, alpha);
    }

    private void PushSceneLayer(PathBuilder path, float alpha, LayerBlend blend, Matrix3x2 transform)
    {
        if (path.Count == 0)
        {
            _layerStack.Push(LayerEntry.Noop());
            return;
        }

        _scene.PushLayer(path, blend, transform, alpha);
        _layerStack.Push(LayerEntry.Scene());
    }

    private void PopLayer(ref int counter)
    {
        if (counter > 0)
        {
            counter--;
        }

        var entry = _layerStack.Pop();
        if (entry.HasLayer)
        {
            _scene.PopLayer();
        }
    }

    private void PopLayerEntry()
    {
        if (_layerStack.Count == 0)
        {
            return;
        }

        var entry = _layerStack.Pop();
        if (entry.HasLayer)
        {
            _scene.PopLayer();
        }
    }

    private static PathBuilder CreateRectanglePath(Rect rect)
    {
        var builder = new PathBuilder();
        AppendRectangle(builder, rect);
        return builder;
    }

    private static PathBuilder CreateRoundedRectPath(RoundedRect rect)
    {
        var builder = new PathBuilder();
        builder.AddRoundedRectangle(rect);
        return builder;
    }

    private BitmapInterpolationMode GetEffectiveBitmapInterpolationMode()
    {
        var mode = _renderOptions.BitmapInterpolationMode;
        return mode == BitmapInterpolationMode.Unspecified ? BitmapInterpolationMode.HighQuality : mode;
    }

    private BitmapBlendingMode GetEffectiveBitmapBlendingMode()
    {
        var mode = _renderOptions.BitmapBlendingMode;
        return mode == BitmapBlendingMode.Unspecified ? BitmapBlendingMode.SourceOver : mode;
    }

    private static ImageQuality ToImageQuality(BitmapInterpolationMode interpolationMode)
    {
        return interpolationMode switch
        {
            BitmapInterpolationMode.None => ImageQuality.Low,
            BitmapInterpolationMode.LowQuality => ImageQuality.Low,
            BitmapInterpolationMode.MediumQuality => ImageQuality.Medium,
            BitmapInterpolationMode.HighQuality => ImageQuality.High,
            _ => ImageQuality.High,
        };
    }

    private (LayerBlend? Blend, bool Skip) ResolveBitmapBlendingMode(BitmapBlendingMode mode)
    {
        switch (mode)
        {
            case BitmapBlendingMode.SourceOver:
                return (null, false);
            case BitmapBlendingMode.Source:
                return (new LayerBlend(LayerMix.Normal, LayerCompose.Copy), false);
            case BitmapBlendingMode.Destination:
                return (null, true);
            case BitmapBlendingMode.DestinationOver:
                return (new LayerBlend(LayerMix.Normal, LayerCompose.DestOver), false);
            case BitmapBlendingMode.SourceIn:
                return (new LayerBlend(LayerMix.Normal, LayerCompose.SrcIn), false);
            case BitmapBlendingMode.DestinationIn:
                return (new LayerBlend(LayerMix.Normal, LayerCompose.DestIn), false);
            case BitmapBlendingMode.SourceOut:
                return (new LayerBlend(LayerMix.Normal, LayerCompose.SrcOut), false);
            case BitmapBlendingMode.DestinationOut:
                return (new LayerBlend(LayerMix.Normal, LayerCompose.DestOut), false);
            case BitmapBlendingMode.SourceAtop:
                return (new LayerBlend(LayerMix.Normal, LayerCompose.SrcAtop), false);
            case BitmapBlendingMode.DestinationAtop:
                return (new LayerBlend(LayerMix.Normal, LayerCompose.DestAtop), false);
            case BitmapBlendingMode.Xor:
                return (new LayerBlend(LayerMix.Normal, LayerCompose.Xor), false);
            case BitmapBlendingMode.Plus:
                return (new LayerBlend(LayerMix.Normal, LayerCompose.Plus), false);
            case BitmapBlendingMode.Screen:
                return (new LayerBlend(LayerMix.Screen, LayerCompose.SrcOver), false);
            case BitmapBlendingMode.Overlay:
                return (new LayerBlend(LayerMix.Overlay, LayerCompose.SrcOver), false);
            case BitmapBlendingMode.Darken:
                return (new LayerBlend(LayerMix.Darken, LayerCompose.SrcOver), false);
            case BitmapBlendingMode.Lighten:
                return (new LayerBlend(LayerMix.Lighten, LayerCompose.SrcOver), false);
            case BitmapBlendingMode.ColorDodge:
                return (new LayerBlend(LayerMix.ColorDodge, LayerCompose.SrcOver), false);
            case BitmapBlendingMode.ColorBurn:
                return (new LayerBlend(LayerMix.ColorBurn, LayerCompose.SrcOver), false);
            case BitmapBlendingMode.HardLight:
                return (new LayerBlend(LayerMix.HardLight, LayerCompose.SrcOver), false);
            case BitmapBlendingMode.SoftLight:
                return (new LayerBlend(LayerMix.SoftLight, LayerCompose.SrcOver), false);
            case BitmapBlendingMode.Difference:
                return (new LayerBlend(LayerMix.Difference, LayerCompose.SrcOver), false);
            case BitmapBlendingMode.Exclusion:
                return (new LayerBlend(LayerMix.Exclusion, LayerCompose.SrcOver), false);
            case BitmapBlendingMode.Multiply:
                return (new LayerBlend(LayerMix.Multiply, LayerCompose.SrcOver), false);
            case BitmapBlendingMode.Hue:
                return (new LayerBlend(LayerMix.Hue, LayerCompose.SrcOver), false);
            case BitmapBlendingMode.Saturation:
                return (new LayerBlend(LayerMix.Saturation, LayerCompose.SrcOver), false);
            case BitmapBlendingMode.Color:
                return (new LayerBlend(LayerMix.Color, LayerCompose.SrcOver), false);
            case BitmapBlendingMode.Luminosity:
                return (new LayerBlend(LayerMix.Luminosity, LayerCompose.SrcOver), false);
            default:
                return (null, false);
        }
    }

    private bool ShouldHintText()
    {
        var mode = _renderOptions.TextRenderingMode;
        if (mode == TextRenderingMode.Alias)
        {
            return true;
        }

        return false;
    }


    private static void AppendRectangle(PathBuilder builder, Rect rect)
    {
        builder.MoveTo(rect.Left, rect.Top)
            .LineTo(rect.Right, rect.Top)
            .LineTo(rect.Right, rect.Bottom)
            .LineTo(rect.Left, rect.Bottom)
            .Close();
    }

    private static RoundedRect TranslateRoundedRect(RoundedRect rect, global::Avalonia.Vector offset)
    {
        if (offset == default)
        {
            return rect;
        }

        var translatedRect = new Rect(rect.Rect.Position + offset, rect.Rect.Size);
        return new RoundedRect(
            translatedRect,
            rect.RadiiTopLeft,
            rect.RadiiTopRight,
            rect.RadiiBottomRight,
            rect.RadiiBottomLeft);
    }

    private static double CalculateUniformCornerRadius(RoundedRect rect)
    {
        double sum = 0;
        var count = 0;

        static double GetCornerRadius(global::Avalonia.Vector radii)
        {
            return Math.Min(Math.Abs(radii.X), Math.Abs(radii.Y));
        }

        void Accumulate(global::Avalonia.Vector radii)
        {
            var radius = GetCornerRadius(radii);
            if (radius > 0)
            {
                sum += radius;
                count++;
            }
        }

        Accumulate(rect.RadiiTopLeft);
        Accumulate(rect.RadiiTopRight);
        Accumulate(rect.RadiiBottomRight);
        Accumulate(rect.RadiiBottomLeft);

        return count > 0 ? sum / count : 0;
    }

    private static double CalculateBlurStdDeviation(double blur)
    {
        if (blur <= 0)
        {
            return 0;
        }

        return blur / Math.Sqrt(2.0);
    }

    private static Rect ExpandRectForBlur(Rect rect, double stdDev)
    {
        if (stdDev <= 0)
        {
            return rect;
        }

        var extent = stdDev * 3.0;
        if (extent <= 0)
        {
            return rect;
        }

        return rect.Inflate(extent);
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VelloDrawingContextImpl));
        }

        if (_apiLeaseActive)
        {
            throw new InvalidOperationException("The Vello API is currently leased.");
        }
    }

    private bool TrySkipClip()
    {
        if (_skipInitialClip && _clipDepth == 1)
        {
            _skipInitialClip = false;
            _layerStack.Push(LayerEntry.Noop());
            return true;
        }

        return false;
    }

    private static LineCap ConvertLineCap(PenLineCap cap)
    {
        return cap switch
        {
            PenLineCap.Round => LineCap.Round,
            PenLineCap.Square => LineCap.Square,
            _ => LineCap.Butt,
        };
    }

    private static LineJoin ConvertLineJoin(PenLineJoin join)
    {
        return join switch
        {
            PenLineJoin.Round => LineJoin.Round,
            PenLineJoin.Bevel => LineJoin.Bevel,
            _ => LineJoin.Miter,
        };
    }

    private static Matrix3x2 ToMatrix3x2(Matrix matrix)
    {
        return new Matrix3x2(
            (float)matrix.M11,
            (float)matrix.M12,
            (float)matrix.M21,
            (float)matrix.M22,
            (float)matrix.M31,
            (float)matrix.M32);
    }

    private static VelloSharp.FillRule ToVelloFillRule(global::Avalonia.Media.FillRule fillRule)
        {
            return fillRule == global::Avalonia.Media.FillRule.EvenOdd
                ? VelloSharp.FillRule.EvenOdd
                : VelloSharp.FillRule.NonZero;
        }

    private readonly struct LayerEntry
    {
        private LayerEntry(bool hasLayer) => HasLayer = hasLayer;

        public bool HasLayer { get; }

        public static LayerEntry Scene() => new(true);
        public static LayerEntry Noop() => new(false);
    }

    private bool TryCreateGradientBrush(IGradientBrush gradient, Rect? boundsHint, out VelloSharp.Brush brush, out Matrix3x2? transform)
    {
        var bounds = ResolveBounds(boundsHint);

        var stops = CreateGradientStops(gradient);
        if (stops.Length == 0)
        {
            brush = new VelloSharp.SolidColorBrush(ToRgbaColor(Colors.Transparent, 0));
            transform = null;
            return false;
        }

        var extend = ToExtendMode(gradient.SpreadMethod);

        switch (gradient)
        {
            case ILinearGradientBrush linear:
            {
                var start = linear.StartPoint.ToPixels(bounds);
                var end = linear.EndPoint.ToPixels(bounds);

                if (MathUtilities.IsZero(end.X - start.X) && MathUtilities.IsZero(end.Y - start.Y))
                {
                    brush = new VelloSharp.SolidColorBrush(stops[^1].Color);
                    transform = null;
                    return true;
                }

                brush = new VelloSharp.LinearGradientBrush(ToVector2(start), ToVector2(end), stops, extend);
                var matrix = ComposeBrushTransform(bounds, linear.Transform, linear.TransformOrigin);
                transform = ToMatrix3x2Nullable(matrix);
                return true;
            }

            case IRadialGradientBrush radial:
            {
                var centerPoint = radial.Center.ToPixels(bounds);
                var originPoint = radial.GradientOrigin.ToPixels(bounds);
                var radiusX = radial.RadiusX.ToValue(bounds.Width);
                var radiusY = radial.RadiusY.ToValue(bounds.Height);

                if (MathUtilities.IsZero(radiusX) || MathUtilities.IsZero(radiusY))
                {
                    brush = new VelloSharp.SolidColorBrush(stops[^1].Color);
                    transform = null;
                    return true;
                }

                var startCenter = ToVector2(originPoint);
                var endCenter = ToVector2(centerPoint);
                var startRadius = 0f;
                var endRadius = (float)radiusX;

                Matrix? transformMatrix = null;

                if (!MathUtilities.IsZero(radiusY - radiusX))
                {
                    var translateToCenter = Matrix.CreateTranslation(-centerPoint.X, -centerPoint.Y);
                    var scale = Matrix.CreateScale(1, radiusY / radiusX);
                    var translateBack = Matrix.CreateTranslation(centerPoint.X, centerPoint.Y);
                    transformMatrix = translateToCenter * scale * translateBack;
                }

                var extra = ComposeBrushTransform(bounds, radial.Transform, radial.TransformOrigin);
                if (extra is { })
                {
                    transformMatrix = transformMatrix.HasValue ? transformMatrix.Value * extra.Value : extra;
                }

                transform = ToMatrix3x2Nullable(transformMatrix);
                brush = new VelloSharp.RadialGradientBrush(startCenter, startRadius, endCenter, endRadius, stops, extend);
                return true;
            }
        }

        brush = new VelloSharp.SolidColorBrush(stops[^1].Color);
        transform = null;
        return true;
    }

    private bool TryCreateImageBrush(IImageBrush brush, Rect? boundsHint, out VelloSharp.Brush velloBrush, out Matrix3x2? brushTransform)
    {
        velloBrush = null!;
        brushTransform = null;

        using var bitmapRef = TryGetBitmapReference(brush.Source);
        if (bitmapRef.BitmapImpl is not VelloBitmapImpl bitmap)
        {
            return false;
        }

        var bounds = ResolveBounds(boundsHint);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        var bitmapSize = bitmap.PixelSize.ToSizeWithDpi(bitmap.Dpi);
        if (bitmapSize.Width <= 0 || bitmapSize.Height <= 0)
        {
            return false;
        }

        return TryCreateTileBrush(
            brush,
            bitmapSize,
            bounds,
            calc => RenderTileBitmap(calc, bitmap),
            out velloBrush,
            out brushTransform);
    }

    private bool TryCreateSceneBrush(
        ISceneBrushContent content,
        Rect? boundsHint,
        out VelloSharp.Brush velloBrush,
        out Matrix3x2? brushTransform)
    {
        velloBrush = null!;
        brushTransform = null;

        var tileBrush = content.Brush;
        if (tileBrush is null)
        {
            return false;
        }

        var bounds = ResolveBounds(boundsHint);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        var contentSize = content.Rect.Size;
        if (contentSize.Width <= 0 || contentSize.Height <= 0)
        {
            return false;
        }

        return TryCreateTileBrush(
            tileBrush,
            contentSize,
            bounds,
            calc => RenderTileSceneContent(calc, content),
            out velloBrush,
            out brushTransform);
    }

    private bool TryCreateTileBrush(
        ITileBrush tileBrush,
        Size contentSize,
        Rect targetBounds,
        Func<TileBrushInfo, Image?> imageFactory,
        out VelloSharp.Brush velloBrush,
        out Matrix3x2? brushTransform)
    {
        velloBrush = null!;
        brushTransform = null;

        if (contentSize.Width <= 0 || contentSize.Height <= 0)
        {
            return false;
        }

        var tileInfo = CreateTileBrushInfo(tileBrush, contentSize, targetBounds.Size);

        if (tileInfo.IntermediateSize.Width <= 0 || tileInfo.IntermediateSize.Height <= 0)
        {
            return false;
        }

        var image = imageFactory(tileInfo);
        if (image is null)
        {
            return false;
        }

        _deferredDisposables.Add(image);

        var (extendX, extendY) = ToExtendModes(tileBrush.TileMode);
        var opacity = (float)Math.Clamp(tileBrush.Opacity, 0.0, 1.0);
        var imageBrush = new VelloSharp.ImageBrush(image)
        {
            Alpha = opacity,
            Quality = ImageQuality.High,
            XExtend = extendX,
            YExtend = extendY,
        };

        var matrix = ComputeTileBrushTransform(tileInfo, tileBrush, targetBounds);
        brushTransform = ToMatrix3x2(matrix);
        velloBrush = imageBrush;
        return true;
    }

    private Image? RenderTileBitmap(TileBrushInfo tileInfo, VelloBitmapImpl bitmap)
    {
        var intermediatePixelSize = PixelSize.FromSizeWithDpi(tileInfo.IntermediateSize, s_intermediateDpi);
        if (intermediatePixelSize.Width <= 0 || intermediatePixelSize.Height <= 0)
        {
            return null;
        }

        var sourceRect = new Rect(bitmap.PixelSize.ToSizeWithDpi(new global::Avalonia.Vector(96, 96)));
        var targetRect = new Rect(bitmap.PixelSize.ToSizeWithDpi(s_intermediateDpi));

        return RenderToImage(intermediatePixelSize, context =>
        {
            context.Clear(Colors.Transparent);
            context.PushClip(tileInfo.IntermediateClip);

            var originalTransform = context.Transform;
            context.Transform = tileInfo.IntermediateTransform;

            context.DrawBitmap(bitmap, 1.0, sourceRect, targetRect);

            context.Transform = originalTransform;
            context.PopClip();
        });
    }

    private Image? RenderTileSceneContent(TileBrushInfo tileInfo, ISceneBrushContent content)
    {
        var intermediatePixelSize = PixelSize.FromSizeWithDpi(tileInfo.IntermediateSize, s_intermediateDpi);
        if (intermediatePixelSize.Width <= 0 || intermediatePixelSize.Height <= 0)
        {
            return null;
        }

        var rect = content.Rect;
        var contentTransform = rect.TopLeft == default
            ? (Matrix?)null
            : Matrix.CreateTranslation(-rect.X, -rect.Y);

        return RenderToImage(intermediatePixelSize, context =>
        {
            context.Clear(Colors.Transparent);
            context.PushClip(tileInfo.IntermediateClip);

            var originalTransform = context.Transform;
            context.Transform = tileInfo.IntermediateTransform;

            content.Render(context, contentTransform);

            context.Transform = originalTransform;
            context.PopClip();
        });
    }

    private Image? RenderToImage(PixelSize pixelSize, Action<VelloDrawingContextImpl> render)
    {
        if (pixelSize.Width <= 0 || pixelSize.Height <= 0)
        {
            return null;
        }

        using var scene = new Scene();
        RenderParams? renderParams = null;

        using (var context = new VelloDrawingContextImpl(
                   scene,
                   pixelSize,
                   _options,
                   ctx => renderParams = ctx.RenderParams,
                   skipInitialClip: true))
        {
            render(context);
        }

        if (renderParams is null)
        {
            return null;
        }

        var parameters = renderParams.Value with
        {
            BaseColor = RgbaColor.FromBytes(0, 0, 0, 0),
            Format = RenderFormat.Rgba8,
        };

        if (parameters.Width == 0 || parameters.Height == 0)
        {
            return null;
        }

        var stride = checked((int)parameters.Width * 4);
        var buffer = new byte[checked((int)parameters.Height * stride)];

        var rendererOptions = CreateOffscreenRendererOptions();
        using var renderer = new Renderer(parameters.Width, parameters.Height, rendererOptions);
        renderer.Render(scene, parameters, buffer, stride);

        return Image.FromPixels(
            buffer,
            (int)parameters.Width,
            (int)parameters.Height,
            RenderFormat.Rgba8,
            ImageAlphaMode.Straight,
            stride);
    }

    private RendererOptions CreateOffscreenRendererOptions()
    {
        var options = _options.RendererOptions;
        return new RendererOptions(
            useCpu: true,
            supportArea: options.SupportArea,
            supportMsaa8: options.SupportMsaa8,
            supportMsaa16: options.SupportMsaa16,
            initThreads: options.InitThreads,
            pipelineCache: options.PipelineCache);
    }

    private static Matrix ComputeTileBrushTransform(TileBrushInfo tileInfo, ITileBrush tileBrush, Rect targetBounds)
    {
        var matrix = Matrix.Identity;

        if (tileBrush.TileMode != TileMode.None)
        {
            matrix *= Matrix.CreateTranslation(-tileInfo.DestinationRect.X, -tileInfo.DestinationRect.Y);
        }

        var dpiScale = Matrix.CreateScale(96.0 / s_intermediateDpi.X, 96.0 / s_intermediateDpi.Y);
        matrix *= dpiScale;

        if (tileBrush.Transform is { })
        {
            var origin = tileBrush.TransformOrigin.ToPixels(targetBounds);
            var translateToOrigin = Matrix.CreateTranslation(-origin.X, -origin.Y);
            var translateBack = Matrix.CreateTranslation(origin.X, origin.Y);
            var transformMatrix = translateToOrigin * tileBrush.Transform.Value * translateBack;
            matrix *= transformMatrix;
        }

        if (tileBrush.DestinationRect.Unit == RelativeUnit.Relative)
        {
            matrix *= Matrix.CreateTranslation(targetBounds.X, targetBounds.Y);
        }

        return matrix;
    }

    private static TileBrushInfo CreateTileBrushInfo(ITileBrush tileBrush, Size contentSize, Size targetSize)
    {
        var sourceRect = tileBrush.SourceRect.ToPixels(contentSize);
        var destinationRect = tileBrush.DestinationRect.ToPixels(targetSize);

        var scale = tileBrush.Stretch.CalculateScaling(destinationRect.Size, sourceRect.Size);
        var scaledSourceSize = new Size(sourceRect.Width * scale.X, sourceRect.Height * scale.Y);
        var translate = CalculateTranslate(tileBrush.AlignmentX, tileBrush.AlignmentY, scaledSourceSize, destinationRect.Size);

        var intermediateSize = tileBrush.TileMode == TileMode.None ? targetSize : destinationRect.Size;
        var intermediateTransform = CalculateIntermediateTransform(tileBrush.TileMode, sourceRect, destinationRect, scale, translate, out var drawRect);

        return new TileBrushInfo(sourceRect, destinationRect, intermediateSize, drawRect, intermediateTransform);
    }

    private static global::Avalonia.Vector CalculateTranslate(AlignmentX alignmentX, AlignmentY alignmentY, Size sourceSize, Size destinationSize)
    {
        var x = 0.0;
        var y = 0.0;

        switch (alignmentX)
        {
            case AlignmentX.Center:
                x += (destinationSize.Width - sourceSize.Width) / 2;
                break;
            case AlignmentX.Right:
                x += destinationSize.Width - sourceSize.Width;
                break;
        }

        switch (alignmentY)
        {
            case AlignmentY.Center:
                y += (destinationSize.Height - sourceSize.Height) / 2;
                break;
            case AlignmentY.Bottom:
                y += destinationSize.Height - sourceSize.Height;
                break;
        }

        return new global::Avalonia.Vector(x, y);
    }

    private static Matrix CalculateIntermediateTransform(
        TileMode tileMode,
        Rect sourceRect,
        Rect destinationRect,
        global::Avalonia.Vector scale,
        global::Avalonia.Vector translate,
        out Rect drawRect)
    {
        var transform = Matrix.CreateTranslation(-sourceRect.X, -sourceRect.Y)
                        * Matrix.CreateScale(scale.X, scale.Y)
                        * Matrix.CreateTranslation(translate.X, translate.Y);

        Rect rect;

        if (tileMode == TileMode.None)
        {
            rect = destinationRect;
            transform *= Matrix.CreateTranslation(destinationRect.X, destinationRect.Y);
        }
        else
        {
            rect = new Rect(destinationRect.Size);
        }

        drawRect = rect;
        return transform;
    }

    private readonly record struct TileBrushInfo(
        Rect SourceRect,
        Rect DestinationRect,
        Size IntermediateSize,
        Rect IntermediateClip,
        Matrix IntermediateTransform);

    private static BitmapReference TryGetBitmapReference(IImageBrushSource? source)
    {
        if (source is null || s_imageBrushBitmapProperty is null)
        {
            return BitmapReference.Empty;
        }

        object? referenceObject;

        try
        {
            referenceObject = s_imageBrushBitmapProperty.GetValue(source);
        }
        catch
        {
            return BitmapReference.Empty;
        }

        if (referenceObject is null)
        {
            return BitmapReference.Empty;
        }

        IDisposable? disposable = referenceObject as IDisposable;
        IBitmapImpl? bitmapImpl = null;

        var itemProperty = referenceObject.GetType().GetProperty("Item", BindingFlags.Instance | BindingFlags.Public);
        if (itemProperty is not null)
        {
            try
            {
                bitmapImpl = itemProperty.GetValue(referenceObject) as IBitmapImpl;
            }
            catch
            {
                bitmapImpl = null;
            }
        }

        return new BitmapReference(disposable, bitmapImpl);
    }

    private readonly struct BitmapReference : IDisposable
    {
        private readonly IDisposable? _reference;

        public BitmapReference(IDisposable? reference, IBitmapImpl? bitmapImpl)
        {
            _reference = reference;
            BitmapImpl = bitmapImpl;
        }

        public IBitmapImpl? BitmapImpl { get; }

        public void Dispose()
        {
            _reference?.Dispose();
        }

        public static BitmapReference Empty => new(null, null);
    }

    private Rect ResolveBounds(Rect? bounds)
    {
        if (bounds is { } value && value.Width > 0 && value.Height > 0)
        {
            return value;
        }

        return new Rect(0, 0, Math.Max(1d, _targetSize.Width), Math.Max(1d, _targetSize.Height));
    }

    private static VelloSharp.GradientStop[] CreateGradientStops(IGradientBrush gradient)
    {
        var stops = gradient.GradientStops;
        var result = new VelloSharp.GradientStop[stops.Count];
        var opacity = gradient.Opacity;

        for (var i = 0; i < stops.Count; i++)
        {
            var stop = stops[i];
            var color = ToRgbaColor(stop.Color, opacity);
            result[i] = new VelloSharp.GradientStop((float)Math.Clamp(stop.Offset, 0, 1), color);
        }

        return result;
    }

    private static ExtendMode ToExtendMode(GradientSpreadMethod spreadMethod) => spreadMethod switch
    {
        GradientSpreadMethod.Reflect => ExtendMode.Reflect,
        GradientSpreadMethod.Repeat => ExtendMode.Repeat,
        _ => ExtendMode.Pad,
    };

    private static (ExtendMode X, ExtendMode Y) ToExtendModes(TileMode mode) => mode switch
    {
        TileMode.FlipX => (ExtendMode.Reflect, ExtendMode.Pad),
        TileMode.FlipY => (ExtendMode.Pad, ExtendMode.Reflect),
        TileMode.FlipXY => (ExtendMode.Reflect, ExtendMode.Reflect),
        TileMode.Tile => (ExtendMode.Repeat, ExtendMode.Repeat),
        _ => (ExtendMode.Pad, ExtendMode.Pad),
    };

    private static Vector2 ToVector2(Point point) => new((float)point.X, (float)point.Y);

    private static Matrix? ComposeBrushTransform(Rect bounds, ITransform? transform, RelativePoint origin)
    {
        if (transform is null)
        {
            return null;
        }

        var originPoint = origin.ToPixels(bounds);
        var translateToOrigin = Matrix.CreateTranslation(-originPoint.X, -originPoint.Y);
        var translateBack = Matrix.CreateTranslation(originPoint.X, originPoint.Y);
        var matrix = transform.Value;
        return translateToOrigin * matrix * translateBack;
    }

    private static Matrix3x2? ToMatrix3x2Nullable(Matrix? matrix) => matrix.HasValue ? ToMatrix3x2(matrix.Value) : (Matrix3x2?)null;
}
