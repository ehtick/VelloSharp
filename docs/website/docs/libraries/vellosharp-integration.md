# VelloSharp.Integration

`VelloSharp.Integration` centralises reusable hosting helpers that bridge Vello renderers into UI frameworks. It contains
Avalonia controls, render-path negotiation services, and utility types that higher-level packages (Skia, WinForms, WPF)
build upon.

## Getting Started

1. Install with `dotnet add package VelloSharp.Integration`.
2. Reference the package alongside the platform-specific integration (for example `VelloSharp.Avalonia.Vello`).
3. Use the provided controls or render-path helpers to set up renderer lifetimes, swap between GPU and sparse targets, and
   hook frame callbacks.
4. Combine with `VelloSharp.Gpu` and `VelloSharp.Text` to drive complete rendering pipelines.

## Usage Example

```csharp
using Avalonia;
using VelloSharp.Integration.Avalonia;

AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .UseVelloSkiaTextServices()
    .StartWithClassicDesktopLifetime(args);
```

The `UseVelloSkiaTextServices` extension bootstraps Vello-backed text services when Avalonia is running on a compatible
renderer. Other helpers, such as `VelloView`, expose frame events and renderer swapping logic in a single control.

## Next Steps

- Review the platform-specific guides (Skia, WinForms, WPF) to see how they layer on top of the shared integration helpers.
- Inspect the samples under `samples/` to see real applications using the Avalonia `VelloView` and render-path services.
- Pair the integration package with the charting suite to quickly host real-time dashboards inside Avalonia or Windows UI shells.

