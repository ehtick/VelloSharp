# VelloSharp.ChartDiagnostics

`VelloSharp.ChartDiagnostics` contains instrumentation helpers that measure charting performance, memory usage, and rendering throughput.

## Getting Started

1. Add the package with `dotnet add package VelloSharp.ChartDiagnostics`.
2. Use `using VelloSharp.ChartDiagnostics;` in projects where you need telemetry for chart rendering.
3. Attach the diagnostic observers or loggers to your `VelloSharp.ChartEngine` pipelines to capture metrics during development or production.
4. Feed the collected data into your monitoring solution or display it on overlay widgets via the charting UI components.

## Usage Example

```csharp
using System;
using VelloSharp.ChartDiagnostics;

using var collector = new FrameDiagnosticsCollector();
collector.Record(new FrameStats(TimeSpan.FromMilliseconds(4.2), TimeSpan.FromMilliseconds(3.1), TimeSpan.FromMilliseconds(1.0), 128, DateTimeOffset.UtcNow));
```

## Next Steps

- Study the API reference for available diagnostic sources, sampling strategies, and event payloads.
- Enable diagnostics behind configuration switches so you can toggle them without redeploying your application.

