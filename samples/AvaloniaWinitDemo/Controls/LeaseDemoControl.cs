using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Threading;
using VelloSharp;
using VelloSharp.Avalonia.Vello.Rendering;

namespace AvaloniaWinitDemo.Controls;

public class LeaseDemoControl : Control
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private DispatcherTimer? _timer;
    private RgbaColor _accentColor = new(0.25f, 0.65f, 1f, 0.85f);
    private bool _leaseAvailable;

    public LeaseDemoControl()
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

        context.FillRectangle(new Avalonia.Media.SolidColorBrush(Color.Parse("#0B1C2D")), bounds);

        context.Custom(new LeaseDrawOperation(bounds, this));

        if (!_leaseAvailable)
        {
            var message = new FormattedText(
                "IVelloApiLeaseFeature unavailable.",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Inter"),
                16,
                Brushes.White);

            message.TextAlignment = TextAlignment.Center;
            var origin = new Point(
                bounds.Width / 2 - message.WidthIncludingTrailingWhitespace / 2,
                bounds.Height / 2 - message.Height / 2);
            context.DrawText(message, origin);
        }
    }

    private void SetLeaseAvailability(bool available)
    {
        _leaseAvailable = available;
    }

    private float GetElapsedTimeSeconds() => (float)_stopwatch.Elapsed.TotalSeconds;

    private void UpdateAccentColor(RgbaColor color) => _accentColor = color;

    private readonly struct LeaseDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly LeaseDemoControl _owner;

        public LeaseDrawOperation(Rect bounds, LeaseDemoControl owner)
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
            other is LeaseDrawOperation operation &&
            operation._owner == _owner &&
            operation._bounds.Equals(_bounds);

        public void Render(ImmediateDrawingContext context)
        {
            if (!context.TryGetFeature<IVelloApiLeaseFeature>(out var feature) || feature is null)
            {
                _owner.SetLeaseAvailability(false);
                return;
            }

            using var lease = feature.Lease();
            using var platformLease = lease.TryLeasePlatformGraphics();

            var globalTransform = ToMatrix3x2(lease.Transform);

            if (platformLease is not null)
            {
                var features = platformLease.Device.GetFeatures();
                _owner.UpdateAccentColor(ComputeAccent(features));
            }

            var scene = lease.Scene;
            var time = _owner.GetElapsedTimeSeconds();

            var width = (float)_bounds.Width;
            var height = (float)_bounds.Height;
            var center = new Vector2(width / 2f, height / 2f);

            var ringRadius = MathF.Min(width, height) * 0.35f;
            var waveAmplitude = ringRadius * 0.08f;
            const int waveFrequency = 5;
            const int segments = 180;

            var swirl = new PathBuilder();
            for (var i = 0; i <= segments; i++)
            {
                var angle = (float)(Math.PI * 2) * (i / (float)segments);
                var wobble = MathF.Sin(angle * waveFrequency + time * 1.35f) * waveAmplitude;
                var radius = ringRadius + wobble;
                var x = center.X + MathF.Cos(angle) * radius;
                var y = center.Y + MathF.Sin(angle) * radius;

                if (i == 0)
                {
                    swirl.MoveTo(x, y);
                }
                else
                {
                    swirl.LineTo(x, y);
                }
            }

            swirl.Close();

            var fillBrush = new VelloSharp.SolidColorBrush(_owner._accentColor);
            scene.FillPath(swirl, VelloSharp.FillRule.NonZero, globalTransform, fillBrush);

            var orbit = new PathBuilder();
            var orbitRect = new Rect(center.X - ringRadius * 0.55f, center.Y - ringRadius * 0.55f, ringRadius * 1.1f, ringRadius * 1.1f);
            AddEllipse(orbit, orbitRect);

            var stroke = new StrokeStyle
            {
                Width = ringRadius * 0.12f,
                LineJoin = LineJoin.Round,
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };

            var orbitBrush = new VelloSharp.SolidColorBrush(new RgbaColor(1f, 1f, 1f, 0.55f));
            var rotation = Matrix3x2.CreateRotation(time * 0.4f, center);
            scene.StrokePath(orbit, stroke, Matrix3x2.Multiply(rotation, globalTransform), orbitBrush);

            var comet = new PathBuilder();
            var cometAngle = time * 1.2f;
            var cometRadius = ringRadius * 0.9f;
            var cometHead = new Vector2(
                center.X + MathF.Cos(cometAngle) * cometRadius,
                center.Y + MathF.Sin(cometAngle) * cometRadius);

            comet.MoveTo(center.X, center.Y);
            comet.LineTo(cometHead.X, cometHead.Y);

            var cometBrush = new VelloSharp.SolidColorBrush(new RgbaColor(1f, 1f, 1f, 0.4f));
            var cometStroke = new StrokeStyle
            {
                Width = ringRadius * 0.045f,
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };

            scene.StrokePath(comet, cometStroke, globalTransform, cometBrush);

            _owner.SetLeaseAvailability(true);
        }
    }

    private static void AddEllipse(PathBuilder builder, Rect rect)
    {
        const double kappa = 0.5522847498307936;
        var rx = rect.Width / 2;
        var ry = rect.Height / 2;
        var cx = rect.X + rx;
        var cy = rect.Y + ry;
        var kappaX = rx * kappa;
        var kappaY = ry * kappa;

        builder.MoveTo(cx, rect.Y);
        builder.CubicTo(cx + kappaX, rect.Y, rect.Right, cy - kappaY, rect.Right, cy);
        builder.CubicTo(rect.Right, cy + kappaY, cx + kappaX, rect.Bottom, cx, rect.Bottom);
        builder.CubicTo(cx - kappaX, rect.Bottom, rect.X, cy + kappaY, rect.X, cy);
        builder.CubicTo(rect.X, cy - kappaY, cx - kappaX, rect.Y, cx, rect.Y);
        builder.Close();
    }

    private static RgbaColor ComputeAccent(WgpuFeature features)
    {
        var combined = (ulong)features;
        var r = ((combined >> 8) & 0xFF) / 255f;
        var g = ((combined >> 16) & 0xFF) / 255f;
        var b = ((combined >> 24) & 0xFF) / 255f;

        r = Math.Clamp(r + 0.25f, 0.25f, 0.9f);
        g = Math.Clamp(g + 0.3f, 0.35f, 0.9f);
        b = Math.Clamp(b + 0.35f, 0.45f, 0.95f);

        return new RgbaColor(r, g, b, 0.85f);
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
}
