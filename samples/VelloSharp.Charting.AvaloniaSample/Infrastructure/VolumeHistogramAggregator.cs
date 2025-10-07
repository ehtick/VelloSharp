using System;
using VelloSharp.ChartData;

namespace VelloSharp.Charting.AvaloniaSample.Infrastructure;

internal sealed class VolumeHistogramAggregator
{
    private readonly TimeBucketAggregator _aggregator;

    public VolumeHistogramAggregator(int seriesId, double bucketSeconds, double windowSeconds)
    {
        _aggregator = new TimeBucketAggregator(seriesId, bucketSeconds, windowSeconds);
    }

    public int Accumulate(double timestampSeconds, double quantity, Span<ChartSamplePoint> output)
    {
        if (quantity <= 0 || output.IsEmpty)
        {
            return 0;
        }

        return _aggregator.Accumulate(timestampSeconds, quantity, output);
    }

    public void Reset() => _aggregator.Reset();
}
