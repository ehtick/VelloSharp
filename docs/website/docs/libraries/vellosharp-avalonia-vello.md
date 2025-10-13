# VelloSharp.Avalonia.Vello

`VelloSharp.Avalonia.Vello` connects the Vello renderer with Avalonia UI, allowing you to host high-performance GPU visuals inside Avalonia controls.

## Getting Started

1. Install with `dotnet add package VelloSharp.Avalonia.Vello`.
2. Add `using VelloSharp.Avalonia.Vello;` in your Avalonia project.
3. Register the provided rendering services (for example in your `AppBuilder`) so Avalonia controls can reference the Vello-backed surfaces.
4. Drop the supplied controls or drawing surfaces into your XAML or code-behind and feed them scenes produced via `VelloSharp` or `VelloSharp.Composition`.

## Usage Example

```csharp
using Avalonia;
using VelloSharp.Avalonia.Vello;

AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .UseVello(new VelloPlatformOptions { FramesPerSecond = 120 })
    .StartWithClassicDesktopLifetime(args);
```

## Next Steps

- Read the API reference for details on the control contracts and adapter services.
- Combine with `VelloSharp.Charting.Avalonia` for rich interactive visualizations in Avalonia applications.

