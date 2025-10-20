using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Rendering.SceneGraph;
using VelloSharp.Avalonia.Vello.Rendering;

namespace VelloSharp.Avalonia.Svg;

/// <summary>
/// Vello-backed SVG control that keeps the classic Avalonia SVG surface mostly intact.
/// </summary>
public class Svg : Control
{
    private readonly Uri? _baseUri;
    private SvgSource? _svg;
    private bool _enableCache;
    private bool _wireframe;
    private bool _disableFilters;
    private double _zoom = 1.0;
    private double _panX;
    private double _panY;
    private Dictionary<string, SvgSource>? _cache;

    public static readonly StyledProperty<string?> PathProperty =
        AvaloniaProperty.Register<Svg, string?>(nameof(Path));

    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<Svg, string?>(nameof(Source));

    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<Svg, Stretch>(nameof(Stretch), Stretch.Uniform);

    public static readonly StyledProperty<StretchDirection> StretchDirectionProperty =
        AvaloniaProperty.Register<Svg, StretchDirection>(nameof(StretchDirection), StretchDirection.Both);

    public static readonly DirectProperty<Svg, bool> EnableCacheProperty =
        AvaloniaProperty.RegisterDirect<Svg, bool>(nameof(EnableCache), o => o.EnableCache, (o, v) => o.EnableCache = v);

    public static readonly DirectProperty<Svg, bool> WireframeProperty =
        AvaloniaProperty.RegisterDirect<Svg, bool>(nameof(Wireframe), o => o.Wireframe, (o, v) => o.Wireframe = v);

    public static readonly DirectProperty<Svg, bool> DisableFiltersProperty =
        AvaloniaProperty.RegisterDirect<Svg, bool>(nameof(DisableFilters), o => o.DisableFilters, (o, v) => o.DisableFilters = v);

    public static readonly DirectProperty<Svg, double> ZoomProperty =
        AvaloniaProperty.RegisterDirect<Svg, double>(nameof(Zoom), o => o.Zoom, (o, v) => o.Zoom = v);

    public static readonly DirectProperty<Svg, double> PanXProperty =
        AvaloniaProperty.RegisterDirect<Svg, double>(nameof(PanX), o => o.PanX, (o, v) => o.PanX = v);

    public static readonly DirectProperty<Svg, double> PanYProperty =
        AvaloniaProperty.RegisterDirect<Svg, double>(nameof(PanY), o => o.PanY, (o, v) => o.PanY = v);

    public static readonly AttachedProperty<string?> CssProperty =
        AvaloniaProperty.RegisterAttached<Svg, AvaloniaObject, string?>("Css", inherits: true);

    public static readonly AttachedProperty<string?> CurrentCssProperty =
        AvaloniaProperty.RegisterAttached<Svg, AvaloniaObject, string?>("CurrentCss", inherits: true);

    static Svg()
    {
        AffectsRender<Svg>(PathProperty, SourceProperty, StretchProperty, StretchDirectionProperty);
        AffectsMeasure<Svg>(PathProperty, SourceProperty, StretchProperty, StretchDirectionProperty);

        CssProperty.Changed.AddClassHandler<Control>(OnCssChanged);
        CurrentCssProperty.Changed.AddClassHandler<Control>(OnCssChanged);
    }

    public Svg()
    {
    }

    public Svg(Uri baseUri)
    {
        _baseUri = baseUri;
    }

    public Svg(IServiceProvider serviceProvider)
    {
        _baseUri = serviceProvider.GetContextBaseUri();
    }

    [Content]
    public string? Path
    {
        get => GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public StretchDirection StretchDirection
    {
        get => GetValue(StretchDirectionProperty);
        set => SetValue(StretchDirectionProperty, value);
    }

    public bool EnableCache
    {
        get => _enableCache;
        set => SetAndRaise(EnableCacheProperty, ref _enableCache, value);
    }

    public bool Wireframe
    {
        get => _wireframe;
        set => SetAndRaise(WireframeProperty, ref _wireframe, value);
    }

    public bool DisableFilters
    {
        get => _disableFilters;
        set => SetAndRaise(DisableFiltersProperty, ref _disableFilters, value);
    }

    public double Zoom
    {
        get => _zoom;
        set => SetAndRaise(ZoomProperty, ref _zoom, value);
    }

    public double PanX
    {
        get => _panX;
        set => SetAndRaise(PanXProperty, ref _panX, value);
    }

    public double PanY
    {
        get => _panY;
        set => SetAndRaise(PanYProperty, ref _panY, value);
    }

    public VelloSharp.VelloSvg? VelloSvg => _svg?.Vello;

    public void ZoomToPoint(double newZoom, Point point)
    {
        var oldZoom = _zoom;

        if (newZoom < 0.1)
        {
            newZoom = 0.1;
        }
        else if (newZoom > 10)
        {
            newZoom = 10;
        }

        var zoomFactor = newZoom / oldZoom;

        _panX = point.X - (point.X - _panX) * zoomFactor;
        _panY = point.Y - (point.Y - _panY) * zoomFactor;

        SetAndRaise(PanXProperty, ref _panX, _panX);
        SetAndRaise(PanYProperty, ref _panY, _panY);
        SetAndRaise(ZoomProperty, ref _zoom, newZoom);
    }


    public static string? GetCss(AvaloniaObject element) => element.GetValue(CssProperty);

    public static void SetCss(AvaloniaObject element, string? value) => element.SetValue(CssProperty, value);

    public static string? GetCurrentCss(AvaloniaObject element) => element.GetValue(CurrentCssProperty);

    public static void SetCurrentCss(AvaloniaObject element, string? value) => element.SetValue(CurrentCssProperty, value);

    protected override Size MeasureOverride(Size availableSize)
    {
        var source = _svg;
        var size = source?.Size ?? default;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return default;
        }

        return Stretch.CalculateSize(availableSize, size, StretchDirection);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var source = _svg;
        var size = source?.Size ?? default;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return default;
        }

        return Stretch.CalculateSize(finalSize, size);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var source = _svg;
        if (source is null)
        {
            return;
        }

        var sourceSize = source.Size;
        if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
        {
            return;
        }

        var viewPort = new Rect(Bounds.Size);
        var scale = Stretch.CalculateScaling(Bounds.Size, sourceSize, StretchDirection);
        var scaledSize = sourceSize * scale;
        var destRect = viewPort.CenterRect(new Rect(scaledSize)).Intersect(viewPort);
        var svgSourceRect = new Rect(sourceSize).CenterRect(new Rect(destRect.Size / scale));
        var scaleMatrix = Matrix.CreateScale(
            destRect.Width / svgSourceRect.Width,
            destRect.Height / svgSourceRect.Height);
        var translateMatrix = Matrix.CreateTranslation(
            -svgSourceRect.X + destRect.X,
            -svgSourceRect.Y + destRect.Y);
        var userMatrix = Matrix.CreateScale(Zoom, Zoom) * Matrix.CreateTranslation(PanX, PanY);
        var localTransform = (scaleMatrix * translateMatrix).ToMatrix3x2();
        var userTransform = userMatrix.ToMatrix3x2();

        using var clip = context.PushClip(destRect);
        context.Custom(new SvgCustomDrawOperation(destRect, source, localTransform, userTransform));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PathProperty)
        {
            var css = CombineCss(GetCss(this), GetCurrentCss(this));
            LoadFromPath(change.GetNewValue<string?>(), css);
        }
        else if (change.Property == SourceProperty)
        {
            var css = CombineCss(GetCss(this), GetCurrentCss(this));
            LoadFromSource(change.GetNewValue<string?>(), css);
        }
        else if (change.Property == StretchProperty || change.Property == StretchDirectionProperty)
        {
            InvalidateMeasure();
            InvalidateVisual();
        }
        else if (change.Property == EnableCacheProperty)
        {
            if (!change.GetNewValue<bool>())
            {
                DisposeCache();
                _cache = null;
            }
            else
            {
                _cache = new Dictionary<string, SvgSource>();
            }
        }
        else if (change.Property == WireframeProperty || change.Property == DisableFiltersProperty)
        {
            // Wireframe and filter toggles are not currently implemented for Vello rendering.
            InvalidateVisual();
        }
        else if (change.Property == ZoomProperty ||
                 change.Property == PanXProperty ||
                 change.Property == PanYProperty)
        {
            InvalidateVisual();
        }
    }

    private void LoadFromPath(string? path, string? css)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            _svg?.Dispose();
            _svg = null;
            DisposeCache();
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }

        if (_enableCache && _cache is { } cache && cache.TryGetValue(path, out var cached))
        {
            _svg = cached;
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }

        if (!_enableCache)
        {
            _svg?.Dispose();
            _svg = null;
        }

        try
        {
            SvgParameters? parameters = string.IsNullOrWhiteSpace(css) ? null : new SvgParameters(null, css);
            var baseUri = _baseUri ?? (this as IUriContext)?.BaseUri;
            _svg = SvgSource.Load(path, baseUri, parameters);

            if (_enableCache && _cache is { } dictionary && _svg is { })
            {
                dictionary[path] = _svg;
            }
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "Failed to load SVG: {0}", ex);
            _svg = null;
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    private void LoadFromSource(string? source, string? css)
    {
        if (string.IsNullOrEmpty(source))
        {
            _svg?.Dispose();
            _svg = null;
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }

        try
        {
            SvgParameters? parameters = string.IsNullOrWhiteSpace(css) ? null : new SvgParameters(null, css);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(source));
            _svg = SvgSource.LoadFromStream(stream, parameters);
        }
        catch (Exception ex)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this, "Failed to load inline SVG: {0}", ex);
            _svg = null;
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    private static void OnCssChanged(AvaloniaObject obj, AvaloniaPropertyChangedEventArgs args)
    {
        if (obj is Svg svg)
        {
            var css = CombineCss(GetCss(svg), GetCurrentCss(svg));
            var path = svg.Path;
            var source = svg.Source;

            if (path is { })
            {
                svg.LoadFromPath(path, css);
            }
            else if (source is { })
            {
                svg.LoadFromSource(source, css);
            }
        }
    }

    private static string? CombineCss(string? css, string? currentCss)
    {
        if (string.IsNullOrWhiteSpace(css))
        {
            return string.IsNullOrWhiteSpace(currentCss) ? null : currentCss;
        }

        if (string.IsNullOrWhiteSpace(currentCss))
        {
            return css;
        }

        return string.Concat(css, ' ', currentCss);
    }

    private void DisposeCache()
    {
        if (_cache is null)
        {
            return;
        }

        foreach (var kvp in _cache)
        {
            if (!ReferenceEquals(kvp.Value, _svg))
            {
                kvp.Value.Dispose();
            }
        }
    }

    private sealed class SvgCustomDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly SvgSource _source;
        private readonly Matrix3x2 _localTransform;
        private readonly Matrix3x2 _userTransform;

        public SvgCustomDrawOperation(Rect bounds, SvgSource source, Matrix3x2 localTransform, Matrix3x2 userTransform)
        {
            _bounds = bounds;
            _source = source;
            _localTransform = localTransform;
            _userTransform = userTransform;
        }

        public Rect Bounds => _bounds;

        public bool HitTest(Point p) => _bounds.Contains(p);

        public void Render(ImmediateDrawingContext context)
        {
            if (_source.Vello is not { } vello)
            {
                return;
            }

            if (context.TryGetFeature(typeof(IVelloApiLeaseFeature)) is not IVelloApiLeaseFeature velloFeature)
            {
                return;
            }

            using var lease = velloFeature.Lease();
            if (lease is null)
            {
                return;
            }

            var global = lease.Transform.ToMatrix3x2();
            var transform = Matrix3x2.Multiply(_localTransform, _userTransform);
            transform = Matrix3x2.Multiply(transform, global);
            vello.Render(lease.Scene, transform);
        }

        public bool Equals(ICustomDrawOperation? other)
        {
            return other is SvgCustomDrawOperation operation &&
                   ReferenceEquals(operation._source, _source) &&
                   operation._bounds == _bounds;
        }

        public void Dispose()
        {
        }
    }
}
