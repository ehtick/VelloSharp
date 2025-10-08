using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using VelloSharp;
using VelloSharp.ChartData;
using VelloSharp.ChartDiagnostics;
using VelloSharp.ChartRuntime;

namespace VelloSharp.ChartEngine;

/// <summary>
/// High-level wrapper over the native chart engine exposed via FFI.
/// </summary>
public sealed class ChartEngine : IDisposable
{
    private const int StackThreshold = 256;
    private const int PaletteStackThreshold = 32;
    private const int SeriesOverrideStackThreshold = 16;
    private const int SeriesDefinitionStackThreshold = 16;
    private const double DefaultStrokeWidth = 1.5;

    private readonly ChartEngineOptions _options;
    private readonly RenderScheduler _scheduler;
    private readonly FrameDiagnosticsCollector _diagnostics;
    private readonly ChartAnimationController _animations;
    private IChartTelemetrySink? _telemetrySink;
    private nint _handle;
    private int _disposed;
    private FrameStats _lastStats;

    static ChartEngine()
    {
        ChartEngineTraceBridge.Initialize();
    }

    public ChartEngine(ChartEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _telemetrySink = options.TelemetrySink;
        _scheduler = new RenderScheduler(options.FrameBudget, options.TimeProvider ?? TimeProvider.System);
        _scheduler.SetAutomaticTicksEnabled(options.AutomaticTicksEnabled);
        if (options.TickSource is not null)
        {
            _scheduler.SetTickSource(options.TickSource, options.OwnsTickSource);
        }
        _diagnostics = new FrameDiagnosticsCollector(_telemetrySink);
        var animationProfile = options.Animations ?? ChartAnimationProfile.Default;
        _animations = new ChartAnimationController(
            this,
            _scheduler,
            NormalizeStrokeWidth(options.StrokeWidth),
            animationProfile);

        var nativeOptions = new VelloChartEngineOptions
        {
            VisibleDurationSeconds = Math.Max(options.VisibleDuration.TotalSeconds, 0.001),
            VerticalPaddingRatio = options.VerticalPaddingRatio,
            StrokeWidth = NormalizeStrokeWidth(options.StrokeWidth),
            ShowAxes = options.ShowAxes ? 1u : 0u,
        };

        _handle = CreateEngineHandle(options, nativeOptions);
        if (_handle == 0)
        {
            ThrowNativeFailure("Failed to create chart engine");
        }
    }

    public ChartEngineOptions Options => _options;

    public FrameDiagnosticsCollector Diagnostics => _diagnostics;

    public FrameStats LastFrameStats => _lastStats;

    public void ScheduleRender(FrameTickCallback callback)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(callback);
        _scheduler.Schedule(callback);
    }

    public void ConfigureTickSource(IFrameTickSource? tickSource, bool ownsTickSource = false)
    {
        ThrowIfDisposed();
        _scheduler.SetTickSource(tickSource, ownsTickSource);
    }

    public void SetAutomaticScheduling(bool enabled)
    {
        ThrowIfDisposed();
        _scheduler.SetAutomaticTicksEnabled(enabled);
    }

    public bool TryAdvanceFrame(TimeSpan? timestampOverride = null)
    {
        ThrowIfDisposed();
        return _scheduler.TryRunManualTick(timestampOverride);
    }

    public void SetTelemetrySink(IChartTelemetrySink? telemetrySink)
    {
        ThrowIfDisposed();
        _telemetrySink = telemetrySink;
        _diagnostics.SetTelemetrySink(telemetrySink);
    }

    public void RecordMetric(ChartMetric metric)
    {
        ThrowIfDisposed();
        _diagnostics.RecordMetric(metric);
    }

    public void PumpData(ReadOnlySpan<ChartSamplePoint> samples)
    {
        ThrowIfDisposed();

        if (samples.IsEmpty)
        {
            return;
        }

        if (samples.Length <= StackThreshold)
        {
            Span<VelloChartSamplePoint> span = stackalloc VelloChartSamplePoint[samples.Length];
            PublishNativeSamples(span, samples);
        }
        else
        {
            var rented = ArrayPool<VelloChartSamplePoint>.Shared.Rent(samples.Length);
            try
            {
                PublishNativeSamples(rented.AsSpan(0, samples.Length), samples);
            }
            finally
            {
                ArrayPool<VelloChartSamplePoint>.Shared.Return(rented, clearArray: true);
            }
        }
    }

    public void UpdatePalette(ReadOnlySpan<ChartColor> palette)
    {
        ThrowIfDisposed();

        if (palette.IsEmpty)
        {
            unsafe
            {
                var status = NativeMethods.vello_chart_engine_set_palette(_handle, null, 0);
                ThrowOnStatus(status, "vello_chart_engine_set_palette");
            }

            return;
        }

        Span<VelloChartColor> colors = palette.Length <= PaletteStackThreshold
            ? stackalloc VelloChartColor[palette.Length]
            : new VelloChartColor[palette.Length];

        for (var i = 0; i < palette.Length; i++)
        {
            colors[i] = palette[i].ToNative();
        }

        unsafe
        {
            fixed (VelloChartColor* ptr = colors)
            {
                var status = NativeMethods.vello_chart_engine_set_palette(_handle, ptr, (nuint)palette.Length);
                ThrowOnStatus(status, "vello_chart_engine_set_palette");
            }
        }
    }

    public unsafe void ConfigureSeries(ReadOnlySpan<ChartSeriesDefinition> definitions)
    {
        ThrowIfDisposed();

        _animations.RegisterDefinitions(definitions);

        if (definitions.IsEmpty)
        {
            var status = NativeMethods.vello_chart_engine_set_series_definitions(_handle, null, 0);
            ThrowOnStatus(status, "vello_chart_engine_set_series_definitions");
            return;
        }

        var count = definitions.Length;
        Span<VelloChartSeriesDefinition> nativeDefinitions = count <= SeriesDefinitionStackThreshold
            ? stackalloc VelloChartSeriesDefinition[count]
            : new VelloChartSeriesDefinition[count];

        for (var i = 0; i < count; i++)
        {
            var definition = definitions[i] ?? throw new ArgumentNullException(
                nameof(definitions),
                "Series definitions cannot contain null entries.");
            nativeDefinitions[i] = definition.ToNative();
        }

        unsafe
        {
            fixed (VelloChartSeriesDefinition* ptr = nativeDefinitions)
            {
                var status = NativeMethods.vello_chart_engine_set_series_definitions(
                    _handle,
                    ptr,
                    (nuint)count);
                ThrowOnStatus(status, "vello_chart_engine_set_series_definitions");
            }
        }
    }

    public void AnimateSeriesStrokeWidth(uint seriesId, double targetStrokeWidth, TimeSpan duration)
    {
        ThrowIfDisposed();
        _animations.AnimateStrokeWidth(seriesId, targetStrokeWidth, duration);
    }

    public void ResetSeriesStrokeWidth(uint seriesId, TimeSpan duration)
    {
        ThrowIfDisposed();
        _animations.ResetStrokeWidth(seriesId, duration);
    }

    public void AnimateCursor(ChartCursorUpdate update)
    {
        ThrowIfDisposed();
        _animations.AnimateCursor(update);
    }

    public void AnimateAnnotation(string annotationId, bool highlighted, TimeSpan? duration = null)
    {
        ThrowIfDisposed();
        _animations.AnimateAnnotation(annotationId, highlighted, duration);
    }

    public void AnimateStreaming(ReadOnlySpan<ChartStreamingUpdate> updates)
    {
        ThrowIfDisposed();
        _animations.AnimateStreaming(updates);
    }

    public unsafe void ConfigureComposition(ChartComposition? composition)
    {
        ThrowIfDisposed();

        if (composition is null || composition.Panes.Count == 0)
        {
            var status = NativeMethods.vello_chart_engine_set_composition(_handle, null);
            ThrowOnStatus(status, "vello_chart_engine_set_composition");
            return;
        }

        var panes = composition.Panes;
        var engineProfile = _options.Animations ?? ChartAnimationProfile.Default;
        var profile = composition.HasCustomAnimations ? composition.Animations : engineProfile;
        _animations.UpdateProfile(profile);
        var nativePanes = new VelloChartCompositionPane[panes.Count];
        var pinnedHandles = new List<GCHandle>(panes.Count * 2);

        try
        {
            for (var i = 0; i < panes.Count; i++)
            {
                var pane = panes[i];
                var idBytes = Encoding.UTF8.GetBytes(pane.Id);
                var idHandle = GCHandle.Alloc(idBytes, GCHandleType.Pinned);
                pinnedHandles.Add(idHandle);

                var seriesIds = pane.SeriesIds.Count > 0
                    ? pane.SeriesIds.ToArray()
                    : Array.Empty<uint>();

                GCHandle? seriesHandle = null;
                if (seriesIds.Length > 0)
                {
                    seriesHandle = GCHandle.Alloc(seriesIds, GCHandleType.Pinned);
                    pinnedHandles.Add(seriesHandle.Value);
                }

                nativePanes[i] = new VelloChartCompositionPane
                {
                    Id = idHandle.AddrOfPinnedObject(),
                    IdLength = (nuint)idBytes.Length,
                    HeightRatio = pane.NormalizedRatio,
                    ShareXAxisWithPrimary = pane.ShareXAxisWithPrimary ? 1u : 0u,
                    SeriesIds = seriesHandle?.AddrOfPinnedObject() ?? nint.Zero,
                    SeriesIdCount = (nuint)seriesIds.Length,
                };
            }

            fixed (VelloChartCompositionPane* panePtr = nativePanes)
            {
                var compositionNative = new VelloChartComposition
                {
                    Panes = (nint)panePtr,
                    PaneCount = (nuint)nativePanes.Length,
                };

                var status = NativeMethods.vello_chart_engine_set_composition(_handle, &compositionNative);
                ThrowOnStatus(status, "vello_chart_engine_set_composition");
            }
        }
        finally
        {
            foreach (var handle in pinnedHandles)
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }
        }
    }

    public unsafe void ApplySeriesOverrides(ReadOnlySpan<ChartSeriesOverride> overrides)
    {
        ThrowIfDisposed();

        if (overrides.IsEmpty)
        {
            return;
        }

        var count = overrides.Length;
        Span<VelloChartSeriesOverride> nativeOverrides = count <= SeriesOverrideStackThreshold
            ? stackalloc VelloChartSeriesOverride[count]
            : new VelloChartSeriesOverride[count];

        Span<nint> labelAllocations = count <= SeriesOverrideStackThreshold
            ? stackalloc nint[count]
            : new nint[count];
        labelAllocations.Clear();

        var written = 0;

        try
        {
            for (var i = 0; i < count; i++)
            {
                var item = overrides[i];
                item.Validate();

                if (item.IsEmpty)
                {
                    continue;
                }

                ref var native = ref nativeOverrides[written];
                native.SeriesId = unchecked((uint)item.SeriesId);
                native.Flags = item.Flags;
                native.StrokeWidth = item.StrokeWidth;
                native.Color = default;
                native.Label = 0;
                native.LabelLength = 0;

                if ((item.Flags & SeriesOverrideFlags.ColorSet) != 0)
                {
                    native.Color = item.Color.ToNative();
                }

                if ((item.Flags & SeriesOverrideFlags.LabelSet) != 0)
                {
                    var label = item.Label ?? string.Empty;
                    var bytes = Encoding.UTF8.GetBytes(label);
                    if (bytes.Length > 0)
                    {
                        unsafe
                        {
                            var allocation = NativeMemory.Alloc((nuint)bytes.Length);
                            try
                            {
                                fixed (byte* src = bytes)
                                {
                                    Buffer.MemoryCopy(src, allocation, bytes.Length, bytes.Length);
                                }
                            }
                            catch
                            {
                                NativeMemory.Free(allocation);
                                throw;
                            }

                            native.Label = (nint)allocation;
                            native.LabelLength = (nuint)bytes.Length;
                            labelAllocations[written] = (nint)allocation;
                        }
                    }
                }

                written++;
            }

            if (written == 0)
            {
                return;
            }

            unsafe
            {
                fixed (VelloChartSeriesOverride* ptr = nativeOverrides)
                {
                    var status = NativeMethods.vello_chart_engine_apply_series_overrides(
                        _handle,
                        ptr,
                        (nuint)written);

                    ThrowOnStatus(status, "vello_chart_engine_apply_series_overrides");
                }
            }
        }
        finally
        {
            for (var i = 0; i < labelAllocations.Length; i++)
            {
                var allocation = labelAllocations[i];
                if (allocation != 0)
                {
                    unsafe
                    {
                        NativeMemory.Free((void*)allocation);
                    }
                }
            }
        }
    }

    private void PublishNativeSamples(Span<VelloChartSamplePoint> destination, ReadOnlySpan<ChartSamplePoint> source)
    {
        for (var i = 0; i < source.Length; i++)
        {
            var sample = source[i];
            destination[i] = new VelloChartSamplePoint(
                unchecked((uint)sample.SeriesId),
                sample.TimestampSeconds,
                sample.Value);
        }

        unsafe
        {
            fixed (VelloChartSamplePoint* ptr = destination)
            {
                var status = NativeMethods.vello_chart_engine_publish_samples(
                    _handle,
                    ptr,
                    (nuint)destination.Length);

                ThrowOnStatus(status, "vello_chart_engine_publish_samples");
            }
        }
    }

    public void Render(Scene scene, uint width, uint height)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(scene);

        if (width == 0 || height == 0)
        {
            return;
        }

        var status = NativeMethods.vello_chart_engine_render(
            _handle,
            scene.Handle,
            width,
            height,
            out var stats);

        ThrowOnStatus(status, "vello_chart_engine_render");

        _lastStats = stats.ToManaged();
        _diagnostics.Record(_lastStats);
    }

    public ChartFrameMetadata GetFrameMetadata()
    {
        ThrowIfDisposed();
        var status = NativeMethods.vello_chart_engine_last_frame_metadata(_handle, out var metadata);
        ThrowOnStatus(status, "vello_chart_engine_last_frame_metadata");
        unsafe
        {
            var managed = ChartFrameMetadata.FromNative(metadata);
            managed.SetCursorOverlay(_animations.GetCursorOverlaySnapshot());
            managed.SetAnnotationOverlays(_animations.GetAnnotationSnapshots());
            managed.SetStreamingOverlays(_animations.GetStreamingOverlaySnapshots());
            return managed;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _scheduler.Dispose();
        _animations.Dispose();
        _diagnostics.Dispose();

        if (_handle != 0)
        {
            NativeMethods.vello_chart_engine_destroy(_handle);
            _handle = 0;
        }

        GC.SuppressFinalize(this);
    }

    private static double NormalizeStrokeWidth(double strokeWidth)
    {
        if (double.IsFinite(strokeWidth) && strokeWidth > 0)
        {
            return strokeWidth;
        }

        return DefaultStrokeWidth;
    }

    private static nint CreateEngineHandle(ChartEngineOptions options, VelloChartEngineOptions nativeOptions)
    {
        return options.Palette is { Count: > 0 } palette
            ? CreateWithPalette(nativeOptions, palette)
            : NativeMethods.vello_chart_engine_create(nativeOptions);
    }

    private static nint CreateWithPalette(VelloChartEngineOptions nativeOptions, IReadOnlyList<ChartColor> palette)
    {
        var count = palette.Count;
        Span<VelloChartColor> colors = count <= PaletteStackThreshold
            ? stackalloc VelloChartColor[count]
            : new VelloChartColor[count];

        for (var i = 0; i < count; i++)
        {
            colors[i] = palette[i].ToNative();
        }

        unsafe
        {
            fixed (VelloChartColor* ptr = colors)
            {
                var optionsWithPalette = nativeOptions;
                optionsWithPalette.PaletteLength = (nuint)count;
                optionsWithPalette.Palette = (nint)ptr;
                return NativeMethods.vello_chart_engine_create(optionsWithPalette);
            }
        }
    }

    private static void ThrowOnStatus(VelloChartEngineStatus status, string operation)
    {
        if (status == VelloChartEngineStatus.Success)
        {
            return;
        }

        var message = NativeHelpers.GetUtf8String(NativeMethods.vello_chart_engine_last_error_message());
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new InvalidOperationException($"{operation} failed with status {status}");
        }

        throw new InvalidOperationException($"{operation} failed: {message} (status: {status})");
    }

    private static void ThrowNativeFailure(string message)
    {
        var native = NativeHelpers.GetUtf8String(NativeMethods.vello_chart_engine_last_error_message());
        if (string.IsNullOrWhiteSpace(native))
        {
            throw new InvalidOperationException(message);
        }

        throw new InvalidOperationException($"{message}: {native}");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed != 0, this);
    }
}
