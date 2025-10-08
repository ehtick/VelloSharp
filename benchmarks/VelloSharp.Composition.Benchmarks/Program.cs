using System.Diagnostics;
using System.Text.Json;
using VelloSharp.Composition;

const int defaultTrackCount = 10_000;
const int defaultTicks = 480;

var (trackCount, ticks) = ParseArguments(Environment.GetCommandLineArgs()[1..]);

var result = RunTimelineBenchmark(trackCount, ticks);

var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
{
    WriteIndented = true,
});

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

    // Warm-up tick to populate native caches before measuring.
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

static (int TrackCount, int Ticks) ParseArguments(string[] args)
{
    int trackCount = defaultTrackCount;
    int ticks = defaultTicks;

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
    }

    return (trackCount, ticks);
}

internal sealed record AnimationBenchmarkResult(
    string Scenario,
    int TrackCount,
    int Ticks,
    double TotalMs,
    double AvgTickMs,
    double MaxTickMs);
