using System;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using VelloSharp;

namespace VelloSharp.ChartEngine;

internal static class ChartEngineTraceBridge
{
    private static int _initialized;

    public static void Initialize()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
        {
            return;
        }

        unsafe
        {
            var status = NativeMethods.vello_chart_engine_set_trace_callback(&OnTraceEvent);
            if (status != VelloChartEngineStatus.Success)
            {
                // Tracing is best-effort; failures are ignored to avoid blocking engine startup.
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OnTraceEvent(
        ChartTraceLevel level,
        long timestampMs,
        nint targetPtr,
        nint messagePtr,
        nint propertiesPtr)
    {
        var target = NativeHelpers.GetUtf8String(targetPtr);
        var message = NativeHelpers.GetUtf8String(messagePtr);
        var properties = NativeHelpers.GetUtf8String(propertiesPtr);
        ChartEngineEventSource.Log.Trace(level, timestampMs, target, message, properties);
    }
}

[EventSource(Name = "VelloSharp-ChartEngine")]
internal sealed class ChartEngineEventSource : EventSource
{
    public static ChartEngineEventSource Log { get; } = new();

    private ChartEngineEventSource()
    {
    }

    [NonEvent]
    public void Trace(ChartTraceLevel level, long timestampMs, string? target, string? message, string? properties)
    {
        if (!IsEnabled())
        {
            return;
        }

        TraceCore(
            (int)level,
            timestampMs,
            target ?? string.Empty,
            message ?? string.Empty,
            properties ?? string.Empty);
    }

    [Event(1, Level = EventLevel.Verbose, Message = "[{2}] {3} {4}")]
    private void TraceCore(int level, long timestampMs, string target, string message, string properties)
    {
        WriteEvent(1, level, timestampMs, target, message, properties);
    }
}
