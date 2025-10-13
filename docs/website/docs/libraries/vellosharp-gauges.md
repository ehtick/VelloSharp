# VelloSharp.Gauges

`VelloSharp.Gauges` delivers reusable gauge, dial, and indicator components tailored for monitoring scenarios and industrial dashboards.

## Getting Started

1. Install with `dotnet add package VelloSharp.Gauges`.
2. Bring the namespace into scope via `using VelloSharp.Gauges;`.
3. Instantiate the gauge widgets or view models exposed by the package, bind them to live data streams, and host them inside your preferred UI framework.
4. Coordinate rendering through `VelloSharp.Charting` or `VelloSharp.Composition` to display gauges alongside charts and other visuals.

## Usage Example

```csharp
using VelloSharp.Gauges;

GaugeModule.EnsureInitialized();
```

## Next Steps

- Explore the API reference for available gauge types, styling hooks, and animation helpers.
- Combine with `VelloSharp.Scada` when you need end-to-end industrial telemetry solutions.

