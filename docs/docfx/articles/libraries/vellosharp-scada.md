# VelloSharp.Scada

`VelloSharp.Scada` extends the visualization stack with SCADA-oriented components, bindings, and runtime helpers.

## Getting Started

1. Install using `dotnet add package VelloSharp.Scada`.
2. Import `using VelloSharp.Scada;` within services that coordinate telemetry and operator interfaces.
3. Configure the SCADA data adapters and visual widgets provided by the package, linking them to your control systems or simulators.
4. Present the resulting dashboards through `VelloSharp.Charting`, `VelloSharp.Gauges`, or direct composition scenes.

## Usage Example

```csharp
using VelloSharp.Scada;

ScadaRuntime.EnsureInitialized();
```

## Next Steps

- Consult the API reference for device abstractions, alarm handling, and UI widget details.
- Evaluate security and redundancy considerations early when connecting to live control networks.

