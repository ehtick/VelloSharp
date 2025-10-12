using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using AvaloniaVelloHarfBuzzSample.Diagnostics;
using AvaloniaVelloHarfBuzzSample.Services;
using VelloSharp;
using VelloSharp.Avalonia.Vello.Rendering;

namespace AvaloniaVelloHarfBuzzSample.Rendering;

public class HarfBuzzLeaseSurface : Control
{
    public static readonly StyledProperty<IHarfBuzzLeaseRenderer?> RendererProperty =
        AvaloniaProperty.Register<HarfBuzzLeaseSurface, IHarfBuzzLeaseRenderer?>(nameof(Renderer));

    public static readonly StyledProperty<HarfBuzzSampleServices?> ServicesProperty =
        AvaloniaProperty.Register<HarfBuzzLeaseSurface, HarfBuzzSampleServices?>(nameof(Services));

    public static readonly StyledProperty<bool> ShowAxesProperty =
        AvaloniaProperty.Register<HarfBuzzLeaseSurface, bool>(nameof(ShowAxes), true);

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly FontAssetService _fallbackFontAssets;
    private readonly HarfBuzzShapeService _fallbackShapeService;
    private readonly ShapeCaptureRecorder _fallbackCaptureRecorder;
    private readonly HarfBuzzSampleServices _fallbackServices;

    private ulong _frameCounter;
    private bool _leaseAvailable;
    private string? _overlayMessage;

    static HarfBuzzLeaseSurface()
    {
        AffectsRender<HarfBuzzLeaseSurface>(RendererProperty, ServicesProperty, ShowAxesProperty);
    }

    public HarfBuzzLeaseSurface()
    {
        ClipToBounds = true;
        _fallbackFontAssets = new FontAssetService();
        _fallbackShapeService = new HarfBuzzShapeService(_fallbackFontAssets);
        _fallbackCaptureRecorder = new ShapeCaptureRecorder();
        _fallbackServices = new HarfBuzzSampleServices(_fallbackFontAssets, _fallbackShapeService, _fallbackCaptureRecorder);
    }

    public IHarfBuzzLeaseRenderer? Renderer
    {
        get => GetValue(RendererProperty);
        set => SetValue(RendererProperty, value);
    }

    public HarfBuzzSampleServices? Services
    {
        get => GetValue(ServicesProperty);
        set => SetValue(ServicesProperty, value);
    }

    public bool ShowAxes
    {
        get => GetValue(ShowAxesProperty);
        set => SetValue(ShowAxesProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        context.Custom(new LeaseDrawOperation(this, Bounds));

        if (!_leaseAvailable && !string.IsNullOrWhiteSpace(_overlayMessage))
        {
            DrawOverlay(context, Bounds, _overlayMessage);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _leaseAvailable = false;
    }

    private HarfBuzzSampleServices ResolveServices()
        => Services ?? _fallbackServices;

    private void UpdateStatus(bool available, string? message = null)
    {
        _leaseAvailable = available;
        _overlayMessage = available ? null : message;
    }

    private static Matrix3x2 ToMatrix3x2(Matrix matrix)
        => new((float)matrix.M11, (float)matrix.M12, (float)matrix.M21, (float)matrix.M22, (float)matrix.M31, (float)matrix.M32);

    private void DrawAxes(Scene scene, Matrix3x2 globalTransform)
    {
        if (!ShowAxes)
        {
            return;
        }

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var center = bounds.Center;
        var horizontal = new PathBuilder();
        horizontal.MoveTo((float)bounds.X, (float)center.Y);
        horizontal.LineTo((float)bounds.Right, (float)center.Y);

        var vertical = new PathBuilder();
        vertical.MoveTo((float)center.X, (float)bounds.Y);
        vertical.LineTo((float)center.X, (float)bounds.Bottom);

        var stroke = new StrokeStyle
        {
            Width = 1.0f,
            LineJoin = LineJoin.Miter,
            StartCap = LineCap.Square,
            EndCap = LineCap.Square,
        };

        var axisColor = RgbaColor.FromBytes(64, 162, 255, 96);
        var transform = globalTransform;
        scene.StrokePath(horizontal, stroke, transform, new VelloSharp.SolidColorBrush(axisColor));
        scene.StrokePath(vertical, stroke, transform, new VelloSharp.SolidColorBrush(axisColor));
    }

    private static void DrawOverlay(DrawingContext context, Rect bounds, string message)
    {
        var padding = new Thickness(16, 12);
        var background = new Avalonia.Media.SolidColorBrush(Color.FromArgb(180, 18, 26, 38));
        var border = new Avalonia.Media.SolidColorBrush(Color.FromArgb(220, 35, 48, 68));

        var text = new FormattedText(
            message,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            14,
            Brushes.White)
        {
            TextAlignment = TextAlignment.Center,
        };

        var width = Math.Min(bounds.Width - 32, Math.Max(220, text.WidthIncludingTrailingWhitespace + padding.Left + padding.Right));
        var height = text.Height + padding.Top + padding.Bottom;
        var rect = new Rect(
            bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2,
            width,
            height);

        context.FillRectangle(background, rect);
        context.DrawRectangle(new Pen(border, 1), rect);

        var textOrigin = new Point(rect.X + (rect.Width - text.WidthIncludingTrailingWhitespace) / 2, rect.Y + (rect.Height - text.Height) / 2);
        context.DrawText(text, textOrigin);
    }

    private readonly struct LeaseDrawOperation : ICustomDrawOperation
    {
        private readonly HarfBuzzLeaseSurface _owner;
        private readonly Rect _bounds;

        public LeaseDrawOperation(HarfBuzzLeaseSurface owner, Rect bounds)
        {
            _owner = owner;
            _bounds = bounds;
        }

        public Rect Bounds => _bounds;

        public void Dispose()
        {
        }

        public bool HitTest(Point p) => _bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) =>
            other is LeaseDrawOperation operation &&
            ReferenceEquals(_owner, operation._owner) &&
            _bounds.Equals(operation._bounds);

        public void Render(ImmediateDrawingContext context)
        {
            var renderer = _owner.Renderer;
            if (renderer is null)
            {
                _owner.UpdateStatus(false, "Renderer not assigned.");
                return;
            }

            if (context.TryGetFeature(typeof(IVelloApiLeaseFeature)) is not IVelloApiLeaseFeature feature)
            {
                _owner.UpdateStatus(false, "IVelloApiLeaseFeature unavailable.");
                return;
            }

            using var lease = feature.Lease();
            if (lease is null)
            {
                _owner.UpdateStatus(false, "Failed to acquire Vello lease.");
                return;
            }

            var services = _owner.ResolveServices();
            var scaling = _owner.VisualRoot?.RenderScaling ?? 1.0;
            var platformLease = lease.TryLeasePlatformGraphics();
            using var renderContext = new HarfBuzzLeaseRenderContext(
                lease.Scene,
                _bounds,
                ToMatrix3x2(lease.Transform),
                scaling,
                _owner._stopwatch.Elapsed,
                _owner._frameCounter++,
                services,
                platformLease);

            lease.Scene.Reset();
            renderer.Render(renderContext);

            _owner.DrawAxes(lease.Scene, renderContext.GlobalTransform);
            _owner.UpdateStatus(true);
        }
    }
}
