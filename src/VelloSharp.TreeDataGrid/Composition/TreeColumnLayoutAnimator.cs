using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using VelloSharp.Composition;

namespace VelloSharp.TreeDataGrid.Composition;

public sealed class TreeColumnLayoutAnimator : IDisposable
{
    private readonly TreeNodeLayoutEngine _engine = new();
    private readonly TimelineSystem _timeline = new();
    private readonly uint _timelineGroup;
    private readonly Dictionary<uint, ColumnState> _columns = new();
    private readonly Dictionary<uint, TrackBinding> _trackBindings = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private TreeColumnSlot[] _slotBuffer = Array.Empty<TreeColumnSlot>();
    private uint[] _columnOrder = Array.Empty<uint>();
    private int _slotCount;
    private TimeSpan _lastTimestamp;

    private readonly float _springStiffness;
    private readonly float _springDamping;
    private readonly float _springMass;
    private readonly float _restVelocity;
    private readonly float _restOffset;

    public TreeColumnLayoutAnimator(
        double stiffness = 110.0,
        double damping = 18.0,
        double mass = 1.0,
        double restVelocity = 0.0005,
        double restOffset = 0.0005)
    {
        _springStiffness = (float)Math.Clamp(stiffness, 10.0, 500.0);
        _springDamping = (float)Math.Clamp(damping, 1.0, 80.0);
        _springMass = (float)Math.Clamp(mass, 0.1, 10.0);
        _restVelocity = (float)Math.Clamp(restVelocity, 0.0001, 0.01);
        _restOffset = (float)Math.Clamp(restOffset, 0.0001, 0.01);

        _timelineGroup = _timeline.CreateGroup(new TimelineGroupConfig());
        _timeline.PlayGroup(_timelineGroup);
    }

    public ReadOnlySpan<TreeColumnSlot> Animate(
        ReadOnlySpan<TreeColumnDefinition> columns,
        double availableWidth,
        double spacing)
    {
        var targets = _engine.ArrangeColumns(columns, availableWidth, spacing);
        if (targets.Count == 0)
        {
            Reset();
            return ReadOnlySpan<TreeColumnSlot>.Empty;
        }

        EnsureSlotCapacity(targets.Count);
        EnsureOrderCapacity(targets.Count);
        _slotCount = targets.Count;

        var seen = HashSetPool<uint>.Rent();
        try
        {
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                var key = ResolveColumnKey(columns, i);
                seen.Add(key);
                _columnOrder[i] = key;

                var state = GetOrCreateState(key, target);
                UpdateSpringTrack(key, ColumnProperty.Offset, target.Offset);
                UpdateSpringTrack(key, ColumnProperty.Width, target.Width);
            }

            RemoveStaleColumns(seen);
            TickTimeline();
            CopyStatesToBuffer();
        }
        finally
        {
            HashSetPool<uint>.Return(seen);
        }

        return _slotBuffer.AsSpan(0, _slotCount);
    }

    public bool TryAdvance(out ReadOnlySpan<TreeColumnSlot> slots)
    {
        if (_slotCount == 0 || _trackBindings.Count == 0)
        {
            slots = ReadOnlySpan<TreeColumnSlot>.Empty;
            return false;
        }

        if (!TickTimeline())
        {
            slots = ReadOnlySpan<TreeColumnSlot>.Empty;
            return false;
        }

        CopyStatesToBuffer();
        slots = _slotBuffer.AsSpan(0, _slotCount);
        return true;
    }

    public void Reset()
    {
        foreach (var state in _columns.Values)
        {
            if (state.OffsetTrackId != 0)
            {
                _timeline.RemoveTrack(state.OffsetTrackId);
            }

            if (state.WidthTrackId != 0)
            {
                _timeline.RemoveTrack(state.WidthTrackId);
            }
        }

        _columns.Clear();
        _trackBindings.Clear();
        _lastTimestamp = _stopwatch.Elapsed;
        _slotCount = 0;
    }

    public void Dispose()
    {
        Reset();
        _timeline.Dispose();
        GC.SuppressFinalize(this);
    }

    private static uint ResolveColumnKey(ReadOnlySpan<TreeColumnDefinition> definitions, int index)
    {
        if (definitions.Length > index && definitions[index].Key != 0)
        {
            return definitions[index].Key;
        }

        return (uint)(index + 1);
    }

    private ColumnState GetOrCreateState(uint key, TreeColumnSlot target)
    {
        if (_columns.TryGetValue(key, out var state))
        {
            return state;
        }

        state = new ColumnState
        {
            Offset = target.Offset,
            Width = target.Width,
            TargetOffset = target.Offset,
            TargetWidth = target.Width,
        };
        _columns[key] = state;
        return state;
    }

    private void UpdateSpringTrack(uint key, ColumnProperty property, double targetValue)
    {
        if (!_columns.TryGetValue(key, out var state))
        {
            return;
        }

        var (trackId, currentValue) = property == ColumnProperty.Offset
            ? (state.OffsetTrackId, state.Offset)
            : (state.WidthTrackId, state.Width);

        if (property == ColumnProperty.Offset)
        {
            state.TargetOffset = targetValue;
        }
        else
        {
            state.TargetWidth = targetValue;
        }

        if (Math.Abs(currentValue - targetValue) < 0.25)
        {
            if (property == ColumnProperty.Offset)
            {
                state.Offset = targetValue;
                state.OffsetTrackId = 0;
            }
            else
            {
                state.Width = targetValue;
                state.WidthTrackId = 0;
            }

            if (trackId != 0)
            {
                _timeline.RemoveTrack(trackId);
                _trackBindings.Remove(trackId);
            }

            return;
        }

        if (trackId == 0)
        {
            var descriptor = new TimelineSpringTrackDescriptor(
                nodeId: 0,
                channelId: GenerateChannelId(key, property),
                stiffness: _springStiffness,
                damping: _springDamping,
                mass: _springMass,
                startValue: (float)currentValue,
                initialVelocity: 0.0f,
                targetValue: (float)targetValue,
                restVelocity: _restVelocity,
                restOffset: _restOffset,
                dirtyBinding: TimelineDirtyBinding.None);

            var created = _timeline.AddSpringTrack(_timelineGroup, descriptor);
            if (created == uint.MaxValue)
            {
                if (property == ColumnProperty.Offset)
                {
                    state.Offset = targetValue;
                }
                else
                {
                    state.Width = targetValue;
                }
                return;
            }

            _trackBindings[created] = new TrackBinding(key, property);
            if (property == ColumnProperty.Offset)
            {
                state.OffsetTrackId = created;
            }
            else
            {
                state.WidthTrackId = created;
            }
        }
        else
        {
            _timeline.SetSpringTarget(trackId, (float)targetValue);
        }
    }

    private void EnsureSlotCapacity(int required)
    {
        if (_slotBuffer.Length < required)
        {
            Array.Resize(
                ref _slotBuffer,
                Math.Max(required, _slotBuffer.Length == 0 ? 4 : _slotBuffer.Length * 2));
        }
    }

    private void EnsureOrderCapacity(int required)
    {
        if (_columnOrder.Length < required)
        {
            Array.Resize(
                ref _columnOrder,
                Math.Max(required, _columnOrder.Length == 0 ? 4 : _columnOrder.Length * 2));
        }
    }

    private void CopyStatesToBuffer()
    {
        if (_slotCount == 0)
        {
            return;
        }

        for (var i = 0; i < _slotCount; i++)
        {
            var key = _columnOrder[i];
            if (_columns.TryGetValue(key, out var state))
            {
                _slotBuffer[i] = new TreeColumnSlot(state.Offset, state.Width);
            }
            else
            {
                _slotBuffer[i] = default;
            }
        }
    }

    private bool TickTimeline()
    {
        var trackCount = _trackBindings.Count;
        if (trackCount == 0)
        {
            _lastTimestamp = _stopwatch.Elapsed;
            return false;
        }

        var now = _stopwatch.Elapsed;
        var delta = now - _lastTimestamp;
        if (delta <= TimeSpan.Zero)
        {
            return false;
        }

        _lastTimestamp = now;

        Span<TimelineSample> buffer = trackCount <= 32
            ? stackalloc TimelineSample[trackCount]
            : new TimelineSample[trackCount];

        var produced = _timeline.Tick(delta, cache: null, buffer);
        var changed = produced > 0;

        for (var i = 0; i < produced; i++)
        {
            ref readonly var sample = ref buffer[i];
            if (!_trackBindings.TryGetValue(sample.TrackId, out var binding))
            {
                continue;
            }

            if (!_columns.TryGetValue(binding.ColumnKey, out var state))
            {
                _trackBindings.Remove(sample.TrackId);
                continue;
            }

            var value = (double)sample.Value;
            if (binding.Property == ColumnProperty.Offset)
            {
                if (Math.Abs(state.Offset - value) > double.Epsilon)
                {
                    state.Offset = value;
                    changed = true;
                }
            }
            else
            {
                if (Math.Abs(state.Width - value) > double.Epsilon)
                {
                    state.Width = value;
                    changed = true;
                }
            }

            if ((sample.Flags & (TimelineSampleFlags.Completed | TimelineSampleFlags.AtRest)) != 0)
            {
                _timeline.RemoveTrack(sample.TrackId);
                _trackBindings.Remove(sample.TrackId);
                if (binding.Property == ColumnProperty.Offset)
                {
                    changed = true;
                    state.OffsetTrackId = 0;
                }
                else
                {
                    changed = true;
                    state.WidthTrackId = 0;
                }
            }

            if ((sample.Flags & TimelineSampleFlags.Completed) != 0)
            {
                // Snap to the final target to avoid drift accumulated during integration.
                if (binding.Property == ColumnProperty.Offset)
                {
                    state.Offset = value;
                }
                else
                {
                    state.Width = value;
                }
            }
        }

        if (produced == 0)
        {
            changed |= ApplyFallback(delta);
        }

        return changed;
    }

    private bool ApplyFallback(TimeSpan delta)
    {
        if (_trackBindings.Count == 0 || delta <= TimeSpan.Zero)
        {
            return false;
        }

        var seconds = delta.TotalSeconds;
        if (seconds <= 0d)
        {
            return false;
        }

        var alpha = 1.0 - Math.Exp(-seconds / 0.2);
        if (alpha <= 0d)
        {
            return false;
        }

        var toRemove = ArrayPool<uint>.Shared.Rent(_trackBindings.Count);
        try
        {
            var removeCount = 0;
            var changed = false;

            foreach (var pair in _trackBindings)
            {
                var trackId = pair.Key;
                var binding = pair.Value;
                if (!_columns.TryGetValue(binding.ColumnKey, out var state))
                {
                    toRemove[removeCount++] = trackId;
                    continue;
                }

                switch (binding.Property)
                {
                    case ColumnProperty.Offset:
                    {
                        var target = state.TargetOffset;
                        var next = state.Offset + ((target - state.Offset) * alpha);
                        if (Math.Abs(next - state.Offset) > double.Epsilon)
                        {
                            state.Offset = next;
                            changed = true;
                        }

                        if (Math.Abs(state.Offset - target) < 0.01)
                        {
                            state.Offset = target;
                            _timeline.RemoveTrack(state.OffsetTrackId);
                            state.OffsetTrackId = 0;
                            toRemove[removeCount++] = trackId;
                        }

                        break;
                    }

                    case ColumnProperty.Width:
                    {
                        var target = state.TargetWidth;
                        var next = state.Width + ((target - state.Width) * alpha);
                        if (Math.Abs(next - state.Width) > double.Epsilon)
                        {
                            state.Width = next;
                            changed = true;
                        }

                        if (Math.Abs(state.Width - target) < 0.01)
                        {
                            state.Width = target;
                            _timeline.RemoveTrack(state.WidthTrackId);
                            state.WidthTrackId = 0;
                            toRemove[removeCount++] = trackId;
                        }

                        break;
                    }
                }
            }

            if (removeCount > 0)
            {
                for (var i = 0; i < removeCount; i++)
                {
                    _trackBindings.Remove(toRemove[i]);
                }
            }

            return changed;
        }
        finally
        {
            ArrayPool<uint>.Shared.Return(toRemove);
        }
    }

    private void RemoveStaleColumns(HashSet<uint> activeKeys)
    {
        if (_columns.Count == activeKeys.Count)
        {
            return;
        }

        var toRemove = new List<uint>();
        foreach (var key in _columns.Keys)
        {
            if (!activeKeys.Contains(key))
            {
                toRemove.Add(key);
            }
        }

        foreach (var key in toRemove)
        {
            if (_columns.TryGetValue(key, out var state))
            {
                if (state.OffsetTrackId != 0)
                {
                    _timeline.RemoveTrack(state.OffsetTrackId);
                }

                if (state.WidthTrackId != 0)
                {
                    _timeline.RemoveTrack(state.WidthTrackId);
                }
            }

            _columns.Remove(key);
        }
    }

    private static ushort GenerateChannelId(uint columnKey, ColumnProperty property)
    {
        var channel = (columnKey * 2) + (property == ColumnProperty.Offset ? 0u : 1u);
        return unchecked((ushort)(channel & 0xFFFF));
    }

    private sealed class ColumnState
    {
        public double Offset { get; set; }
        public double Width { get; set; }
        public double TargetOffset { get; set; }
        public double TargetWidth { get; set; }
        public uint OffsetTrackId { get; set; }
        public uint WidthTrackId { get; set; }
    }

    private readonly record struct TrackBinding(uint ColumnKey, ColumnProperty Property);

    private enum ColumnProperty
    {
        Offset = 0,
        Width = 1,
    }
}

internal static class HashSetPool<T>
    where T : notnull
{
    private static readonly Stack<HashSet<T>> Pool = new();

    public static HashSet<T> Rent()
    {
        lock (Pool)
        {
            if (Pool.TryPop(out var set))
            {
                return set;
            }
        }

        return new HashSet<T>();
    }

    public static void Return(HashSet<T> set)
    {
        set.Clear();
        lock (Pool)
        {
            Pool.Push(set);
        }
    }
}
