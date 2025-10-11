using System;
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
    /// <summary>
    /// Occurs when the control requires the caller to draw into the active Vello scene.
    /// </summary>
    public event EventHandler<VelloDrawEventArgs>? Draw;

    public VelloCanvasControl()
    {
        ClipToBounds = true;
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

    internal void HandleDraw(IVelloApiLease lease, Rect bounds, TimeSpan total, TimeSpan delta)
    {
        var args = new VelloDrawEventArgs(lease, bounds, total, delta);
        OnDraw(args);
    }

    internal static bool TryGetLeaseFeature(ImmediateDrawingContext context, out IVelloApiLeaseFeature? feature)
    {
        if (context.TryGetFeature(typeof(IVelloApiLeaseFeature)) is IVelloApiLeaseFeature leaseFeature)
        {
            feature = leaseFeature;
            return true;
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
                return;
            }

            try
            {
                using var lease = feature!.Lease();
                if (lease is null)
                {
                    return;
                }

                _owner.HandleDraw(lease, _bounds, _totalTime, _deltaTime);
            }
            catch (Exception ex)
            {
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
