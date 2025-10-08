using System;
using System.Buffers;

namespace VelloSharp.Composition.Controls;

public sealed class VisualTreeVirtualizer : IDisposable
{
    private readonly CompositionInterop.CompositionVirtualizer _virtualizer = new();
    private bool _disposed;

    public void UpdateRows(ReadOnlySpan<VirtualRowMetric> rows)
    {
        EnsureNotDisposed();
        _virtualizer.SetRows(rows);
    }

    public void UpdateColumns(ReadOnlySpan<VirtualColumnStrip> strips)
    {
        EnsureNotDisposed();
        _virtualizer.SetColumns(strips);
    }

    public VirtualizationPlan CapturePlan(RowViewportMetrics rowMetrics, ColumnViewportMetrics columnMetrics)
    {
        EnsureNotDisposed();
        _virtualizer.Plan(rowMetrics, columnMetrics);

        int activeCount = _virtualizer.CopyPlan(Span<RowPlanEntry>.Empty);
        int recycleCount = _virtualizer.CopyRecycle(Span<RowPlanEntry>.Empty);

        RowPlanEntry[]? active = activeCount > 0 ? ArrayPool<RowPlanEntry>.Shared.Rent(activeCount) : null;
        RowPlanEntry[]? recycled = recycleCount > 0 ? ArrayPool<RowPlanEntry>.Shared.Rent(recycleCount) : null;

        if (active is not null)
        {
            _virtualizer.CopyPlan(active.AsSpan(0, activeCount));
        }

        if (recycled is not null)
        {
            _virtualizer.CopyRecycle(recycled.AsSpan(0, recycleCount));
        }

        _virtualizer.TryGetWindow(out var window);
        var slice = _virtualizer.GetColumnSlice();
        var telemetry = _virtualizer.GetTelemetry();

        return new VirtualizationPlan(this, active, activeCount, recycled, recycleCount, window, slice, telemetry);
    }

    internal void Release(RowPlanEntry[]? active, RowPlanEntry[]? recycled)
    {
        if (active is not null)
        {
            ArrayPool<RowPlanEntry>.Shared.Return(active);
        }

        if (recycled is not null)
        {
            ArrayPool<RowPlanEntry>.Shared.Return(recycled);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _virtualizer.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VisualTreeVirtualizer));
        }
    }

    public readonly struct VirtualizationPlan : IDisposable
    {
        private readonly VisualTreeVirtualizer _owner;
        private readonly RowPlanEntry[]? _active;
        private readonly int _activeCount;
        private readonly RowPlanEntry[]? _recycled;
        private readonly int _recycledCount;

        internal VirtualizationPlan(
            VisualTreeVirtualizer owner,
            RowPlanEntry[]? active,
            int activeCount,
            RowPlanEntry[]? recycled,
            int recycledCount,
            RowWindow window,
            ColumnSlice columns,
            VirtualizerTelemetry telemetry)
        {
            _owner = owner;
            _active = active;
            _activeCount = activeCount;
            _recycled = recycled;
            _recycledCount = recycledCount;
            Window = window;
            Columns = columns;
            Telemetry = telemetry;
        }

        public ReadOnlySpan<RowPlanEntry> Active =>
            _active is null ? ReadOnlySpan<RowPlanEntry>.Empty : new ReadOnlySpan<RowPlanEntry>(_active, 0, _activeCount);

        public ReadOnlySpan<RowPlanEntry> Recycled =>
            _recycled is null ? ReadOnlySpan<RowPlanEntry>.Empty : new ReadOnlySpan<RowPlanEntry>(_recycled, 0, _recycledCount);

        public RowWindow Window { get; }

        public ColumnSlice Columns { get; }

        public VirtualizerTelemetry Telemetry { get; }

        public void Dispose()
        {
            _owner.Release(_active, _recycled);
        }
    }
}
