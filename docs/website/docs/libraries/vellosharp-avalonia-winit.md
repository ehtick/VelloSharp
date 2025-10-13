# VelloSharp.Avalonia.Winit

`VelloSharp.Avalonia.Winit` adds Winit-backed windowing and input support to Avalonia applications that embed Vello content.

## Getting Started

1. Install the package: `dotnet add package VelloSharp.Avalonia.Winit`.
2. Import `using VelloSharp.Avalonia.Winit;` when configuring your Avalonia host.
3. Enable the Winit platform in your bootstrapper by calling the extension methods exposed by this package.
4. Pair the windowing layer with `VelloSharp.Avalonia.Vello` so that rendered frames flow through the Winit-managed swapchain.

## Usage Example

```csharp
using Avalonia;
using VelloSharp.Avalonia.Winit;

AppBuilder.Configure<App>()
    .UseWinit()
    .StartWithClassicDesktopLifetime(args);
```

## Next Steps

- Consult the API reference for platform options, window configuration hooks, and lifecycle events.
- Test on each target platform you intend to ship because Winit capabilities vary across desktop and mobile environments.

