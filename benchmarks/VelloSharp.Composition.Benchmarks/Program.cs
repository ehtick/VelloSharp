using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using VelloSharp.Composition;
using VelloSharp.Composition.Controls;

const int defaultTrackCount = 10_000;
const int defaultTicks = 480;

var options = ParseArguments(Environment.GetCommandLineArgs()[1..]);

var scenarios = options.Scenarios.Count == 0
    ? new[] { BenchmarkScenarios.Timeline }
    : options.Scenarios
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(BenchmarkScenarios.Normalize)
        .ToArray();

var results = new List<object>(scenarios.Length);

foreach (var scenario in scenarios)
{
    switch (scenario)
    {
        case BenchmarkScenarios.Timeline:
            results.Add(RunTimelineBenchmark(options.TrackCount, options.Ticks));
            break;
        case BenchmarkScenarios.StackLayout:
            results.Add(RunStackLayoutBenchmark(options.TrackCount, options.Ticks));
            break;
        case BenchmarkScenarios.TemplatedLifecycle:
            results.Add(RunTemplatedLifecycleBenchmark(options.TrackCount, options.Ticks));
            break;
        default:
            throw new ArgumentException($"Unknown benchmark scenario '{scenario}'.");
    }
}

var payload = results.Count == 1 ? results[0] : results;
var json = JsonSerializer.Serialize(
    payload,
    new JsonSerializerOptions { WriteIndented = true });

Console.WriteLine(json);

static AnimationBenchmarkResult RunTimelineBenchmark(int trackCount, int ticks)
{
    using var timeline = new TimelineSystem();
    using var cache = new SceneCache();

    uint groupId = timeline.CreateGroup(new TimelineGroupConfig());
    timeline.PlayGroup(groupId);

    ushort channel = 0;
    for (int i = 0; i < trackCount; i++)
    {
        uint nodeId = cache.CreateNode();
        var descriptor = new TimelineEasingTrackDescriptor(
            nodeId,
            channel,
            startValue: 0f,
            endValue: 1f,
            duration: 0.45f,
            easing: TimelineEasing.EaseInOutQuad,
            repeat: TimelineRepeat.PingPong,
            dirtyBinding: TimelineDirtyBinding.None);

        uint trackId = timeline.AddEasingTrack(groupId, descriptor);
        if (trackId == uint.MaxValue)
        {
            throw new InvalidOperationException("Failed to allocate easing track for benchmark run.");
        }

        channel++;
    }

    var delta = TimeSpan.FromSeconds(1.0 / 120.0);
    var samples = new TimelineSample[trackCount];

    _ = timeline.Tick(delta, cache, samples);

    var totalStopwatch = Stopwatch.StartNew();
    var tickAccumulator = TimeSpan.Zero;
    var maxTick = TimeSpan.Zero;

    for (int i = 0; i < ticks; i++)
    {
        var tickStopwatch = Stopwatch.StartNew();
        _ = timeline.Tick(delta, cache, samples);
        tickStopwatch.Stop();

        tickAccumulator += tickStopwatch.Elapsed;
        if (tickStopwatch.Elapsed > maxTick)
        {
            maxTick = tickStopwatch.Elapsed;
        }
    }

    totalStopwatch.Stop();

    return new AnimationBenchmarkResult(
        "timeline_10k_tracks",
        trackCount,
        ticks,
        totalStopwatch.Elapsed.TotalMilliseconds,
        tickAccumulator.TotalMilliseconds / ticks,
        maxTick.TotalMilliseconds);
}

static LayoutBenchmarkResult RunStackLayoutBenchmark(int childCount, int iterations)
{
    var random = new Random(1729);
    var children = new StackLayoutChild[childCount];

    for (int i = 0; i < childCount; i++)
    {
        double preferredHeight = 16 + random.NextDouble() * 32;
        double preferredWidth = 80 + random.NextDouble() * 160;
        var constraints = new LayoutConstraints(
            new ScalarConstraint(preferredWidth, preferredWidth, preferredWidth),
            new ScalarConstraint(preferredHeight, preferredHeight, preferredHeight));
        children[i] = new StackLayoutChild(constraints);
    }

    var options = new StackLayoutOptions(
        LayoutOrientation.Vertical,
        spacing: 2,
        padding: new LayoutThickness(6, 6, 6, 6),
        crossAlignment: LayoutAlignment.Stretch);

    var available = new LayoutSize(1200, double.PositiveInfinity);
    var rectBuffer = new LayoutRect[childCount];

    CompositionInterop.SolveStackLayout(children, options, available, rectBuffer);

    double totalMs = 0;
    double maxMs = 0;

    for (int i = 0; i < iterations; i++)
    {
        var stopwatch = Stopwatch.StartNew();
        CompositionInterop.SolveStackLayout(children, options, available, rectBuffer);
        stopwatch.Stop();

        var elapsed = stopwatch.Elapsed.TotalMilliseconds;
        totalMs += elapsed;
        if (elapsed > maxMs)
        {
            maxMs = elapsed;
        }
    }

    return new LayoutBenchmarkResult(
        BenchmarkScenarios.StackLayout,
        childCount,
        iterations,
        totalMs,
        totalMs / iterations,
        maxMs);
}

static TemplateBenchmarkResult RunTemplatedLifecycleBenchmark(int rowCount, int iterations)
{
    var control = new TemplatedControl
    {
        Template = CompositionTemplate.Create(_ => new BenchmarkElement()),
    };

    control.Mount();
    control.ApplyTemplate();

    var rows = new VirtualRowMetric[rowCount];
    for (uint i = 0; i < rowCount; i++)
    {
        var height = 16 + (i % 5);
        rows[i] = new VirtualRowMetric(i, height);
    }

    var columns = new[]
    {
        new VirtualColumnStrip(0, 160, FrozenKind.Leading, 1),
        new VirtualColumnStrip(160, 220, FrozenKind.None, 2),
        new VirtualColumnStrip(380, 200, FrozenKind.Trailing, 3),
    };

    control.UpdateVirtualization(rows, columns);

    var rowViewport = new RowViewportMetrics(0, 800, 160);
    var columnViewport = new ColumnViewportMetrics(0, 640, 120);

    using (var warmup = control.CaptureVirtualizationPlan(rowViewport, columnViewport))
    {
        ConsumePlan(warmup);
    }

    double totalMs = 0;
    double maxMs = 0;
    var constraints = new LayoutConstraints(
        new ScalarConstraint(0, 640, 640),
        new ScalarConstraint(0, 800, 800));
    var arrangeRect = new LayoutRect(0, 0, 640, 800, 0, 800);

    for (int i = 0; i < iterations; i++)
    {
        var stopwatch = Stopwatch.StartNew();

        control.UpdateVirtualization(rows, columns);
        using (var plan = control.CaptureVirtualizationPlan(rowViewport, columnViewport))
        {
            ConsumePlan(plan);
        }

        control.Measure(constraints);
        control.Arrange(arrangeRect);

        stopwatch.Stop();

        var elapsed = stopwatch.Elapsed.TotalMilliseconds;
        totalMs += elapsed;
        if (elapsed > maxMs)
        {
            maxMs = elapsed;
        }
    }

    control.Unmount();

    return new TemplateBenchmarkResult(
        BenchmarkScenarios.TemplatedLifecycle,
        rowCount,
        iterations,
        totalMs,
        totalMs / iterations,
        maxMs);
}

static void ConsumePlan(VisualTreeVirtualizer.VirtualizationPlan plan)
{
    foreach (var entry in plan.Active)
    {
        _ = entry.BufferId;
        _ = entry.NodeId;
    }

    foreach (var entry in plan.Recycled)
    {
        _ = entry.BufferId;
        _ = entry.NodeId;
    }
}

static BenchmarkOptions ParseArguments(string[] args)
{
    int trackCount = defaultTrackCount;
    int ticks = defaultTicks;
    var scenarios = new List<string>();

    foreach (string argument in args)
    {
        if (argument.StartsWith("--track-count=", StringComparison.Ordinal))
        {
            var value = argument["--track-count=".Length..];
            if (int.TryParse(value, out var parsed) && parsed > 0)
            {
                trackCount = parsed;
            }
        }
        else if (argument.StartsWith("--ticks=", StringComparison.Ordinal))
        {
            var value = argument["--ticks=".Length..];
            if (int.TryParse(value, out var parsed) && parsed > 0)
            {
                ticks = parsed;
            }
        }
        else if (argument.StartsWith("--scenario=", StringComparison.OrdinalIgnoreCase))
        {
            var value = argument["--scenario=".Length..].Trim();
            if (string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
            {
                scenarios.Clear();
                scenarios.AddRange(BenchmarkScenarios.All);
            }
            else if (!string.IsNullOrEmpty(value))
            {
                scenarios.Add(value);
            }
        }
    }

    return new BenchmarkOptions(trackCount, ticks, scenarios);
}

internal sealed record AnimationBenchmarkResult(
    string Scenario,
    int TrackCount,
    int Ticks,
    double TotalMs,
    double AvgTickMs,
    double MaxTickMs);

internal sealed record LayoutBenchmarkResult(
    string Scenario,
    int ChildCount,
    int Iterations,
    double TotalMs,
    double AvgIterationMs,
    double MaxIterationMs);

internal sealed record TemplateBenchmarkResult(
    string Scenario,
    int RowCount,
    int Iterations,
    double TotalMs,
    double AvgIterationMs,
    double MaxIterationMs);

internal sealed record BenchmarkOptions(int TrackCount, int Ticks, List<string> Scenarios);

internal static class BenchmarkScenarios
{
    public const string Timeline = "timeline";
    public const string StackLayout = "stack-layout";
    public const string TemplatedLifecycle = "templated-lifecycle";

    public static readonly string[] All =
    {
        Timeline,
        StackLayout,
        TemplatedLifecycle,
    };

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Scenario value cannot be empty.", nameof(value));
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "timeline" => Timeline,
            "stack-layout" => StackLayout,
            "stack_layout" => StackLayout,
            "templated-lifecycle" => TemplatedLifecycle,
            "templated_lifecycle" => TemplatedLifecycle,
            _ => throw new ArgumentException($"Unknown benchmark scenario '{value}'.", nameof(value)),
        };
    }
}

private sealed class BenchmarkElement : CompositionElement
{
    public override void Measure(in LayoutConstraints constraints)
    {
        base.Measure(constraints);
        DesiredSize = new LayoutSize(
            Math.Clamp(constraints.Width.Preferred, constraints.Width.Min, constraints.Width.Max),
            Math.Clamp(constraints.Height.Preferred, constraints.Height.Min, constraints.Height.Max));
    }
}
