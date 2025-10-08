using System;
using System.Collections.Generic;
using System.Linq;
using VelloSharp.Composition;
using VelloSharp.ChartRuntime;
using AnnotationOverlay = VelloSharp.ChartEngine.ChartFrameMetadata.ChartAnnotationOverlay;
using CursorOverlay = VelloSharp.ChartEngine.ChartFrameMetadata.ChartCursorOverlay;
using StreamingOverlay = VelloSharp.ChartEngine.ChartFrameMetadata.ChartStreamingOverlay;

namespace VelloSharp.ChartEngine;

internal sealed class ChartAnimationController : IDisposable
{
    private readonly ChartEngine _engine;
    private readonly RenderScheduler _scheduler;
    private ChartAnimationProfile _profile;
    private readonly TimelineSystem _timeline = new();
    private readonly uint _timelineGroup;
    private readonly Dictionary<uint, SeriesAnimation> _series = new();
    private readonly Dictionary<uint, TrackBinding> _trackBindings = new();
    private readonly List<ChartSeriesOverride> _overrideBuffer = new();
    private readonly CursorAnimation _cursor = new();
    private readonly Dictionary<string, AnnotationAnimation> _annotationAnimations = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AnnotationOverlay> _annotationSnapshot = new();
    private readonly Dictionary<uint, StreamingAnimationState> _streamingAnimations = new();
    private readonly object _gate = new();
    private readonly double _defaultStrokeWidth;

    private TimeSpan _lastTimestamp;
    private bool _scheduled;
    private bool _disposed;

    private const double MinimumStrokeWidth = 0.05;

    public ChartAnimationController(ChartEngine engine, RenderScheduler scheduler, double defaultStrokeWidth, ChartAnimationProfile profile)
    {
        _engine = engine;
        _scheduler = scheduler;
        _profile = profile ?? ChartAnimationProfile.Default;
        _defaultStrokeWidth = defaultStrokeWidth;
        _timelineGroup = _timeline.CreateGroup(new TimelineGroupConfig());
    }

    public void UpdateProfile(ChartAnimationProfile profile)
    {
        if (_disposed)
        {
            return;
        }

        _profile = profile ?? ChartAnimationProfile.Default;

        if (!_profile.ReducedMotionEnabled)
        {
            return;
        }

        var trackIds = new List<uint>(_trackBindings.Keys);
        foreach (var trackId in trackIds)
        {
            _timeline.RemoveTrack(trackId);
        }
        _trackBindings.Clear();

        foreach (var pair in _series)
        {
            ApplyImmediateStrokeWidth(pair.Key, pair.Value, pair.Value.BaseStrokeWidth, clearOnComplete: false, flush: false);
        }

        ApplyImmediateCursor(_cursor.CurrentTimeSeconds, _cursor.CurrentValue, _cursor.Visible, flush: false);

        foreach (var annotationId in _annotationAnimations.Keys.ToArray())
        {
            var annotation = _annotationAnimations[annotationId];
            annotation.TrackId = 0;
            _annotationAnimations[annotationId] = annotation;
        }

        foreach (var state in _streamingAnimations.Values)
        {
            if (state.FadeTrackId != 0)
            {
                _timeline.RemoveTrack(state.FadeTrackId);
                _trackBindings.Remove(state.FadeTrackId);
                state.FadeTrackId = 0;
            }

            if (state.SlideTrackId != 0)
            {
                _timeline.RemoveTrack(state.SlideTrackId);
                _trackBindings.Remove(state.SlideTrackId);
                state.SlideTrackId = 0;
            }

            if (state.ShiftTrackId != 0)
            {
                _timeline.RemoveTrack(state.ShiftTrackId);
                _trackBindings.Remove(state.ShiftTrackId);
                state.ShiftTrackId = 0;
            }

            state.Fade = 0f;
            state.Slide = 0f;
            state.ShiftSeconds = 0f;
        }

        _streamingAnimations.Clear();

        FlushOverrides();
    }

    public void RegisterDefinitions(ReadOnlySpan<ChartSeriesDefinition> definitions)
    {
        if (_disposed)
        {
            return;
        }

        var seen = HashSetPool<uint>.Rent();
        try
        {
            foreach (var definition in definitions)
            {
                if (definition is null)
                {
                    continue;
                }

                var baseStroke = (float)(definition.StrokeWidth ?? _defaultStrokeWidth);
                baseStroke = Math.Max(baseStroke, (float)MinimumStrokeWidth);

                var seriesId = unchecked((uint)definition.SeriesId);
                seen.Add(seriesId);

                if (!_series.TryGetValue(seriesId, out var state))
                {
                    state = new SeriesAnimation
                    {
                        BaseStrokeWidth = baseStroke,
                        CurrentStrokeWidth = 0f,
                    };
                    _series[seriesId] = state;
                    StartStrokeAnimation(seriesId, state, 0f, baseStroke, _profile.SeriesStroke, clearOnComplete: false);
                }
                else
                {
                    if (Math.Abs(state.BaseStrokeWidth - baseStroke) > 0.001f)
                    {
                        state.BaseStrokeWidth = baseStroke;
                        StartStrokeAnimation(seriesId, state, state.CurrentStrokeWidth, baseStroke, _profile.SeriesStroke, clearOnComplete: false);
                    }
                    else
                    {
                        state.BaseStrokeWidth = baseStroke;
                        state.CurrentStrokeWidth = Math.Max(state.CurrentStrokeWidth, baseStroke);
                    }
                }
            }

            if (seen.Count == 0 && definitions.Length == 0)
            {
                ClearAll();
            }
            else
            {
                RemoveMissingSeries(seen);
            }
        }
        finally
        {
            HashSetPool<uint>.Return(seen);
        }
    }

    public void AnimateStrokeWidth(uint seriesId, double targetStrokeWidth, TimeSpan duration)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ChartAnimationController));
        }

        var state = GetOrCreateSeries(seriesId);
        var target = (float)Math.Max(targetStrokeWidth, MinimumStrokeWidth);
        var start = state.StrokeTrackId != 0 ? state.CurrentStrokeWidth : state.CurrentStrokeWidth;
        var timeline = new ChartAnimationTimeline(duration, _profile.SeriesStroke.Easing, _profile.SeriesStroke.Repeat);
        StartStrokeAnimation(seriesId, state, start, target, timeline, clearOnComplete: false);
    }

    public void ResetStrokeWidth(uint seriesId, TimeSpan duration)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ChartAnimationController));
        }

        if (!_series.TryGetValue(seriesId, out var state))
        {
            return;
        }

        var timeline = new ChartAnimationTimeline(duration, _profile.SeriesStroke.Easing, _profile.SeriesStroke.Repeat);
        StartStrokeAnimation(seriesId, state, state.CurrentStrokeWidth, state.BaseStrokeWidth, timeline, clearOnComplete: true);
    }

    public void AnimateCursor(in ChartCursorUpdate update)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ChartAnimationController));
        }

        var positionTimeline = update.PositionDuration.HasValue
            ? new ChartAnimationTimeline(update.PositionDuration.Value, _profile.CursorTrail.Easing, _profile.CursorTrail.Repeat)
            : _profile.CursorTrail;

        var fadeTimeline = update.FadeDuration.HasValue
            ? new ChartAnimationTimeline(update.FadeDuration.Value, _profile.CrosshairFade.Easing, _profile.CrosshairFade.Repeat)
            : _profile.CrosshairFade;

        if (_profile.ReducedMotionEnabled)
        {
            ApplyImmediateCursor(update.TimestampSeconds, update.Value, update.IsVisible, flush: false);
            return;
        }

        StartCursorCoordinateTrack(CursorTrackProperty.Time, (float)update.TimestampSeconds, positionTimeline);
        StartCursorCoordinateTrack(CursorTrackProperty.Value, (float)update.Value, positionTimeline);
        StartCursorCoordinateTrack(CursorTrackProperty.Opacity, update.IsVisible ? 1f : 0f, fadeTimeline);
        _cursor.Visible = update.IsVisible;
    }

    public void AnimateAnnotation(string annotationId, bool highlighted, TimeSpan? duration = null)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ChartAnimationController));
        }

        if (string.IsNullOrWhiteSpace(annotationId))
        {
            throw new ArgumentException("Annotation id must be provided.", nameof(annotationId));
        }

        var target = highlighted ? 1f : 0f;
        var timeline = duration.HasValue
            ? new ChartAnimationTimeline(duration.Value, _profile.IndicatorOverlay.Easing, _profile.IndicatorOverlay.Repeat)
            : _profile.IndicatorOverlay;

        if (_profile.ReducedMotionEnabled)
        {
            ApplyImmediateAnnotation(annotationId, target);
            return;
        }

        StartAnnotationTrack(annotationId, target, timeline);
    }

    public void AnimateStreaming(ReadOnlySpan<ChartStreamingUpdate> updates)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ChartAnimationController));
        }

        if (updates.IsEmpty)
        {
            return;
        }

        foreach (var update in updates)
        {
            HandleStreamingUpdate(update);
        }

        EnsureScheduled();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var state in _series.Values)
        {
            if (state.StrokeTrackId != 0)
            {
                _timeline.RemoveTrack(state.StrokeTrackId);
                _trackBindings.Remove(state.StrokeTrackId);
            }
        }

        if (_cursor.TimeTrackId != 0)
        {
            _timeline.RemoveTrack(_cursor.TimeTrackId);
            _trackBindings.Remove(_cursor.TimeTrackId);
            _cursor.TimeTrackId = 0;
        }

        if (_cursor.ValueTrackId != 0)
        {
            _timeline.RemoveTrack(_cursor.ValueTrackId);
            _trackBindings.Remove(_cursor.ValueTrackId);
            _cursor.ValueTrackId = 0;
        }

        if (_cursor.OpacityTrackId != 0)
        {
            _timeline.RemoveTrack(_cursor.OpacityTrackId);
            _trackBindings.Remove(_cursor.OpacityTrackId);
            _cursor.OpacityTrackId = 0;
        }

        _cursor.Visible = false;
        _cursor.Opacity = 0f;

        foreach (var annotation in _annotationAnimations.Values)
        {
            if (annotation.TrackId != 0)
            {
                _timeline.RemoveTrack(annotation.TrackId);
                _trackBindings.Remove(annotation.TrackId);
            }
        }

        _annotationAnimations.Clear();
        _annotationSnapshot.Clear();

        foreach (var state in _streamingAnimations.Values)
        {
            if (state.FadeTrackId != 0)
            {
                _timeline.RemoveTrack(state.FadeTrackId);
                _trackBindings.Remove(state.FadeTrackId);
                state.FadeTrackId = 0;
            }

            if (state.SlideTrackId != 0)
            {
                _timeline.RemoveTrack(state.SlideTrackId);
                _trackBindings.Remove(state.SlideTrackId);
                state.SlideTrackId = 0;
            }

            if (state.ShiftTrackId != 0)
            {
                _timeline.RemoveTrack(state.ShiftTrackId);
                _trackBindings.Remove(state.ShiftTrackId);
                state.ShiftTrackId = 0;
            }
        }

        _streamingAnimations.Clear();

        _series.Clear();
        _trackBindings.Clear();
        _overrideBuffer.Clear();
        _timeline.Dispose();
    }

    private SeriesAnimation GetOrCreateSeries(uint seriesId)
    {
        if (_series.TryGetValue(seriesId, out var state))
        {
            return state;
        }

        state = new SeriesAnimation
        {
            BaseStrokeWidth = (float)_defaultStrokeWidth,
            CurrentStrokeWidth = (float)_defaultStrokeWidth,
        };
        _series[seriesId] = state;
        return state;
    }

    private void StartStrokeAnimation(uint seriesId, SeriesAnimation state, float from, float to, ChartAnimationTimeline timeline, bool clearOnComplete)
    {
        if (_disposed)
        {
            return;
        }

        if (state.StrokeTrackId != 0)
        {
            _timeline.RemoveTrack(state.StrokeTrackId);
            _trackBindings.Remove(state.StrokeTrackId);
            state.StrokeTrackId = 0;
        }

        from = Math.Max(from, (float)MinimumStrokeWidth);
        to = Math.Max(to, (float)MinimumStrokeWidth);

        if (_profile.ReducedMotionEnabled || !timeline.IsEnabled)
        {
            var immediateTarget = clearOnComplete ? state.BaseStrokeWidth : to;
            ApplyImmediateStrokeWidth(seriesId, state, immediateTarget, clearOnComplete);
            return;
        }

        var durationSeconds = timeline.GetDurationSeconds();
        if (durationSeconds <= 0f)
        {
            var immediateTarget = clearOnComplete ? state.BaseStrokeWidth : to;
            ApplyImmediateStrokeWidth(seriesId, state, immediateTarget, clearOnComplete);
            return;
        }

        var descriptor = new TimelineEasingTrackDescriptor(
            nodeId: 0,
            channelId: GenerateChannel(seriesId),
            startValue: from,
            endValue: to,
            duration: MathF.Max(durationSeconds, 0.05f),
            easing: timeline.Easing,
            repeat: timeline.Repeat,
            dirtyBinding: TimelineDirtyBinding.None);

        var trackId = _timeline.AddEasingTrack(_timelineGroup, descriptor);
        if (trackId == uint.MaxValue)
        {
            var immediateTarget = clearOnComplete ? state.BaseStrokeWidth : to;
            ApplyImmediateStrokeWidth(seriesId, state, immediateTarget, clearOnComplete);
            return;
        }

        state.StrokeTrackId = trackId;
        state.ClearOnComplete = clearOnComplete;
        state.CurrentStrokeWidth = from;
        _trackBindings[trackId] = new TrackBinding(AnimationTrackTarget.SeriesStrokeWidth, seriesId, null);

        EnsureScheduled();
    }

    private void ApplyImmediateStrokeWidth(uint seriesId, SeriesAnimation state, float target, bool clearOnComplete, bool flush = true)
    {
        state.StrokeTrackId = 0;
        state.ClearOnComplete = false;
        var normalized = Math.Max(target, (float)MinimumStrokeWidth);

        if (clearOnComplete)
        {
            var baseWidth = Math.Max(state.BaseStrokeWidth, (float)MinimumStrokeWidth);
            state.CurrentStrokeWidth = baseWidth;
            if (Math.Abs(baseWidth - _defaultStrokeWidth) < 0.001f)
            {
                _overrideBuffer.Add(new ChartSeriesOverride((int)seriesId).ClearStrokeWidth());
            }
            else
            {
                _overrideBuffer.Add(new ChartSeriesOverride((int)seriesId).WithStrokeWidth(baseWidth));
            }
        }
        else
        {
            state.CurrentStrokeWidth = normalized;
            if (Math.Abs(normalized - _defaultStrokeWidth) < 0.001f)
            {
                _overrideBuffer.Add(new ChartSeriesOverride((int)seriesId).ClearStrokeWidth());
            }
            else
            {
                _overrideBuffer.Add(new ChartSeriesOverride((int)seriesId).WithStrokeWidth(normalized));
            }
        }

        if (flush)
        {
            FlushOverrides();
        }
    }

    private void FlushOverrides()
    {
        if (_overrideBuffer.Count == 0)
        {
            return;
        }

        var count = _overrideBuffer.Count;
        var overrides = new ChartSeriesOverride[count];
        for (var i = 0; i < count; i++)
        {
            overrides[i] = _overrideBuffer[i];
        }

        _engine.ApplySeriesOverrides(overrides);
        _overrideBuffer.Clear();
    }

    private void StartCursorCoordinateTrack(CursorTrackProperty property, float target, ChartAnimationTimeline timeline)
    {
        var duration = timeline.GetDurationSeconds();
        if (_profile.ReducedMotionEnabled || !timeline.IsEnabled || duration <= 0f)
        {
            ApplyCursorValue(property, target);
            return;
        }

        ref uint trackId = ref GetCursorTrackId(property);
        ClearCursorTrack(ref trackId);

        var start = property switch
        {
            CursorTrackProperty.Time => (float)_cursor.CurrentTimeSeconds,
            CursorTrackProperty.Value => (float)_cursor.CurrentValue,
            CursorTrackProperty.Opacity => _cursor.Opacity,
            _ => 0f,
        };

        if (Math.Abs(start - target) < 0.0001f)
        {
            ApplyCursorValue(property, target);
            return;
        }

        var descriptor = new TimelineEasingTrackDescriptor(
            nodeId: 0,
            channelId: GenerateCursorChannel(property),
            startValue: start,
            endValue: target,
            duration: MathF.Max(duration, 0.05f),
            easing: timeline.Easing,
            repeat: timeline.Repeat,
            dirtyBinding: TimelineDirtyBinding.None);

        trackId = _timeline.AddEasingTrack(_timelineGroup, descriptor);
        if (trackId == uint.MaxValue)
        {
            ApplyCursorValue(property, target);
            return;
        }

        var targetKind = property switch
        {
            CursorTrackProperty.Time => AnimationTrackTarget.CursorTime,
            CursorTrackProperty.Value => AnimationTrackTarget.CursorValue,
            CursorTrackProperty.Opacity => AnimationTrackTarget.CursorOpacity,
            _ => AnimationTrackTarget.CursorTime,
        };

        _trackBindings[trackId] = new TrackBinding(targetKind, 0, null);
        EnsureScheduled();
    }

    private ref uint GetCursorTrackId(CursorTrackProperty property)
    {
        switch (property)
        {
            case CursorTrackProperty.Time:
                return ref _cursor.TimeTrackId;
            case CursorTrackProperty.Value:
                return ref _cursor.ValueTrackId;
            case CursorTrackProperty.Opacity:
                return ref _cursor.OpacityTrackId;
            default:
                throw new ArgumentOutOfRangeException(nameof(property), property, null);
        }
    }

    private void ClearCursorTrack(ref uint trackId)
    {
        if (trackId == 0)
        {
            return;
        }

        _timeline.RemoveTrack(trackId);
        _trackBindings.Remove(trackId);
        trackId = 0;
    }

    private void ApplyCursorValue(CursorTrackProperty property, float value)
    {
        switch (property)
        {
            case CursorTrackProperty.Time:
                _cursor.CurrentTimeSeconds = value;
                break;
            case CursorTrackProperty.Value:
                _cursor.CurrentValue = value;
                break;
            case CursorTrackProperty.Opacity:
                _cursor.Opacity = Math.Clamp(value, 0f, 1f);
                break;
        }
    }

    private void ApplyImmediateCursor(double timestampSeconds, double value, bool visible, bool flush)
    {
        ClearCursorTrack(ref _cursor.TimeTrackId);
        ClearCursorTrack(ref _cursor.ValueTrackId);
        ClearCursorTrack(ref _cursor.OpacityTrackId);

        _cursor.CurrentTimeSeconds = timestampSeconds;
        _cursor.CurrentValue = value;
        _cursor.Visible = visible;
        _cursor.Opacity = visible ? 1f : 0f;

        if (flush)
        {
            FlushOverrides();
        }
    }

    private void StartAnnotationTrack(string annotationId, float target, ChartAnimationTimeline timeline)
    {
        var duration = timeline.GetDurationSeconds();
        if (_profile.ReducedMotionEnabled || !timeline.IsEnabled || duration <= 0f)
        {
            ApplyImmediateAnnotation(annotationId, target);
            return;
        }

        var state = _annotationAnimations.TryGetValue(annotationId, out var existing)
            ? existing
            : new AnnotationAnimation();

        if (state.TrackId != 0)
        {
            _timeline.RemoveTrack(state.TrackId);
            _trackBindings.Remove(state.TrackId);
            state.TrackId = 0;
        }

        var descriptor = new TimelineEasingTrackDescriptor(
            nodeId: 0,
            channelId: GenerateAnnotationChannel(annotationId),
            startValue: state.Emphasis,
            endValue: target,
            duration: MathF.Max(duration, 0.05f),
            easing: timeline.Easing,
            repeat: timeline.Repeat,
            dirtyBinding: TimelineDirtyBinding.None);

        var trackId = _timeline.AddEasingTrack(_timelineGroup, descriptor);
        if (trackId == uint.MaxValue)
        {
            state.Emphasis = Math.Clamp(target, 0f, 1f);
            if (state.Emphasis <= 0.001f)
            {
                _annotationAnimations.Remove(annotationId);
            }
            else
            {
                _annotationAnimations[annotationId] = state;
            }

            return;
        }

        state.TrackId = trackId;
        _annotationAnimations[annotationId] = state;
        _trackBindings[trackId] = new TrackBinding(AnimationTrackTarget.AnnotationEmphasis, 0, annotationId);
        EnsureScheduled();
    }

    private void ApplyImmediateAnnotation(string annotationId, float emphasis)
    {
        if (_annotationAnimations.TryGetValue(annotationId, out var state))
        {
            if (state.TrackId != 0)
            {
                _timeline.RemoveTrack(state.TrackId);
                _trackBindings.Remove(state.TrackId);
                state.TrackId = 0;
            }
        }
        else
        {
            state = new AnnotationAnimation();
        }

        state.Emphasis = Math.Clamp(emphasis, 0f, 1f);
        if (state.Emphasis <= 0.001f)
        {
            _annotationAnimations.Remove(annotationId);
        }
        else
        {
            _annotationAnimations[annotationId] = state;
        }
    }

    private void HandleStreamingUpdate(in ChartStreamingUpdate update)
    {
        var seriesId = update.SeriesId;
        switch (update.Kind)
        {
            case ChartStreamingEventKind.FadeIn:
                StartStreamingTrack(seriesId, StreamingTrackProperty.Fade, 1f, 0f, _profile.Streaming.FadeIn);
                break;
            case ChartStreamingEventKind.SlideIn:
                StartStreamingTrack(seriesId, StreamingTrackProperty.Slide, 1f, 0f, _profile.Streaming.SlideIn);
                break;
            case ChartStreamingEventKind.RollingWindowShift:
                var shift = (float)Math.Clamp(update.ShiftSeconds, float.MinValue, float.MaxValue);
                StartStreamingTrack(seriesId, StreamingTrackProperty.Shift, shift, 0f, _profile.Streaming.RollingWindowShift);
                break;
        }
    }

    private void StartStreamingTrack(
        uint seriesId,
        StreamingTrackProperty property,
        float start,
        float target,
        ChartAnimationTimeline timeline)
    {
        var state = GetOrCreateStreamingState(seriesId);
        var duration = timeline.GetDurationSeconds();

        float clampedTarget = property switch
        {
            StreamingTrackProperty.Fade => Math.Clamp(target, 0f, 1f),
            StreamingTrackProperty.Slide => Math.Clamp(target, 0f, 1f),
            _ => target,
        };

        float clampedStart = property switch
        {
            StreamingTrackProperty.Fade => Math.Clamp(start, 0f, 1f),
            StreamingTrackProperty.Slide => Math.Clamp(start, 0f, 1f),
            _ => start,
        };

        switch (property)
        {
            case StreamingTrackProperty.Fade:
                if (state.FadeTrackId != 0)
                {
                    _timeline.RemoveTrack(state.FadeTrackId);
                    _trackBindings.Remove(state.FadeTrackId);
                    state.FadeTrackId = 0;
                }

                state.Fade = clampedStart;
                break;

            case StreamingTrackProperty.Slide:
                if (state.SlideTrackId != 0)
                {
                    _timeline.RemoveTrack(state.SlideTrackId);
                    _trackBindings.Remove(state.SlideTrackId);
                    state.SlideTrackId = 0;
                }

                state.Slide = clampedStart;
                break;

            case StreamingTrackProperty.Shift:
                if (state.ShiftTrackId != 0)
                {
                    _timeline.RemoveTrack(state.ShiftTrackId);
                    _trackBindings.Remove(state.ShiftTrackId);
                    state.ShiftTrackId = 0;
                }

                state.ShiftSeconds = clampedStart;
                break;
        }

        if (_profile.ReducedMotionEnabled || !timeline.IsEnabled || duration <= 0f)
        {
            ApplyImmediateStreaming(seriesId, property, clampedTarget);
            return;
        }

        var descriptor = new TimelineEasingTrackDescriptor(
            nodeId: 0,
            channelId: GenerateStreamingChannel(seriesId, property),
            startValue: clampedStart,
            endValue: clampedTarget,
            duration: MathF.Max(duration, 0.01f),
            easing: timeline.Easing,
            repeat: timeline.Repeat,
            dirtyBinding: TimelineDirtyBinding.None);

        var trackId = _timeline.AddEasingTrack(_timelineGroup, descriptor);
        if (trackId == uint.MaxValue)
        {
            ApplyImmediateStreaming(seriesId, property, clampedTarget);
            return;
        }

        switch (property)
        {
            case StreamingTrackProperty.Fade:
                state.FadeTrackId = trackId;
                _trackBindings[trackId] = new TrackBinding(AnimationTrackTarget.StreamingFade, seriesId, null);
                break;
            case StreamingTrackProperty.Slide:
                state.SlideTrackId = trackId;
                _trackBindings[trackId] = new TrackBinding(AnimationTrackTarget.StreamingSlide, seriesId, null);
                break;
            case StreamingTrackProperty.Shift:
                state.ShiftTrackId = trackId;
                _trackBindings[trackId] = new TrackBinding(AnimationTrackTarget.StreamingShift, seriesId, null);
                break;
        }
    }

    private void ApplyImmediateStreaming(uint seriesId, StreamingTrackProperty property, float value)
    {
        if (!_streamingAnimations.TryGetValue(seriesId, out var state))
        {
            if (property == StreamingTrackProperty.Shift && MathF.Abs(value) <= 0.0001f)
            {
                return;
            }

            state = new StreamingAnimationState();
            _streamingAnimations[seriesId] = state;
        }

        switch (property)
        {
            case StreamingTrackProperty.Fade:
                if (state.FadeTrackId != 0)
                {
                    _timeline.RemoveTrack(state.FadeTrackId);
                    _trackBindings.Remove(state.FadeTrackId);
                    state.FadeTrackId = 0;
                }

                state.Fade = Math.Clamp(value, 0f, 1f);
                break;
            case StreamingTrackProperty.Slide:
                if (state.SlideTrackId != 0)
                {
                    _timeline.RemoveTrack(state.SlideTrackId);
                    _trackBindings.Remove(state.SlideTrackId);
                    state.SlideTrackId = 0;
                }

                state.Slide = Math.Clamp(value, 0f, 1f);
                break;
            case StreamingTrackProperty.Shift:
                if (state.ShiftTrackId != 0)
                {
                    _timeline.RemoveTrack(state.ShiftTrackId);
                    _trackBindings.Remove(state.ShiftTrackId);
                    state.ShiftTrackId = 0;
                }

                state.ShiftSeconds = value;
                break;
        }

        if (state.IsDormant)
        {
            _streamingAnimations.Remove(seriesId);
        }
    }

    private StreamingAnimationState GetOrCreateStreamingState(uint seriesId)
    {
        if (!_streamingAnimations.TryGetValue(seriesId, out var state))
        {
            state = new StreamingAnimationState();
            _streamingAnimations[seriesId] = state;
        }

        return state;
    }

    private void ProcessStreamingSample(StreamingTrackProperty property, uint seriesId, in TimelineSample sample)
    {
        if (!_streamingAnimations.TryGetValue(seriesId, out var state))
        {
            _timeline.RemoveTrack(sample.TrackId);
            _trackBindings.Remove(sample.TrackId);
            return;
        }

        switch (property)
        {
            case StreamingTrackProperty.Fade:
                state.Fade = Math.Clamp(sample.Value, 0f, 1f);
                if ((sample.Flags & (TimelineSampleFlags.Completed | TimelineSampleFlags.AtRest)) != 0)
                {
                    _timeline.RemoveTrack(sample.TrackId);
                    _trackBindings.Remove(sample.TrackId);
                    state.FadeTrackId = 0;
                    state.Fade = Math.Clamp(sample.Value, 0f, 1f);
                }

                break;

            case StreamingTrackProperty.Slide:
                state.Slide = Math.Clamp(sample.Value, 0f, 1f);
                if ((sample.Flags & (TimelineSampleFlags.Completed | TimelineSampleFlags.AtRest)) != 0)
                {
                    _timeline.RemoveTrack(sample.TrackId);
                    _trackBindings.Remove(sample.TrackId);
                    state.SlideTrackId = 0;
                    state.Slide = Math.Clamp(sample.Value, 0f, 1f);
                }

                break;

            case StreamingTrackProperty.Shift:
                state.ShiftSeconds = sample.Value;
                if ((sample.Flags & (TimelineSampleFlags.Completed | TimelineSampleFlags.AtRest)) != 0)
                {
                    _timeline.RemoveTrack(sample.TrackId);
                    _trackBindings.Remove(sample.TrackId);
                    state.ShiftTrackId = 0;
                    state.ShiftSeconds = sample.Value;
                }

                break;
        }

        if ((sample.Flags & (TimelineSampleFlags.Completed | TimelineSampleFlags.AtRest)) != 0 && state.IsDormant)
        {
            _streamingAnimations.Remove(seriesId);
        }
    }

    private static ushort GenerateStreamingChannel(uint seriesId, StreamingTrackProperty property)
    {
        ushort offset = property switch
        {
            StreamingTrackProperty.Fade => 0x6000,
            StreamingTrackProperty.Slide => 0x7000,
            StreamingTrackProperty.Shift => 0x8000,
            _ => 0x6000,
        };

        var seriesBits = (ushort)(seriesId & 0x0FFF);
        return (ushort)(offset | seriesBits);
    }

    public CursorOverlay? GetCursorOverlaySnapshot()
    {
        var opacity = Math.Clamp(_cursor.Opacity, 0f, 1f);
        if (opacity <= 0.001f && !_cursor.Visible)
        {
            return null;
        }

        return new CursorOverlay(_cursor.CurrentTimeSeconds, _cursor.CurrentValue, opacity);
    }

    public IReadOnlyList<AnnotationOverlay> GetAnnotationSnapshots()
    {
        if (_annotationAnimations.Count == 0)
        {
            return Array.Empty<AnnotationOverlay>();
        }

        _annotationSnapshot.Clear();
        foreach (var pair in _annotationAnimations)
        {
            var emphasis = Math.Clamp(pair.Value.Emphasis, 0f, 1f);
            if (emphasis <= 0.001f && pair.Value.TrackId == 0)
            {
                continue;
            }

            _annotationSnapshot.Add(new AnnotationOverlay(pair.Key, emphasis));
        }

        if (_annotationSnapshot.Count == 0)
        {
            return Array.Empty<AnnotationOverlay>();
        }

        return _annotationSnapshot.ToArray();
    }

    public IReadOnlyList<StreamingOverlay> GetStreamingOverlaySnapshots()
    {
        if (_streamingAnimations.Count == 0)
        {
            return Array.Empty<StreamingOverlay>();
        }

        var overlays = new List<StreamingOverlay>(_streamingAnimations.Count);
        foreach (var pair in _streamingAnimations)
        {
            var state = pair.Value;
            if (state.IsDormant)
            {
                continue;
            }

            overlays.Add(new StreamingOverlay(
                pair.Key,
                Math.Clamp(state.Fade, 0f, 1f),
                Math.Clamp(state.Slide, -1f, 1f),
                state.ShiftSeconds));
        }

        if (overlays.Count == 0)
        {
            return Array.Empty<StreamingOverlay>();
        }

        return overlays.ToArray();
    }

    private static ushort GenerateCursorChannel(CursorTrackProperty property)
        => (ushort)(0xC000 | (ushort)property);

    private static ushort GenerateAnnotationChannel(string annotationId)
    {
        var hash = (uint)(annotationId?.GetHashCode(StringComparison.OrdinalIgnoreCase) ?? 0);
        return (ushort)(0xB000 | (hash & 0x0FFF));
    }

    private void EnsureScheduled()
    {
        lock (_gate)
        {
            if (_scheduled || _disposed)
            {
                return;
            }

            _scheduled = true;
            _scheduler.Schedule(OnFrameTick);
        }
    }

    private void OnFrameTick(FrameTick tick)
    {
        if (_disposed)
        {
            return;
        }

        TimeSpan delta;
        if (_profile.DeterministicPlaybackEnabled && tick.Budget > TimeSpan.Zero)
        {
            delta = tick.Budget;
            _lastTimestamp += delta;
        }
        else
        {
            delta = tick.Elapsed - _lastTimestamp;
            _lastTimestamp = tick.Elapsed;
            if (delta < TimeSpan.Zero)
            {
                delta = TimeSpan.Zero;
            }
        }

        AdvanceTimeline(delta);

        lock (_gate)
        {
            if (_trackBindings.Count > 0 && !_disposed)
            {
                _scheduler.Schedule(OnFrameTick);
            }
            else
            {
                _scheduled = false;
            }
        }
    }

    private void AdvanceTimeline(TimeSpan delta)
    {
        if (_trackBindings.Count == 0 || delta <= TimeSpan.Zero)
        {
            return;
        }

        Span<TimelineSample> samples = _trackBindings.Count <= 16
            ? stackalloc TimelineSample[_trackBindings.Count]
            : new TimelineSample[_trackBindings.Count];

        var produced = _timeline.Tick(delta, cache: null, samples);
        for (var i = 0; i < produced; i++)
        {
            ref readonly var sample = ref samples[i];
            if (!_trackBindings.TryGetValue(sample.TrackId, out var binding))
            {
                continue;
            }

            switch (binding.Target)
            {
                case AnimationTrackTarget.SeriesStrokeWidth:
                    if (_series.TryGetValue(binding.EntityId, out var seriesState))
                    {
                        var width = Math.Max(sample.Value, (float)MinimumStrokeWidth);
                        seriesState.CurrentStrokeWidth = width;
                        _overrideBuffer.Add(new ChartSeriesOverride((int)binding.EntityId).WithStrokeWidth(width));

                        if ((sample.Flags & (TimelineSampleFlags.Completed | TimelineSampleFlags.AtRest)) != 0)
                        {
                            _timeline.RemoveTrack(sample.TrackId);
                            _trackBindings.Remove(sample.TrackId);
                            seriesState.StrokeTrackId = 0;

                            if (seriesState.ClearOnComplete)
                            {
                                seriesState.ClearOnComplete = false;
                                var baseWidth = Math.Max(seriesState.BaseStrokeWidth, (float)MinimumStrokeWidth);
                                seriesState.CurrentStrokeWidth = baseWidth;
                                if (Math.Abs(baseWidth - _defaultStrokeWidth) < 0.001f)
                                {
                                    _overrideBuffer.Add(new ChartSeriesOverride((int)binding.EntityId).ClearStrokeWidth());
                                }
                                else
                                {
                                    _overrideBuffer.Add(new ChartSeriesOverride((int)binding.EntityId).WithStrokeWidth(baseWidth));
                                }
                            }
                        }
                    }
                    else
                    {
                        _timeline.RemoveTrack(sample.TrackId);
                        _trackBindings.Remove(sample.TrackId);
                    }

                    break;

                case AnimationTrackTarget.CursorTime:
                    _cursor.CurrentTimeSeconds = sample.Value;
                    if ((sample.Flags & (TimelineSampleFlags.Completed | TimelineSampleFlags.AtRest)) != 0)
                    {
                        _timeline.RemoveTrack(sample.TrackId);
                        _trackBindings.Remove(sample.TrackId);
                        _cursor.TimeTrackId = 0;
                    }

                    break;

                case AnimationTrackTarget.CursorValue:
                    _cursor.CurrentValue = sample.Value;
                    if ((sample.Flags & (TimelineSampleFlags.Completed | TimelineSampleFlags.AtRest)) != 0)
                    {
                        _timeline.RemoveTrack(sample.TrackId);
                        _trackBindings.Remove(sample.TrackId);
                        _cursor.ValueTrackId = 0;
                    }

                    break;

                case AnimationTrackTarget.CursorOpacity:
                    _cursor.Opacity = Math.Clamp(sample.Value, 0f, 1f);
                    if ((sample.Flags & (TimelineSampleFlags.Completed | TimelineSampleFlags.AtRest)) != 0)
                    {
                        _timeline.RemoveTrack(sample.TrackId);
                        _trackBindings.Remove(sample.TrackId);
                        _cursor.OpacityTrackId = 0;
                    }

                    break;

                case AnimationTrackTarget.AnnotationEmphasis:
                    if (binding.AnnotationId is string annotationId && _annotationAnimations.TryGetValue(annotationId, out var annotationState))
                    {
                        annotationState.Emphasis = Math.Clamp(sample.Value, 0f, 1f);
                        if ((sample.Flags & (TimelineSampleFlags.Completed | TimelineSampleFlags.AtRest)) != 0)
                        {
                            _timeline.RemoveTrack(sample.TrackId);
                            _trackBindings.Remove(sample.TrackId);
                            annotationState.TrackId = 0;
                        }

                        if (annotationState.Emphasis <= 0.001f && annotationState.TrackId == 0)
                        {
                            _annotationAnimations.Remove(annotationId);
                        }
                        else
                        {
                            _annotationAnimations[annotationId] = annotationState;
                        }
                    }
                    else
                    {
                        _timeline.RemoveTrack(sample.TrackId);
                        _trackBindings.Remove(sample.TrackId);
                    }

                    break;

                case AnimationTrackTarget.StreamingFade:
                    ProcessStreamingSample(StreamingTrackProperty.Fade, binding.EntityId, sample);
                    break;

                case AnimationTrackTarget.StreamingSlide:
                    ProcessStreamingSample(StreamingTrackProperty.Slide, binding.EntityId, sample);
                    break;

                case AnimationTrackTarget.StreamingShift:
                    ProcessStreamingSample(StreamingTrackProperty.Shift, binding.EntityId, sample);
                    break;
            }
        }

        FlushOverrides();
    }

    private void RemoveMissingSeries(HashSet<uint> seen)
    {
        if (_series.Count == seen.Count)
        {
            return;
        }

        var toRemove = new List<uint>();
        foreach (var key in _series.Keys)
        {
            if (!seen.Contains(key))
            {
                toRemove.Add(key);
            }
        }

        foreach (var key in toRemove)
        {
            if (_series.TryGetValue(key, out var state))
            {
                if (state.StrokeTrackId != 0)
                {
                    _timeline.RemoveTrack(state.StrokeTrackId);
                    _trackBindings.Remove(state.StrokeTrackId);
                }
            }

            _series.Remove(key);
        }
    }

    private void ClearAll()
    {
        foreach (var state in _series.Values)
        {
            if (state.StrokeTrackId != 0)
            {
                _timeline.RemoveTrack(state.StrokeTrackId);
                _trackBindings.Remove(state.StrokeTrackId);
                state.StrokeTrackId = 0;
            }
        }

        if (_cursor.TimeTrackId != 0)
        {
            _timeline.RemoveTrack(_cursor.TimeTrackId);
            _trackBindings.Remove(_cursor.TimeTrackId);
            _cursor.TimeTrackId = 0;
        }

        if (_cursor.ValueTrackId != 0)
        {
            _timeline.RemoveTrack(_cursor.ValueTrackId);
            _trackBindings.Remove(_cursor.ValueTrackId);
            _cursor.ValueTrackId = 0;
        }

        if (_cursor.OpacityTrackId != 0)
        {
            _timeline.RemoveTrack(_cursor.OpacityTrackId);
            _trackBindings.Remove(_cursor.OpacityTrackId);
            _cursor.OpacityTrackId = 0;
        }

        _cursor.Visible = false;
        _cursor.Opacity = 0f;

        foreach (var annotation in _annotationAnimations.Values)
        {
            if (annotation.TrackId != 0)
            {
                _timeline.RemoveTrack(annotation.TrackId);
                _trackBindings.Remove(annotation.TrackId);
            }
        }

        _annotationAnimations.Clear();
        _annotationSnapshot.Clear();

        foreach (var state in _streamingAnimations.Values)
        {
            if (state.FadeTrackId != 0)
            {
                _timeline.RemoveTrack(state.FadeTrackId);
                _trackBindings.Remove(state.FadeTrackId);
                state.FadeTrackId = 0;
            }

            if (state.SlideTrackId != 0)
            {
                _timeline.RemoveTrack(state.SlideTrackId);
                _trackBindings.Remove(state.SlideTrackId);
                state.SlideTrackId = 0;
            }

            if (state.ShiftTrackId != 0)
            {
                _timeline.RemoveTrack(state.ShiftTrackId);
                _trackBindings.Remove(state.ShiftTrackId);
                state.ShiftTrackId = 0;
            }
        }

        _streamingAnimations.Clear();

        _series.Clear();
        _trackBindings.Clear();
        _overrideBuffer.Clear();
    }

    private static ushort GenerateChannel(uint seriesId)
    {
        return unchecked((ushort)(seriesId & 0xFFFF));
    }

    private sealed class SeriesAnimation
    {
        public float BaseStrokeWidth { get; set; }
        public float CurrentStrokeWidth { get; set; }
        public uint StrokeTrackId { get; set; }
        public bool ClearOnComplete { get; set; }
    }

    private sealed class CursorAnimation
    {
        public double CurrentTimeSeconds;
        public double CurrentValue;
        public float Opacity;
        public bool Visible;
        public uint TimeTrackId;
        public uint ValueTrackId;
        public uint OpacityTrackId;
    }

    private sealed class AnnotationAnimation
    {
        public float Emphasis;
        public uint TrackId;
    }

    private sealed class StreamingAnimationState
    {
        public float Fade;
        public float Slide;
        public float ShiftSeconds;
        public uint FadeTrackId;
        public uint SlideTrackId;
        public uint ShiftTrackId;

        public bool IsDormant =>
            FadeTrackId == 0 &&
            SlideTrackId == 0 &&
            ShiftTrackId == 0 &&
            MathF.Abs(Fade) <= 0.001f &&
            MathF.Abs(Slide) <= 0.001f &&
            MathF.Abs(ShiftSeconds) <= 0.0005f;
    }

    private enum AnimationTrackTarget
    {
        SeriesStrokeWidth,
        CursorTime,
        CursorValue,
        CursorOpacity,
        AnnotationEmphasis,
        StreamingFade,
        StreamingSlide,
        StreamingShift,
    }

    private enum CursorTrackProperty
    {
        Time,
        Value,
        Opacity,
    }

    private enum StreamingTrackProperty
    {
        Fade,
        Slide,
        Shift,
    }

    private readonly record struct TrackBinding(AnimationTrackTarget Target, uint EntityId, string? AnnotationId);
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
