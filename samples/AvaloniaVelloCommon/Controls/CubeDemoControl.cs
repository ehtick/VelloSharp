using System;
using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Threading;
using AvaloniaVelloCommon.Rendering;
using Avalonia.Platform;
using VelloSharp.Avalonia.Vello.Rendering;

namespace AvaloniaVelloCommon.Controls;

public class CubeDemoControl : Control
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly SolidColorBrush _backgroundBrush = new(Color.FromRgb(6, 20, 33));
    private readonly CubeRenderer _renderer = new();
    private DispatcherTimer? _timer;
    private bool _wgpuAvailable;

    public CubeDemoControl()
    {
        ClipToBounds = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_timer is null)
        {
            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16),
            };
            _timer.Tick += OnTick;
        }

        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
            _timer = null;
        }

        _renderer.Reset();
        _wgpuAvailable = false;
    }

    private void OnTick(object? sender, EventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        if (!_wgpuAvailable)
        {
            context.FillRectangle(_backgroundBrush, bounds);
        }

        context.Custom(new CubeDrawOperation(bounds, this));

        if (!_wgpuAvailable)
        {
            DrawStatusMessage(context, bounds);
        }
    }

    private static void DrawStatusMessage(DrawingContext context, Rect bounds)
    {
        var message = new FormattedText(
            "Waiting for wgpu surface lease...",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            16,
            Brushes.White)
        {
            TextAlignment = TextAlignment.Center,
        };

        var origin = new Point(
            bounds.X + bounds.Width / 2 - message.WidthIncludingTrailingWhitespace / 2,
            bounds.Y + bounds.Height / 2 - message.Height / 2);

        context.DrawText(message, origin);
    }

    private float GetElapsedTimeSeconds() => (float)_stopwatch.Elapsed.TotalSeconds;

    private void SetAvailability(bool available) => _wgpuAvailable = available;

    private bool RenderCube(WgpuSurfaceRenderContext context, float timeSeconds, Rect viewport) =>
        _renderer.Render(context, timeSeconds, viewport);

    private readonly struct CubeDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly CubeDemoControl _owner;

        public CubeDrawOperation(Rect bounds, CubeDemoControl owner)
        {
            _bounds = bounds;
            _owner = owner;
        }

        public Rect Bounds => _bounds;

        public void Dispose()
        {
        }

        public bool HitTest(Point p) => _bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) =>
            other is CubeDrawOperation op && ReferenceEquals(op._owner, _owner) && op._bounds.Equals(_bounds);

        public void Render(ImmediateDrawingContext context)
        {
            if (!context.TryGetFeature<IVelloApiLeaseFeature>(out var feature) || feature is null)
            {
                _owner.SetAvailability(false);
                return;
            }

            using var lease = feature.Lease();
            var time = _owner.GetElapsedTimeSeconds();
            var owner = _owner;
            var transform = lease.Transform;
            var bounds = _bounds;

            lease.ScheduleWgpuSurfaceRender(renderContext =>
            {
                var surfaceRect = new Rect(0, 0, renderContext.RenderParams.Width, renderContext.RenderParams.Height);
                var deviceRect = bounds.TransformToAABB(transform);
                var viewport = deviceRect.Intersect(surfaceRect);

                var rendered = viewport.Width > 0 && viewport.Height > 0 && owner.RenderCube(renderContext, time, viewport);
                owner.SetAvailability(rendered);
            });
        }
    }
}
