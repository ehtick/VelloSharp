using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using VelloSharp.Composition;

namespace VelloSharp.TreeDataGrid.Composition;

/// <summary>
/// Coordinates row-level micro-interactions (height easing, selection glow, caret rotation)
/// using the shared composition timeline runtime.
/// </summary>
internal sealed class TreeRowInteractionAnimator : IDisposable
{
    private readonly TimelineSystem _timeline = new();
    private readonly uint _timelineGroup;
    private readonly Dictionary<uint, RowState> _rows = new();
    private readonly Dictionary<uint, TrackBinding> _trackBindings = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private TimeSpan _lastTimestamp = TimeSpan.Zero;
    private TreeRowAnimationSnapshot[] _snapshotBuffer = Array.Empty<TreeRowAnimationSnapshot>();
    private bool _disposed;
    private TreeRowAnimationProfile _profile;

    public TreeRowInteractionAnimator(TreeRowAnimationProfile? profile = null)
    {
        _profile = profile ?? TreeRowAnimationProfile.Default;
        _timelineGroup = _timeline.CreateGroup(new TimelineGroupConfig());
        _timeline.PlayGroup(_timelineGroup);
    }

    public void UpdateProfile(TreeRowAnimationProfile? profile)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TreeRowInteractionAnimator));
        }

        _profile = profile ?? TreeRowAnimationProfile.Default;
        ApplyProfileToState();
    }

    public void UpdateRows(ReadOnlySpan<TreeRowMetric> metrics)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TreeRowInteractionAnimator));
        }

        for (var i = 0; i < metrics.Length; i++)
        {
            var metric = metrics[i];
            _ = GetOrCreateState(metric.NodeId);
        }

        RemoveMissingRows(metrics);
    }

    public void NotifyExpansion(uint nodeId, bool isExpanded)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TreeRowInteractionAnimator));
        }

        var state = GetOrCreateState(nodeId);
        state.IsExpanded = isExpanded;

        StartHeightAnimation(state, isExpanded);
        StartGlowAnimation(state, isExpanded);
        StartCaretAnimation(state, isExpanded);
    }

    public void Reset()
    {
        foreach (var state in _rows.Values)
        {
            if (state.HeightTrackId != 0)
            {
                _timeline.RemoveTrack(state.HeightTrackId);
            }

            if (state.GlowTrackId != 0)
            {
                _timeline.RemoveTrack(state.GlowTrackId);
            }

            if (state.CaretTrackId != 0)
            {
                _timeline.RemoveTrack(state.CaretTrackId);
            }
        }

        _rows.Clear();
        _trackBindings.Clear();
        _snapshotBuffer = Array.Empty<TreeRowAnimationSnapshot>();
        _lastTimestamp = _stopwatch.Elapsed;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Reset();
        _timeline.Dispose();
    }

    public void Tick()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TreeRowInteractionAnimator));
        }

        if (_trackBindings.Count == 0)
        {
            _lastTimestamp = _stopwatch.Elapsed;
            return;
        }

        var now = _stopwatch.Elapsed;
        var delta = now - _lastTimestamp;
        if (delta <= TimeSpan.Zero)
        {
            return;
        }

        _lastTimestamp = now;

        Span<TimelineSample> buffer = _trackBindings.Count <= 24
            ? stackalloc TimelineSample[_trackBindings.Count]
            : new TimelineSample[_trackBindings.Count];

        var produced = _timeline.Tick(delta, cache: null, buffer);
        for (var i = 0; i < produced; i++)
        {
            ref readonly var sample = ref buffer[i];
            if (!_trackBindings.TryGetValue(sample.TrackId, out var binding))
            {
                continue;
            }

            switch (binding.Kind)
            {
                case TrackTarget.RowHeight:
                    if (_rows.TryGetValue(binding.EntityId, out var heightState))
                    {
                        heightState.HeightFactor = Clamp(sample.Value);
                        if ((sample.Flags & TimelineSampleFlags.Completed) != 0)
                        {
                            _timeline.RemoveTrack(sample.TrackId);
                            _trackBindings.Remove(sample.TrackId);
                            heightState.HeightTrackId = 0;
                            heightState.HeightFactor = Clamp(sample.Value);
                        }
                    }
                    else
                    {
                        _timeline.RemoveTrack(sample.TrackId);
                        _trackBindings.Remove(sample.TrackId);
                    }

                    break;
                case TrackTarget.RowGlow:
                    if (_rows.TryGetValue(binding.EntityId, out var glowState))
                    {
                        glowState.SelectionGlow = Math.Clamp(sample.Value, 0f, 1f);
                        if ((sample.Flags & TimelineSampleFlags.Completed) != 0)
                        {
                            _timeline.RemoveTrack(sample.TrackId);
                            _trackBindings.Remove(sample.TrackId);
                            glowState.GlowTrackId = 0;
                            glowState.SelectionGlow = Math.Clamp(sample.Value, 0f, 1f);
                        }
                    }
                    else
                    {
                        _timeline.RemoveTrack(sample.TrackId);
                        _trackBindings.Remove(sample.TrackId);
                    }

                    break;
                case TrackTarget.RowCaret:
                    if (_rows.TryGetValue(binding.EntityId, out var caretState))
                    {
                        caretState.CaretRotation = sample.Value;
                        if ((sample.Flags & TimelineSampleFlags.Completed) != 0)
                        {
                            _timeline.RemoveTrack(sample.TrackId);
                            _trackBindings.Remove(sample.TrackId);
                            caretState.CaretTrackId = 0;
                            caretState.CaretRotation = sample.Value;
                        }
                    }
                    else
                    {
                        _timeline.RemoveTrack(sample.TrackId);
                        _trackBindings.Remove(sample.TrackId);
                        }

                        break;
            }
        }

        if (produced == 0)
        {
            ApplyFallback(delta);
        }
    }

    public ArraySegment<TreeRowAnimationSnapshot> CaptureSnapshots(IReadOnlyList<TreeRowPlanEntry> rows)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TreeRowInteractionAnimator));
        }

        if (rows.Count == 0)
        {
            return ArraySegment<TreeRowAnimationSnapshot>.Empty;
        }

        if (_snapshotBuffer.Length < rows.Count)
        {
            Array.Resize(ref _snapshotBuffer, rows.Count);
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var entry = rows[i];
            if (_rows.TryGetValue(entry.NodeId, out var state))
            {
                var factor = Clamp(state.HeightFactor);
                var height = Math.Max(entry.Height * factor, entry.Height * GetMinHeightFactor());
                _snapshotBuffer[i] = new TreeRowAnimationSnapshot(
                    entry.NodeId,
                    height,
                    factor,
                    Math.Clamp(state.SelectionGlow, 0f, 1f),
                    state.CaretRotation);
            }
            else
            {
                _snapshotBuffer[i] = new TreeRowAnimationSnapshot(
                    entry.NodeId,
                    entry.Height,
                    1f,
                    0f,
                    _profile.CaretCollapsedDegrees);
            }
        }

        return new ArraySegment<TreeRowAnimationSnapshot>(_snapshotBuffer, 0, rows.Count);
    }

    private void StartHeightAnimation(RowState state, bool isExpanded)
    {
        if (state.HeightTrackId != 0)
        {
            _timeline.RemoveTrack(state.HeightTrackId);
            _trackBindings.Remove(state.HeightTrackId);
            state.HeightTrackId = 0;
        }

        var target = GetMaxHeightFactor();
        var start = isExpanded
            ? Math.Min(state.HeightFactor, _profile.ExpandStartFactor)
            : Math.Min(state.HeightFactor, _profile.CollapseStartFactor);
        start = Clamp(start);
        state.HeightFactor = start;
        state.HeightTarget = target;

        var spring = _profile.HeightSpring;
        if (_profile.ReducedMotionEnabled || !spring.IsEnabled)
        {
            state.HeightFactor = target;
            return;
        }

        var descriptor = new TimelineSpringTrackDescriptor(
            nodeId: 0,
            channelId: GenerateChannel(state.NodeId, RowProperty.Height),
            stiffness: spring.Stiffness,
            damping: spring.Damping,
            mass: spring.Mass,
            startValue: start,
            initialVelocity: 0.0f,
            targetValue: target,
            restVelocity: Math.Max(spring.RestVelocity, 0.0001f),
            restOffset: Math.Max(spring.RestOffset, 0.0001f),
            dirtyBinding: TimelineDirtyBinding.None);

        var trackId = _timeline.AddSpringTrack(_timelineGroup, descriptor);
        if (trackId == uint.MaxValue)
        {
            state.HeightFactor = target;
            return;
        }

        state.HeightTrackId = trackId;
        _trackBindings[trackId] = new TrackBinding(TrackTarget.RowHeight, state.NodeId);
        _timeline.PlayGroup(_timelineGroup);
    }

    private void StartGlowAnimation(RowState state, bool isExpanded)
    {
        if (state.GlowTrackId != 0)
        {
            _timeline.RemoveTrack(state.GlowTrackId);
            _trackBindings.Remove(state.GlowTrackId);
            state.GlowTrackId = 0;
        }

        var timeline = _profile.SelectionGlow;
        var duration = timeline.GetDurationSeconds();
        var target = Math.Clamp(_profile.GlowFalloff, 0f, 1f);
        var start = isExpanded ? _profile.GlowPeak : Math.Max(state.SelectionGlow, _profile.CollapseGlowBaseline);
        start = Math.Clamp(start, 0f, 1f);
        state.SelectionGlow = start;
        state.SelectionTarget = target;

        if (_profile.ReducedMotionEnabled || !timeline.IsEnabled || duration <= 0f)
        {
            state.SelectionGlow = target;
            return;
        }

        var descriptor = new TimelineEasingTrackDescriptor(
            nodeId: 0,
            channelId: GenerateChannel(state.NodeId, RowProperty.Glow),
            startValue: start,
            endValue: target,
            duration: MathF.Max(duration, 0.01f),
            easing: timeline.Easing,
            repeat: timeline.Repeat,
            dirtyBinding: TimelineDirtyBinding.None);

        var trackId = _timeline.AddEasingTrack(_timelineGroup, descriptor);
        if (trackId == uint.MaxValue)
        {
            state.SelectionGlow = target;
            return;
        }

        state.GlowTrackId = trackId;
        _trackBindings[trackId] = new TrackBinding(TrackTarget.RowGlow, state.NodeId);
        _timeline.PlayGroup(_timelineGroup);
    }

    private void StartCaretAnimation(RowState state, bool isExpanded)
    {
        if (state.CaretTrackId != 0)
        {
            _timeline.RemoveTrack(state.CaretTrackId);
            _trackBindings.Remove(state.CaretTrackId);
            state.CaretTrackId = 0;
        }

        var timeline = _profile.CaretRotation;
        var duration = timeline.GetDurationSeconds();
        var target = isExpanded ? _profile.CaretExpandedDegrees : _profile.CaretCollapsedDegrees;
        state.CaretTarget = target;

        if (_profile.ReducedMotionEnabled || !timeline.IsEnabled || duration <= 0f)
        {
            state.CaretRotation = target;
            return;
        }

        var descriptor = new TimelineEasingTrackDescriptor(
            nodeId: 0,
            channelId: GenerateChannel(state.NodeId, RowProperty.Caret),
            startValue: state.CaretRotation,
            endValue: target,
            duration: MathF.Max(duration, 0.01f),
            easing: timeline.Easing,
            repeat: timeline.Repeat,
            dirtyBinding: TimelineDirtyBinding.None);

        var trackId = _timeline.AddEasingTrack(_timelineGroup, descriptor);
        if (trackId == uint.MaxValue)
        {
            state.CaretRotation = target;
            return;
        }

        state.CaretTrackId = trackId;
        _trackBindings[trackId] = new TrackBinding(TrackTarget.RowCaret, state.NodeId);
        _timeline.PlayGroup(_timelineGroup);
    }

    private RowState GetOrCreateState(uint nodeId)
    {
        if (_rows.TryGetValue(nodeId, out var state))
        {
            return state;
        }

        state = new RowState
        {
            NodeId = nodeId,
            HeightFactor = GetMaxHeightFactor(),
            HeightTarget = GetMaxHeightFactor(),
            SelectionGlow = Math.Clamp(_profile.GlowFalloff, 0f, 1f),
            SelectionTarget = Math.Clamp(_profile.GlowFalloff, 0f, 1f),
            CaretRotation = _profile.CaretCollapsedDegrees,
            CaretTarget = _profile.CaretCollapsedDegrees,
            IsExpanded = false,
        };
        _rows[nodeId] = state;
        return state;
    }

    private void RemoveMissingRows(ReadOnlySpan<TreeRowMetric> metrics)
    {
        if (_rows.Count == metrics.Length)
        {
            return;
        }

        var active = HashSetPool<uint>.Rent();
        try
        {
            for (var i = 0; i < metrics.Length; i++)
            {
                active.Add(metrics[i].NodeId);
            }

            var toRemove = new List<uint>();
            foreach (var key in _rows.Keys)
            {
                if (!active.Contains(key))
                {
                    toRemove.Add(key);
                }
            }

            foreach (var nodeId in toRemove)
            {
                if (_rows.Remove(nodeId, out var state))
                {
                    if (state.HeightTrackId != 0)
                    {
                        _timeline.RemoveTrack(state.HeightTrackId);
                        _trackBindings.Remove(state.HeightTrackId);
                    }

                    if (state.GlowTrackId != 0)
                    {
                        _timeline.RemoveTrack(state.GlowTrackId);
                        _trackBindings.Remove(state.GlowTrackId);
                    }

                    if (state.CaretTrackId != 0)
                    {
                        _timeline.RemoveTrack(state.CaretTrackId);
                        _trackBindings.Remove(state.CaretTrackId);
                    }
                }
            }
        }
        finally
        {
            HashSetPool<uint>.Return(active);
        }
    }

    private float Clamp(float value)
    {
        var min = GetMinHeightFactor();
        var max = GetMaxHeightFactor();
        return Math.Clamp(value, min, max);
    }

    private float GetMinHeightFactor()
    {
        var min = Math.Min(_profile.MinHeightFactor, _profile.MaxHeightFactor);
        return min < 0.01f ? 0.01f : min;
    }

    private float GetMaxHeightFactor()
    {
        var min = GetMinHeightFactor();
        var max = Math.Max(_profile.MinHeightFactor, _profile.MaxHeightFactor);
        return max < min ? min : max;
    }

    private void ApplyProfileToState()
    {
        foreach (var state in _rows.Values)
        {
            if (state.HeightTrackId != 0)
            {
                _timeline.RemoveTrack(state.HeightTrackId);
                _trackBindings.Remove(state.HeightTrackId);
                state.HeightTrackId = 0;
            }

            if (state.GlowTrackId != 0)
            {
                _timeline.RemoveTrack(state.GlowTrackId);
                _trackBindings.Remove(state.GlowTrackId);
                state.GlowTrackId = 0;
            }

            if (state.CaretTrackId != 0)
            {
                _timeline.RemoveTrack(state.CaretTrackId);
                _trackBindings.Remove(state.CaretTrackId);
                state.CaretTrackId = 0;
            }

            state.HeightFactor = Clamp(state.HeightFactor);
            state.SelectionGlow = Math.Clamp(state.SelectionGlow, 0f, 1f);
            state.HeightTarget = Clamp(state.HeightTarget);
            state.SelectionTarget = Math.Clamp(state.SelectionTarget, 0f, 1f);

            var minCaret = Math.Min(_profile.CaretCollapsedDegrees, _profile.CaretExpandedDegrees);
            var maxCaret = Math.Max(_profile.CaretCollapsedDegrees, _profile.CaretExpandedDegrees);
            state.CaretRotation = Math.Clamp(state.CaretRotation, minCaret, maxCaret);
            state.CaretTarget = Math.Clamp(state.CaretTarget, minCaret, maxCaret);
        }

        if (_profile.ReducedMotionEnabled)
        {
            foreach (var state in _rows.Values)
            {
                state.HeightFactor = GetMaxHeightFactor();
                state.HeightTarget = GetMaxHeightFactor();
                state.SelectionGlow = Math.Clamp(_profile.GlowFalloff, 0f, 1f);
                state.SelectionTarget = Math.Clamp(_profile.GlowFalloff, 0f, 1f);
                state.CaretRotation = state.IsExpanded
                    ? _profile.CaretExpandedDegrees
                    : _profile.CaretCollapsedDegrees;
                state.CaretTarget = state.CaretRotation;
            }
        }
    }

    private void ApplyFallback(TimeSpan delta)
    {
        if (_trackBindings.Count == 0 || delta <= TimeSpan.Zero)
        {
            return;
        }

        var seconds = (float)delta.TotalSeconds;
        if (seconds <= 0f)
        {
            return;
        }

        var rented = ArrayPool<uint>.Shared.Rent(_trackBindings.Count);
        try
        {
            var removeCount = 0;
            foreach (var pair in _trackBindings)
            {
                var trackId = pair.Key;
                var binding = pair.Value;
                if (!_rows.TryGetValue(binding.EntityId, out var state))
                {
                    rented[removeCount++] = trackId;
                    continue;
                }

                switch (binding.Kind)
                {
                    case TrackTarget.RowHeight:
                    {
                        var next = AdvanceTowards(state.HeightFactor, state.HeightTarget, seconds, 0.03f);
                        state.HeightFactor = Clamp(next);
                        if (Math.Abs(state.HeightFactor - state.HeightTarget) <= 0.005f)
                        {
                            state.HeightFactor = Clamp(state.HeightTarget);
                            _timeline.RemoveTrack(state.HeightTrackId);
                            state.HeightTrackId = 0;
                            rented[removeCount++] = trackId;
                        }

                        break;
                    }

                    case TrackTarget.RowGlow:
                    {
                        var next = AdvanceTowards(state.SelectionGlow, state.SelectionTarget, seconds, 0.12f);
                        state.SelectionGlow = Math.Clamp(next, 0f, 1f);
                        if (Math.Abs(state.SelectionGlow - state.SelectionTarget) <= 0.005f)
                        {
                            state.SelectionGlow = Math.Clamp(state.SelectionTarget, 0f, 1f);
                            _timeline.RemoveTrack(state.GlowTrackId);
                            state.GlowTrackId = 0;
                            rented[removeCount++] = trackId;
                        }

                        break;
                    }

                    case TrackTarget.RowCaret:
                    {
                        var next = AdvanceTowards(state.CaretRotation, state.CaretTarget, seconds, 0.05f);
                        state.CaretRotation = next;
                        if (Math.Abs(state.CaretRotation - state.CaretTarget) <= 0.1f)
                        {
                            state.CaretRotation = state.CaretTarget;
                            _timeline.RemoveTrack(state.CaretTrackId);
                            state.CaretTrackId = 0;
                            rented[removeCount++] = trackId;
                        }

                        break;
                    }
                }
            }

            for (var i = 0; i < removeCount; i++)
            {
                _trackBindings.Remove(rented[i]);
            }
        }
        finally
        {
            ArrayPool<uint>.Shared.Return(rented);
        }
    }

    private static float AdvanceTowards(float current, float target, float deltaSeconds, float timeConstant)
    {
        if (Math.Abs(target - current) <= 0.0001f)
        {
            return target;
        }

        var constant = MathF.Max(timeConstant, 0.01f);
        var alpha = 1f - MathF.Exp(-deltaSeconds / constant);
        alpha = Math.Clamp(alpha, 0f, 1f);
        return current + ((target - current) * alpha);
    }

    private static ushort GenerateChannel(uint nodeId, RowProperty property)
    {
        var baseValue = (nodeId * 4u) + (uint)property;
        return unchecked((ushort)(baseValue & 0xFFFF));
    }

    private enum RowProperty
    {
        Height = 0,
        Glow = 1,
        Caret = 2,
    }

    private enum TrackTarget
    {
        RowHeight = 0,
        RowGlow = 1,
        RowCaret = 2,
    }

    private sealed class RowState
    {
        public uint NodeId;
        public float HeightFactor = 1f;
        public float HeightTarget = 1f;
        public float SelectionGlow;
        public float SelectionTarget;
        public float CaretRotation;
        public float CaretTarget;
        public bool IsExpanded;
        public uint HeightTrackId;
        public uint GlowTrackId;
        public uint CaretTrackId;
    }

    private readonly record struct TrackBinding(TrackTarget Kind, uint EntityId);
}
