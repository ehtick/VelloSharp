using System;
using System.Collections.Generic;
using VelloSharp.ChartData;

namespace VelloSharp.Charting.AvaloniaSample.Infrastructure;

/// <summary>
/// Aggregates streaming magnitudes into fixed time buckets and emits rolling samples.
/// </summary>
internal sealed class TimeBucketAggregator
{
    private readonly int _seriesId;
    private readonly double _bucketSeconds;
    private readonly double _windowSeconds;
    private readonly Dictionary<long, double> _bucketTotals = new();
    private readonly Queue<long> _bucketOrder = new();

    public TimeBucketAggregator(int seriesId, double bucketSeconds, double windowSeconds)
    {
        if (bucketSeconds <= 0 || !double.IsFinite(bucketSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(bucketSeconds), bucketSeconds, "Bucket duration must be positive.");
        }

        if (windowSeconds <= 0 || !double.IsFinite(windowSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(windowSeconds), windowSeconds, "Window duration must be positive.");
        }

        _seriesId = seriesId;
        _bucketSeconds = bucketSeconds;
        _windowSeconds = windowSeconds;
    }

    public int Accumulate(double timestampSeconds, double magnitude, Span<ChartSamplePoint> output)
    {
        if (output.IsEmpty || magnitude <= 0 || !double.IsFinite(timestampSeconds) || !double.IsFinite(magnitude))
        {
            return 0;
        }

        var bucketIndex = (long)Math.Floor(timestampSeconds / _bucketSeconds);

        if (_bucketTotals.TryAdd(bucketIndex, 0d))
        {
            _bucketOrder.Enqueue(bucketIndex);
        }

        _bucketTotals[bucketIndex] += magnitude;

        Prune(timestampSeconds);

        var bucketStart = bucketIndex * _bucketSeconds;
        var sampleTimestamp = bucketStart + (_bucketSeconds * 0.5);
        output[0] = new ChartSamplePoint(_seriesId, sampleTimestamp, _bucketTotals[bucketIndex]);

        return 1;
    }

    public void Reset()
    {
        _bucketTotals.Clear();
        _bucketOrder.Clear();
    }

    private void Prune(double currentTimestamp)
    {
        var cutoff = currentTimestamp - _windowSeconds;
        while (_bucketOrder.Count > 0)
        {
            var index = _bucketOrder.Peek();
            var bucketStart = index * _bucketSeconds;
            if (bucketStart >= cutoff)
            {
                break;
            }

            _bucketOrder.Dequeue();
            _bucketTotals.Remove(index);
        }
    }
}
