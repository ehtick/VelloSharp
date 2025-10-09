# VelloSharp.Charting

`VelloSharp.Charting` delivers the reusable chart components, layouts, and interaction layers built on top of the chart engine.

## Getting Started

1. Install via `dotnet add package VelloSharp.Charting`.
2. Import `using VelloSharp.Charting;` when composing chart visuals.
3. Instantiate chart views or view-model helpers from this package, bind them to `VelloSharp.ChartData` sources, and host them inside your UI framework.
4. Customize axes, themes, crosshairs, and interactivity by configuring the options exposed on the chart controls.

## Usage Example

```csharp
using VelloSharp.Charting.Layout;

var orientation = AxisOrientation.Left;
Console.WriteLine($"Axis orientation: {orientation}");
```

## Next Steps

- Review the API reference for the control hierarchy, command surfaces, and customization points.
- Pair with `VelloSharp.Charting.Avalonia`, `.WinForms`, or `.Wpf` to deliver charts on each platform.

