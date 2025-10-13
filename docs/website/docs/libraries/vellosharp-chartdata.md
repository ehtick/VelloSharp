# VelloSharp.ChartData

`VelloSharp.ChartData` defines the data contracts, models, and adapters that feed the real-time charting engine.

## Getting Started

1. Install using `dotnet add package VelloSharp.ChartData`.
2. Import the namespace with `using VelloSharp.ChartData;` wherever you prepare chart inputs.
3. Instantiate the data series, buffer providers, or adapters exposed by the package, then populate them from your telemetry or analytics sources.
4. Hand the prepared data to `VelloSharp.ChartEngine` or a platform-specific charting package to visualize the results.

## Usage Example

```csharp
using VelloSharp.ChartData;

var bus = new ChartDataBus(capacity: 4);
bus.Write(new[] { 1.0f, 2.5f, 3.75f });
if (bus.TryRead(out var slice))
{
    Console.WriteLine($"Slice items: {slice.ItemCount}");
    slice.Dispose();
}
```

## Next Steps

- Consult the API reference for the list of series types, scaling helpers, and sampling utilities.
- Look at the charting samples to see how data sources are updated on a timer or in response to streaming events.

