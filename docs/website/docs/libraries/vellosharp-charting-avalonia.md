# VelloSharp.Charting.Avalonia

`VelloSharp.Charting.Avalonia` brings the charting controls to Avalonia UI, making it easy to deliver cross-platform dashboards.

## Getting Started

1. Install with `dotnet add package VelloSharp.Charting.Avalonia`.
2. Add `using VelloSharp.Charting.Avalonia;` to your Avalonia project.
3. Register the charting services (for example in the application bootstrapper) and place the provided controls in XAML or code.
4. Supply data from `VelloSharp.ChartData` and render through the existing Avalonia layout system with GPU acceleration provided by the Vello renderer.

## Usage Example

```csharp
using VelloSharp.Charting.Avalonia;

var chartView = new ChartView();
```

## Next Steps

- Review the API reference for Avalonia-specific controls, styling hooks, and input handling.
- Inspect the `samples/VelloSharp.Charting.AvaloniaSample` project for end-to-end usage patterns.

