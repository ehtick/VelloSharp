using System;
using System.Collections.Generic;
using VelloSharp.Charting.Primitives;
using VelloSharp.Charting.Scales;

namespace VelloSharp.Charting.Ticks;

/// <summary>
/// Generates ticks for time scales based on human-friendly intervals.
/// </summary>
public sealed class TimeTickGenerator : IAxisTickGenerator<DateTimeOffset>
{
    private static readonly TimeSpan[] CandidateIntervals =
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(3),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(12),
        TimeSpan.FromDays(1),
        TimeSpan.FromDays(2),
        TimeSpan.FromDays(7),
        TimeSpan.FromDays(14),
        TimeSpan.FromDays(30),
        TimeSpan.FromDays(90),
        TimeSpan.FromDays(180),
        TimeSpan.FromDays(365),
    };

    public IReadOnlyList<AxisTick<DateTimeOffset>> Generate(
        IScale<DateTimeOffset> scale,
        TickGenerationOptions<DateTimeOffset>? options = null)
    {
        ArgumentNullException.ThrowIfNull(scale);

        options ??= new TickGenerationOptions<DateTimeOffset>();
        var domain = scale.Domain.Normalize();
        var start = domain.Start;
        var end = domain.End;

        if (start == end)
        {
            var unit = scale.Project(start);
            return new[]
            {
                new AxisTick<DateTimeOffset>(start, unit, Format(options, start))
            };
        }

        var total = end - start;
        var interval = SelectInterval(total, options.TargetTickCount);
        var firstTick = AlignToInterval(start, interval, alignForward: true);

        var ticks = new List<AxisTick<DateTimeOffset>>();
        for (var current = firstTick; current <= end; current += interval)
        {
            if (current < start)
            {
                continue;
            }

            var unit = scale.Project(current);
            ticks.Add(new AxisTick<DateTimeOffset>(current, unit, Format(options, current)));
        }

        return ticks;
    }

    private static string Format(TickGenerationOptions<DateTimeOffset> options, DateTimeOffset value) =>
        options.LabelFormatter?.Invoke(value) ?? value.ToString("u");

    private static TimeSpan SelectInterval(TimeSpan span, int targetTicks)
    {
        var totalSeconds = Math.Abs(span.TotalSeconds);
        if (totalSeconds <= 0d)
        {
            return TimeSpan.FromSeconds(1);
        }

        foreach (var candidate in CandidateIntervals)
        {
            var tickCount = span.TotalSeconds / candidate.TotalSeconds;
            if (tickCount <= targetTicks * 1.5d)
            {
                return candidate;
            }
        }

        // Fallback to yearly intervals with multiples.
        var years = Math.Max(1, Math.Round(span.TotalDays / 365d / targetTicks));
        return TimeSpan.FromDays(365 * years);
    }

    private static DateTimeOffset AlignToInterval(DateTimeOffset value, TimeSpan interval, bool alignForward)
    {
        if (interval <= TimeSpan.Zero)
        {
            return value;
        }

        var ticks = interval.Ticks;
        var offset = value.Ticks % ticks;
        if (offset == 0)
        {
            return value;
        }

        return alignForward
            ? new DateTimeOffset(value.Ticks + (ticks - offset), value.Offset)
            : new DateTimeOffset(value.Ticks - offset, value.Offset);
    }
}
