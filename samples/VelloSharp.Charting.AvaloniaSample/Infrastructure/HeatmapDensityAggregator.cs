using System;
using VelloSharp.ChartData;

namespace VelloSharp.Charting.AvaloniaSample.Infrastructure;

internal sealed class HeatmapDensityAggregator
{
    private readonly TimeBucketAggregator[] _bucketAggregators;
    private readonly double[] _thresholds;

    public HeatmapDensityAggregator(int seriesBaseId, int bucketCount, double bucketSeconds, double windowSeconds)
    {
        if (bucketCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bucketCount));
        }

        _bucketAggregators = new TimeBucketAggregator[bucketCount];
        for (var i = 0; i < bucketCount; i++)
        {
            _bucketAggregators[i] = new TimeBucketAggregator(seriesBaseId + i, bucketSeconds, windowSeconds);
        }

        // Buckets map to large drop, mild drop, neutral, mild rise, strong rise by default.
        _thresholds = bucketCount switch
        {
            1 => Array.Empty<double>(),
            2 => new[] { 0d },
            3 => new[] { -0.15, 0.15 },
            4 => new[] { -0.25, 0.0, 0.25 },
            _ => new[] { -0.35, -0.15, 0.15, 0.35 }
        };
    }

    public int Accumulate(double timestampSeconds, double quantity, double deltaPercent, Span<ChartSamplePoint> output)
    {
        if (quantity <= 0 || output.IsEmpty || !double.IsFinite(deltaPercent))
        {
            return 0;
        }

        var bucketIndex = MapBucket(deltaPercent);
        return _bucketAggregators[bucketIndex].Accumulate(timestampSeconds, quantity, output);
    }

    public void Reset()
    {
        foreach (var aggregator in _bucketAggregators)
        {
            aggregator.Reset();
        }
    }

    private int MapBucket(double deltaPercent)
    {
        for (var i = 0; i < _thresholds.Length; i++)
        {
            if (deltaPercent < _thresholds[i])
            {
                return i;
            }
        }

        return _bucketAggregators.Length - 1;
    }
}
