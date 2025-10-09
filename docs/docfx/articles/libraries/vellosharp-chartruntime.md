# VelloSharp.ChartRuntime

`VelloSharp.ChartRuntime` coordinates scheduling, background processing, and state management for high-frequency chart updates.

## Getting Started

1. Install with `dotnet add package VelloSharp.ChartRuntime`.
2. Import `using VelloSharp.ChartRuntime;` in the assemblies that manage live data feeds or animation loops.
3. Configure a runtime instance using the builders in the package, wiring in timers, data queues, or task schedulers to push updates into the chart engine.
4. Combine the runtime with `VelloSharp.ChartEngine` and the platform-specific charting controls to deliver smooth, real-time experiences.

## Usage Example

```csharp
using System;
using VelloSharp.ChartRuntime;

var scheduler = new RenderScheduler(TimeSpan.FromMilliseconds(16), TimeProvider.System);
```

## Next Steps

- Refer to the API reference for lifecycle hooks, scheduler options, and extensibility points.
- Stress test your runtime configuration with synthetic data to ensure throughput targets are met before going live.

