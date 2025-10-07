namespace VelloSharp.Charting.AvaloniaSample.Models;

public readonly record struct TradeUpdate(
    string Symbol,
    double Price,
    double Quantity,
    long EventTimeUnixMilliseconds,
    long LatencyMilliseconds);
