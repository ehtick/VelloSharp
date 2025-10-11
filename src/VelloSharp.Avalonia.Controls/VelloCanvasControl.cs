using System;
using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using VelloSharp.Avalonia.Vello.Rendering;

namespace VelloSharp.Avalonia.Controls;

/// <summary>
/// Base canvas control that exposes the Vello renderer to Avalonia applications.
/// </summary>
public class VelloCanvasControl : Control
{
    private static readonly MethodInfo? TryGetFeatureMethod =
        typeof(ImmediateDrawingContext).GetMethod(
            "TryGetFeature",
            new[] { typeof(Type), typeof(object).MakeByRefType() });

    /// <summary>
    /// Defines the <see cref="ShowFallbackMessage"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> ShowFallbackMessageProperty =
        AvaloniaProperty.Register<VelloCanvasControl, bool>(nameof(ShowFallbackMessage), true);

    /// <summary>
    /// Defines the <see cref="FallbackMessage"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> FallbackMessageProperty =
        AvaloniaProperty.Register<VelloCanvasControl, string?>(
            nameof(FallbackMessage),
            "Vello rendering is unavailable. Ensure VelloSharp.Avalonia.Vello is registered.");

    /// <summary>
    /// Defines the <see cref="IsVelloAvailable"/> property.
    /// </summary>
    public static readonly DirectProperty<VelloCanvasControl, bool> IsVelloAvailableProperty =
        AvaloniaProperty.RegisterDirect<VelloCanvasControl, bool>(
            nameof(IsVelloAvailable),
            o => o.IsVelloAvailable);

    /// <summary>
    /// Defines the <see cref="UnavailableReason"/> property.
    /// </summary>
    public static readonly DirectProperty<VelloCanvasControl, string?> UnavailableReasonProperty =
        AvaloniaProperty.RegisterDirect<VelloCanvasControl, string?>(
            nameof(UnavailableReason),
            o => o.UnavailableReason);

    private bool _isVelloAvailable;
    private string? _unavailableReason;

    /// <summary>
    /// Occurs when the control requires the caller to draw into the active Vello scene.
    /// </summary>
    public event EventHandler<VelloDrawEventArgs>? Draw;

    public VelloCanvasControl()
    {
        ClipToBounds = true;
    }

    /// <summary>
    /// Gets or sets whether an informative fallback message is rendered when Vello is not available.
    /// </summary>
    public bool ShowFallbackMessage
    {
        get => GetValue(ShowFallbackMessageProperty);
        set => SetValue(ShowFallbackMessageProperty, value);
    }

    /// <summary>
    /// Gets or sets the message shown when Vello cannot be leased from the drawing context.
    /// </summary>
    public string? FallbackMessage
    {
        get => GetValue(FallbackMessageProperty);
        set => SetValue(FallbackMessageProperty, value);
    }

    /// <summary>
    /// Gets a value indicating whether the control successfully accessed the Vello renderer during the last draw.
    /// </summary>
    public bool IsVelloAvailable
    {
        get => _isVelloAvailable;
        private set => SetAndRaise(IsVelloAvailableProperty, ref _isVelloAvailable, value);
    }

    /// <summary>
    /// Gets the last reported reason why Vello could not be used.
    /// </summary>
    public string? UnavailableReason
    {
        get => _unavailableReason;
        private set => SetAndRaise(UnavailableReasonProperty, ref _unavailableReason, value);
    }

    protected virtual bool ShouldRenderVelloScene => true;

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        if (ShouldRenderVelloScene)
        {
            var (total, delta) = AcquireFrameTimes();
            context.Custom(new VelloDrawOperation(bounds, this, total, delta));
        }

        if (!IsVelloAvailable && ShowFallbackMessage)
        {
            var message = UnavailableReason ?? FallbackMessage;
            if (!string.IsNullOrWhiteSpace(message))
            {
                DrawFallbackMessage(context, bounds, message!);
            }
        }
    }

    /// <summary>
    /// Called when the control needs to produce Vello draw commands.
    /// </summary>
    /// <param name="args">The draw arguments.</param>
    protected virtual void OnDraw(VelloDrawEventArgs args)
    {
        Draw?.Invoke(this, args);
    }

    /// <summary>
    /// Allows derived controls to provide timing data that is passed to <see cref="VelloDrawEventArgs"/>.
    /// </summary>
    /// <returns>The total and delta times reported for the next draw.</returns>
    protected virtual (TimeSpan Total, TimeSpan Delta) GetFrameTimes() => (TimeSpan.Zero, TimeSpan.Zero);

    private (TimeSpan Total, TimeSpan Delta) AcquireFrameTimes() => GetFrameTimes();

    protected void DrawFallbackMessage(DrawingContext context, Rect bounds, string message)
    {
        var typeface = new Typeface(FontManager.Current.DefaultFontFamily);
        var formatted = new FormattedText(
            message,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            14,
            Brushes.Gray);

        formatted.TextAlignment = TextAlignment.Center;
        var origin = new Point(
            bounds.X + bounds.Width / 2 - formatted.WidthIncludingTrailingWhitespace / 2,
            bounds.Y + bounds.Height / 2 - formatted.Height / 2);

        context.DrawText(formatted, origin);
    }

    internal void HandleDraw(IVelloApiLease lease, Rect bounds, TimeSpan total, TimeSpan delta)
    {
        var args = new VelloDrawEventArgs(lease, bounds, total, delta);
        OnDraw(args);
    }

    internal void UpdateAvailability(bool available, string? reason)
    {
        if (available)
        {
            IsVelloAvailable = true;
            if (!string.IsNullOrEmpty(UnavailableReason))
            {
                UnavailableReason = null;
            }
        }
        else
        {
            IsVelloAvailable = false;
            if (!string.IsNullOrEmpty(reason))
            {
                UnavailableReason = reason;
            }
            else if (!string.IsNullOrEmpty(FallbackMessage))
            {
                UnavailableReason = FallbackMessage;
            }
        }
    }

    internal static bool TryGetLeaseFeature(ImmediateDrawingContext context, out IVelloApiLeaseFeature? feature)
    {
        if (TryGetFeatureMethod is { } method)
        {
            object?[] parameters = { typeof(IVelloApiLeaseFeature), null };
            if (method.Invoke(context, parameters) is bool success &&
                success &&
                parameters[1] is IVelloApiLeaseFeature leaseFeature)
            {
                feature = leaseFeature;
                return true;
            }
        }

        feature = null;
        return false;
    }

    private readonly struct VelloDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly VelloCanvasControl _owner;
        private readonly TimeSpan _totalTime;
        private readonly TimeSpan _deltaTime;

        public VelloDrawOperation(
            Rect bounds,
            VelloCanvasControl owner,
            TimeSpan totalTime,
            TimeSpan deltaTime)
        {
            _bounds = bounds;
            _owner = owner;
            _totalTime = totalTime;
            _deltaTime = deltaTime;
        }

        public Rect Bounds => _bounds;

        public bool HitTest(Point p) => _bounds.Contains(p);

        public void Render(ImmediateDrawingContext context)
        {
            if (!TryGetLeaseFeature(context, out var feature))
            {
                _owner.UpdateAvailability(false, "IVelloApiLeaseFeature not exposed by the current drawing context.");
                return;
            }

            try
            {
                using var lease = feature!.Lease();
                if (lease is null)
                {
                    _owner.UpdateAvailability(false, "Failed to obtain a Vello API lease.");
                    return;
                }

                _owner.UpdateAvailability(true, null);
                _owner.HandleDraw(lease, _bounds, _totalTime, _deltaTime);
            }
            catch (Exception ex)
            {
                _owner.UpdateAvailability(false, ex.Message);
                throw;
            }
        }

        public bool Equals(ICustomDrawOperation? other) =>
            other is VelloDrawOperation operation &&
            operation._owner == _owner &&
            operation._bounds == _bounds;

        public void Dispose()
        {
        }
    }
}
