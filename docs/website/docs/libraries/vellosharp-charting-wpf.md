# VelloSharp.Charting.Wpf

`VelloSharp.Charting.Wpf` brings the charting control suite to Windows Presentation Foundation with a rendering pipeline powered by Vello.

## Getting Started

1. Install the package: `dotnet add package VelloSharp.Charting.Wpf`.
2. Add `using VelloSharp.Charting.Wpf;` in your WPF project, then reference the XML namespace in XAML.
3. Drop the chart controls into your XAML layouts and bind them to `VelloSharp.ChartData` view models.
4. Combine with `VelloSharp.Integration.Wpf` to manage swapchains, DPI scaling, and rendering invalidation.

## Usage Example

```csharp
using VelloSharp.Charting.Wpf;

var control = new ChartView();
```

## Next Steps

- Review the API reference to see the list of controls, behaviors, and styling options.
- Explore the WPF samples for guidance on MVVM bindings and high-frequency data updates.

