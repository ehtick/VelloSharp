# VelloSharp.ChartEngine

`VelloSharp.ChartEngine` orchestrates animated chart rendering by combining data feeds, layouts, and GPU-accelerated drawing commands.

## Getting Started

1. Install with `dotnet add package VelloSharp.ChartEngine`.
2. Add `using VelloSharp.ChartEngine;` to projects where you compose chart visuals.
3. Create an engine instance using the builders provided by the package, wire in your `VelloSharp.ChartData` sources, and configure axis/series metadata.
4. Connect the engine output to a platform-specific presentation layer such as Avalonia, WPF, or WinForms using the corresponding charting package.

## Usage Example

```csharp
using VelloSharp.ChartEngine;

var profile = ChartAnimationProfile.Default with { ReducedMotionEnabled = true };
var color = ChartColor.FromRgb(34, 139, 230);
```

## Next Steps

- Browse the API reference for engine configuration options, animation settings, and rendering hooks.
- Explore the samples to learn how to drive the engine from reactive data feeds or background workers.

