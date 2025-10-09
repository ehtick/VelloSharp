# VelloSharp.Integration.Wpf

`VelloSharp.Integration.Wpf` ships the interop components required to host Vello-rendered content inside Windows Presentation Foundation (WPF) applications.

## Getting Started

1. Install the package: `dotnet add package VelloSharp.Integration.Wpf`.
2. Add `using VelloSharp.Integration.Wpf;` to the project that configures your WPF application.
3. Use the controls or helpers provided by this package to embed Vello surfaces inside WPF windows, handling resize and DPI changes.
4. Combine the integration with `VelloSharp.WinForms.Core` or `VelloSharp.Windows.Core` when you need shared device lifetimes across different interop hosts.

## Usage Example

```xml
<Window x:Class="VelloDemo.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vello="clr-namespace:VelloSharp.Wpf.Integration;assembly=VelloSharp.Integration.Wpf"
        Title="VelloSharp WPF" Height="450" Width="800">
    <vello:VelloNativeSwapChainView RenderMode="Continuous"
                                    PreferredBackend="Gpu"
                                    RenderSurface="OnRenderSurface" />
</Window>
```

## Next Steps

- Review the API reference to understand the dispatcher hooks and swapchain abstractions.
- Inspect the samples for concrete XAML usage patterns and frame invalidation loops.

