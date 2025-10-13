# VelloSharp.ChartRuntime.Windows

`VelloSharp.ChartRuntime.Windows` adds Windows-specific scheduling, threading, and swapchain coordination to the chart runtime subsystem.

## Getting Started

1. Install via `dotnet add package VelloSharp.ChartRuntime.Windows`.
2. Add `using VelloSharp.ChartRuntime.Windows;` in Windows-targeted projects that host real-time charts.
3. Configure the runtime to use the dispatcher, composition thread, or swapchain helpers provided by this package.
4. Pair it with `VelloSharp.ChartRuntime` for platform-agnostic logic and with `VelloSharp.Integration.Wpf` or `.WinForms` for UI delivery.

## Usage Example

```csharp
using System.Windows.Forms;
using VelloSharp.ChartRuntime.Windows.WinForms;

using var control = new Control();
using var tickSource = new WinFormsTickSource(control);
```

## Next Steps

- Review the API reference for Windows-specific scheduler implementations and interop bridges.
- Validate behaviour under high-frequency updates and multiple monitor setups to ensure smooth playback.

