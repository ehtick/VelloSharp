namespace VelloSharp.Charting.AvaloniaSample.Models;

public readonly record struct TradeUpdate(
    string Symbol,
    double Price,
    long EventTimeUnixMilliseconds,
    long LatencyMilliseconds);
