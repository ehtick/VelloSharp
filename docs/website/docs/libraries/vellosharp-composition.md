# VelloSharp.Composition

`VelloSharp.Composition` introduces a retained-mode scene graph and composition system built on top of the Vello renderer.

## Getting Started

1. Install the package: `dotnet add package VelloSharp.Composition`.
2. Import `using VelloSharp.Composition;` when constructing composite scenes.
3. Build a composition tree using the nodes, animations, and effect primitives provided by the package, then render it through a platform integration.
4. Combine composition with the charting or gauges libraries to layer visualizations, annotations, and UI chrome in the same scene.

## Usage Example

```csharp
using VelloSharp.Composition;

var metrics = new LabelMetrics(width: 120, height: 24, baseline: 18);
Console.WriteLine($"Label metrics {metrics.Width}x{metrics.Height}");
```

## Next Steps

- Browse the API reference to explore the node hierarchy, animation system, and resource management patterns.
- Study the composition samples to understand frame invalidation and incremental updates.

