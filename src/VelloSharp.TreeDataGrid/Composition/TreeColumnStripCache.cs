using System;
using VelloSharp.TreeDataGrid;

namespace VelloSharp.TreeDataGrid.Composition;

public readonly record struct TreeColumnPaneDiff(bool LeadingChanged, bool PrimaryChanged, bool TrailingChanged)
{
    public bool Any => LeadingChanged || PrimaryChanged || TrailingChanged;

    public TreeColumnPaneDiff Union(TreeColumnPaneDiff other)
        => new(
            LeadingChanged || other.LeadingChanged,
            PrimaryChanged || other.PrimaryChanged,
            TrailingChanged || other.TrailingChanged);
}

public readonly record struct TreeColumnPaneSnapshot(
    ReadOnlyMemory<TreeColumnSpan> Spans,
    ReadOnlyMemory<TreeColumnMetric> Metrics)
{
    public static TreeColumnPaneSnapshot Empty { get; } = new(
        ReadOnlyMemory<TreeColumnSpan>.Empty,
        ReadOnlyMemory<TreeColumnMetric>.Empty);

    public int Count => Spans.Length;
}

public readonly struct TreeColumnStripSnapshot
{
    public TreeColumnStripSnapshot(
        ReadOnlyMemory<TreeColumnSpan> spans,
        ReadOnlyMemory<TreeColumnMetric> metrics,
        uint frozenLeading,
        uint frozenTrailing,
        TreeColumnPaneDiff paneDiff)
    {
        Spans = spans;
        Metrics = metrics;
        FrozenLeading = frozenLeading;
        FrozenTrailing = frozenTrailing;
        PaneDiff = paneDiff;
    }

    public ReadOnlyMemory<TreeColumnSpan> Spans { get; }
    public ReadOnlyMemory<TreeColumnMetric> Metrics { get; }
    public uint FrozenLeading { get; }
    public uint FrozenTrailing { get; }
    public TreeColumnPaneDiff PaneDiff { get; }

    public TreeColumnPaneSnapshot LeadingPane => SlicePane(0, LeadingCount);

    public TreeColumnPaneSnapshot PrimaryPane => SlicePane(
        LeadingCount,
        Math.Max(Spans.Length - LeadingCount - TrailingCount, 0));

    public TreeColumnPaneSnapshot TrailingPane => SlicePane(
        Math.Max(Spans.Length - TrailingCount, 0),
        TrailingCount);

    private int LeadingCount => Math.Min((int)FrozenLeading, Spans.Length);

    private int TrailingCount => Math.Min((int)FrozenTrailing, Math.Max(Spans.Length - LeadingCount, 0));

    private TreeColumnPaneSnapshot SlicePane(int start, int count)
    {
        if (count <= 0 || start >= Spans.Length)
        {
            return TreeColumnPaneSnapshot.Empty;
        }

        var spanSlice = Spans.Slice(start, Math.Min(count, Spans.Length - start));

        ReadOnlyMemory<TreeColumnMetric> metricSlice;
        if (start >= Metrics.Length)
        {
            metricSlice = ReadOnlyMemory<TreeColumnMetric>.Empty;
        }
        else
        {
            metricSlice = Metrics.Slice(start, Math.Min(count, Metrics.Length - start));
        }

        return new TreeColumnPaneSnapshot(spanSlice, metricSlice);
    }
}

/// <summary>
/// Maintains a snapshot of column strips grouped by freeze bands (leading, primary, trailing)
/// and provides change tracking across updates.
/// </summary>
public sealed class TreeColumnStripCache
{
    private TreeColumnSpan[] _spans = Array.Empty<TreeColumnSpan>();
    private TreeColumnMetric[] _metrics = Array.Empty<TreeColumnMetric>();
    private ZoneSnapshot _leadingSnapshot;
    private ZoneSnapshot _primarySnapshot;
    private ZoneSnapshot _trailingSnapshot;
    private bool _initialized;
    private int _length;

    public TreeColumnStripSnapshot Update(
        ReadOnlySpan<TreeColumnDefinition> definitions,
        ReadOnlySpan<TreeColumnSlot> slots)
    {
        if (definitions.Length != slots.Length)
        {
            throw new ArgumentException("Column definition count does not match slot count.", nameof(slots));
        }

        EnsureCapacity(definitions.Length);
        _length = definitions.Length;

        HashAccumulator leadingHash = default;
        HashAccumulator primaryHash = default;
        HashAccumulator trailingHash = default;

        uint frozenLeading = 0;
        uint frozenTrailing = 0;
        int primaryCount = 0;

        for (var index = 0; index < definitions.Length; index++)
        {
            ref readonly var definition = ref definitions[index];
            ref readonly var slot = ref slots[index];
            var frozen = definition.Frozen;

            var key = definition.Key;
            _spans[index] = new TreeColumnSpan(slot.Offset, slot.Width, frozen, key);
            _metrics[index] = new TreeColumnMetric(slot.Offset, slot.Width, frozen, key);

            switch (frozen)
            {
                case TreeFrozenKind.Leading:
                    frozenLeading++;
                    leadingHash.Add(index);
                    leadingHash.Add(slot.Offset);
                    leadingHash.Add(slot.Width);
                    leadingHash.Add(definition.Key);
                    break;
                case TreeFrozenKind.Trailing:
                    frozenTrailing++;
                    trailingHash.Add(index);
                    trailingHash.Add(slot.Offset);
                    trailingHash.Add(slot.Width);
                    trailingHash.Add(definition.Key);
                    break;
                default:
                    primaryCount++;
                    primaryHash.Add(index);
                    primaryHash.Add(slot.Offset);
                    primaryHash.Add(slot.Width);
                    primaryHash.Add(definition.Key);
                    break;
            }
        }

        var leadingChanged = !_initialized ||
            _leadingSnapshot.Count != (int)frozenLeading ||
            _leadingSnapshot.Hash != leadingHash.Value;
        var primaryChanged = !_initialized ||
            _primarySnapshot.Count != primaryCount ||
            _primarySnapshot.Hash != primaryHash.Value;
        var trailingChanged = !_initialized ||
            _trailingSnapshot.Count != (int)frozenTrailing ||
            _trailingSnapshot.Hash != trailingHash.Value;

        _leadingSnapshot = new ZoneSnapshot((int)frozenLeading, leadingHash.Value);
        _primarySnapshot = new ZoneSnapshot(primaryCount, primaryHash.Value);
        _trailingSnapshot = new ZoneSnapshot((int)frozenTrailing, trailingHash.Value);
        _initialized = true;

        return new TreeColumnStripSnapshot(
            _spans.AsMemory(0, _length),
            _metrics.AsMemory(0, _length),
            frozenLeading,
            frozenTrailing,
            new TreeColumnPaneDiff(leadingChanged, primaryChanged, trailingChanged));
    }

    private void EnsureCapacity(int required)
    {
        if (_spans.Length < required)
        {
            Array.Resize(ref _spans, Math.Max(required, _spans.Length == 0 ? 4 : _spans.Length * 2));
        }

        if (_metrics.Length < required)
        {
            Array.Resize(ref _metrics, Math.Max(required, _metrics.Length == 0 ? 4 : _metrics.Length * 2));
        }
    }

    private readonly struct ZoneSnapshot
    {
        public ZoneSnapshot(int count, ulong hash)
        {
            Count = count;
            Hash = hash;
        }

        public int Count { get; }
        public ulong Hash { get; }
    }

    private struct HashAccumulator
    {
        private ulong _value;

        public ulong Value => _value;

        public void Add(int value) => Mix(unchecked((ulong)(uint)value));
        public void Add(uint value) => Mix(value);
        public void Add(double value) => Mix(BitConverter.DoubleToUInt64Bits(value));

        private void Mix(ulong contribution)
        {
            _value ^= contribution + 0x9E3779B97F4A7C15UL + (_value << 6) + (_value >> 2);
        }
    }
}
