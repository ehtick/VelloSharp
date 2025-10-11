using System;
using System.IO;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Platform;
using Avalonia.Markup.Xaml;
using VelloSharp;

namespace VelloSharp.Avalonia.Controls;

/// <summary>
/// Renders an SVG document using the Vello renderer.
/// </summary>
public class VelloSvgControl : VelloCanvasControl
{
    /// <summary>
    /// Defines the <see cref="Svg"/> property.
    /// </summary>
    public static readonly StyledProperty<VelloSvg?> SvgProperty =
        AvaloniaProperty.Register<VelloSvgControl, VelloSvg?>(nameof(Svg));

    /// <summary>
    /// Defines the <see cref="Source"/> property.
    /// </summary>
    public static readonly StyledProperty<Uri?> SourceProperty =
        AvaloniaProperty.Register<VelloSvgControl, Uri?>(nameof(Source));

    /// <summary>
    /// Defines the <see cref="Stretch"/> property.
    /// </summary>
    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<VelloSvgControl, Stretch>(nameof(Stretch), Stretch.Uniform);

    /// <summary>
    /// Defines the <see cref="StretchDirection"/> property.
    /// </summary>
    public static readonly StyledProperty<StretchDirection> StretchDirectionProperty =
        AvaloniaProperty.Register<VelloSvgControl, StretchDirection>(
            nameof(StretchDirection),
            StretchDirection.Both);

    /// <summary>
    /// Defines the <see cref="LoadError"/> property.
    /// </summary>
    public static readonly DirectProperty<VelloSvgControl, string?> LoadErrorProperty =
        AvaloniaProperty.RegisterDirect<VelloSvgControl, string?>(
            nameof(LoadError),
            o => o.LoadError);

    static VelloSvgControl()
    {
        AffectsRender<VelloSvgControl>(SvgProperty, SourceProperty, StretchProperty, StretchDirectionProperty);
        AffectsMeasure<VelloSvgControl>(SvgProperty, SourceProperty, StretchProperty, StretchDirectionProperty);
    }

    private VelloSvg? _loadedSvg;
    private string? _loadError;
    private VelloSvg? _inlineSvg;
    private Stretch _stretch = Stretch.Uniform;
    private StretchDirection _stretchDirection = StretchDirection.Both;

    /// <summary>
    /// Gets or sets the SVG instance rendered by the control.
    /// </summary>
    [Content]
    public VelloSvg? Svg
    {
        get => GetValue(SvgProperty);
        set => SetValue(SvgProperty, value);
    }

    /// <summary>
    /// Gets or sets the URI of the SVG resource to load.
    /// </summary>
    public Uri? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Gets or sets how the SVG content is stretched to fit the available space.
    /// </summary>
    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>
    /// Gets or sets the allowed stretch directions.
    /// </summary>
    public StretchDirection StretchDirection
    {
        get => GetValue(StretchDirectionProperty);
        set => SetValue(StretchDirectionProperty, value);
    }

    /// <summary>
    /// Gets the last load error encountered when reading the SVG resource.
    /// </summary>
    public string? LoadError
    {
        get => _loadError;
        private set => SetAndRaise(LoadErrorProperty, ref _loadError, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _inlineSvg ??= GetValue(SvgProperty);
        _stretch = GetValue(StretchProperty);
        _stretchDirection = GetValue(StretchDirectionProperty);

        if (_inlineSvg is null && Source is not null && _loadedSvg is null)
        {
            ReloadFromSource();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DisposeLoadedSvg();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var svg = GetActiveSvg();
        if (svg is null)
        {
            return default;
        }

    var sourceSize = ToSize(svg.Size);
    return _stretch.CalculateSize(availableSize, sourceSize, _stretchDirection);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var svg = GetActiveSvg();
        if (svg is null)
        {
            return default;
        }

    var sourceSize = ToSize(svg.Size);
    return _stretch.CalculateSize(finalSize, sourceSize);
    }

    protected override void OnDraw(VelloDrawEventArgs args)
    {
        var svg = GetActiveSvg();
        if (svg is not null)
        {
            RenderSvg(args, svg);
        }

        base.OnDraw(args);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceProperty)
        {
            ReloadFromSource();
        }
        else if (change.Property == SvgProperty)
        {
            _inlineSvg = change.NewValue is VelloSvg svg ? svg : null;
            LoadError = null;
            InvalidateMeasure();
            InvalidateVisual();
        }
        else if (change.Property == StretchProperty)
        {
            _stretch = change.GetNewValue<Stretch>();
            InvalidateMeasure();
            InvalidateVisual();
        }
        else if (change.Property == StretchDirectionProperty)
        {
            _stretchDirection = change.GetNewValue<StretchDirection>();
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private void ReloadFromSource()
    {
        DisposeLoadedSvg();

        var source = Source;
        if (source is null)
        {
            LoadError = null;
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }

        try
        {
            var baseUri = (this as IUriContext)?.BaseUri;
            if (!source.IsAbsoluteUri && baseUri is null)
            {
                LoadError = "Relative SVG sources require a BaseUri context.";
                InvalidateMeasure();
                InvalidateVisual();
                return;
            }

            using var stream = OpenSourceStream(source, baseUri);
            if (stream is null)
            {
                LoadError = $"Unable to locate SVG resource '{source}'.";
            }
            else
            {
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                _loadedSvg = VelloSvg.LoadFromUtf8(memory.ToArray());
                LoadError = null;
            }
        }
        catch (Exception ex)
        {
            LoadError = ex.Message;
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    private static Size ToSize(Vector2 vector)
    {
        return new Size(Math.Max(0, vector.X), Math.Max(0, vector.Y));
    }

    private void RenderSvg(VelloDrawEventArgs args, VelloSvg svg)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var originalSize = ToSize(svg.Size);
        if (originalSize.Width <= 0 || originalSize.Height <= 0)
        {
            return;
        }

    var scale = _stretch.CalculateScaling(bounds.Size, originalSize, _stretchDirection);
        if (!double.IsFinite(scale.X) || !double.IsFinite(scale.Y) || scale.X <= 0 || scale.Y <= 0)
        {
            scale = new global::Avalonia.Vector(1, 1);
        }
        var scaledSize = originalSize * scale;
        var destRect = bounds.CenterRect(new Rect(scaledSize)).Intersect(bounds);
        if (destRect.Width <= 0 || destRect.Height <= 0)
        {
            return;
        }

        var sourceRect = new Rect(originalSize).CenterRect(new Rect(destRect.Size / scale));
        var scaleMatrix = Matrix.CreateScale(
            destRect.Width / sourceRect.Width,
            destRect.Height / sourceRect.Height);
        var translateMatrix = Matrix.CreateTranslation(
            -sourceRect.X + destRect.X,
            -sourceRect.Y + destRect.Y);

        var localTransform = (scaleMatrix * translateMatrix).ToMatrix3x2();
        var finalTransform = Matrix3x2.Multiply(localTransform, args.GlobalTransform);
        svg.Render(args.Scene, finalTransform);
    }

    private VelloSvg? GetActiveSvg() => _inlineSvg ?? _loadedSvg;

    private void DisposeLoadedSvg()
    {
        _loadedSvg?.Dispose();
        _loadedSvg = null;
    }

    private Stream? OpenSourceStream(Uri source, Uri? baseUri)
    {
        if (source.IsAbsoluteUri)
        {
            if (source.Scheme == Uri.UriSchemeFile)
            {
                var path = source.LocalPath;
                return File.Exists(path) ? File.OpenRead(path) : null;
            }

            // Allow resource URIs such as avares:// or embedded pack URIs.
            return AssetLoader.Open(source);
        }

        return AssetLoader.Open(source, baseUri);
    }
}
