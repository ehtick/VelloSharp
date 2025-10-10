using System;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Threading;
using AvaloniaVelloPlayground.Services.Scripting;
using VelloSharp.Avalonia.Vello.Rendering;

namespace AvaloniaVelloPlayground.Controls;

public class ScriptedLeaseView : Control
{
    public static readonly StyledProperty<ScriptExecution?> ExecutionProperty =
        AvaloniaProperty.Register<ScriptedLeaseView, ScriptExecution?>(nameof(Execution));

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Random _random = new();
    private DispatcherTimer? _timer;
    private TimeSpan _lastFrame = TimeSpan.Zero;
    private string? _overlayMessage = "Select or run a script to preview.";
    private bool _hasLease;
    private bool _isPointerOver;

    static ScriptedLeaseView()
    {
        AffectsRender<ScriptedLeaseView>(ExecutionProperty);
    }

    public ScriptedLeaseView()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public ScriptExecution? Execution
    {
        get => GetValue(ExecutionProperty);
        set => SetValue(ExecutionProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        StartTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopTimer();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _isPointerOver = true;
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _isPointerOver = false;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        context.Custom(new ScriptDrawOperation(bounds, this));

        if (!_hasLease)
        {
            DrawOverlay(context, bounds, _overlayMessage ?? "Unable to acquire IVelloApiLeaseFeature.", Brushes.OrangeRed);
        }
        else if (!string.IsNullOrEmpty(_overlayMessage))
        {
            var brush = _isPointerOver ? Brushes.LightGray : Brushes.Silver;
            DrawOverlay(context, bounds, _overlayMessage!, brush);
        }
    }

    internal ScriptRenderContext CreateContext(IVelloApiLease lease, Rect bounds)
    {
        var now = _clock.Elapsed;
        var delta = now - _lastFrame;
        _lastFrame = now;
        return new ScriptRenderContext(lease, bounds, now, delta, _random);
    }

    internal void SetOverlay(string? message, bool leaseAvailable)
    {
        _overlayMessage = message;
        _hasLease = leaseAvailable;
        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);
    }

    private void StartTimer()
    {
        if (_timer is not null)
        {
            if (!_timer.IsEnabled)
            {
                _timer.Start();
            }
            return;
        }

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void StopTimer()
    {
        if (_timer is null)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTick;
        _timer = null;
    }

    private void OnTick(object? sender, EventArgs e) => InvalidateVisual();

    private static void DrawOverlay(DrawingContext context, Rect bounds, string message, IBrush brush)
    {
        const double Padding = 16;

        var overlayRect = bounds.Deflate(Padding);
        var rounded = new RoundedRect(overlayRect, 8);
        var background = new SolidColorBrush(Color.FromArgb(160, 20, 24, 28));

        context.DrawRectangle(background, null, rounded);

        var formatted = new FormattedText(
            message,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter", FontStyle.Normal, FontWeight.Medium),
            16,
            brush)
        {
            TextAlignment = TextAlignment.Center,
            MaxTextWidth = overlayRect.Width,
            MaxTextHeight = overlayRect.Height,
        };

        var center = overlayRect.Center;
        var origin = new Point(
            center.X - formatted.WidthIncludingTrailingWhitespace / 2,
            center.Y - formatted.Height / 2);
        context.DrawText(formatted, origin);
    }

    private readonly struct ScriptDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly ScriptedLeaseView _owner;

        public ScriptDrawOperation(Rect bounds, ScriptedLeaseView owner)
        {
            _bounds = bounds;
            _owner = owner;
        }

        public Rect Bounds => _bounds;

        public bool Equals(ICustomDrawOperation? other)
            => other is ScriptDrawOperation op &&
               op._owner == _owner &&
               op._bounds.Equals(_bounds);

        public bool HitTest(Point point) => _bounds.Contains(point);

        public void Dispose()
        {
        }

        public void Render(ImmediateDrawingContext context)
        {
            var featureObject = context.TryGetFeature(typeof(IVelloApiLeaseFeature));
            if (featureObject is not IVelloApiLeaseFeature feature)
            {
                _owner.SetOverlay("IVelloApiLeaseFeature unavailable.", false);
                return;
            }

            using var lease = feature.Lease();
            var execution = _owner.Execution;
            if (execution is null)
            {
                _owner.SetOverlay("Compile or select an example to render.", true);
                lease.Scene.Reset();
                return;
            }

            var renderContext = _owner.CreateContext(lease, _bounds);

            try
            {
                execution.Render(renderContext);
                _owner.SetOverlay(null, true);
            }
            catch (Exception ex)
            {
                var message = $"{ex.GetType().Name}: {ex.Message}";
                _owner.SetOverlay(message, true);
            }
        }
    }
}
