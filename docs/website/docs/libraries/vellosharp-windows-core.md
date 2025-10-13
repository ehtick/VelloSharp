# VelloSharp.Windows.Core

`VelloSharp.Windows.Core` consolidates Windows-specific facilities such as swapchain composition, interop helpers, and device lifetime management.

## Getting Started

1. Install via `dotnet add package VelloSharp.Windows.Core`.
2. Add `using VelloSharp.Windows.Core;` in assemblies that run on Windows and need access to Direct3D or Win32 interop helpers.
3. Use the provided services to bridge native window handles, configure DXGI swapchains, or coordinate message loops with Vello rendering.
4. Combine the package with `VelloSharp.Integration.WinForms` or `VelloSharp.Integration.Wpf` when you host visuals in desktop shells.

## Usage Example

```csharp
using VelloSharp.Windows;

using var device = new VelloGraphicsDevice(1920, 1080);
using var session = device.BeginSession(1920, 1080);
Console.WriteLine($"Surface size: {session.Width}x{session.Height}");
```

## Next Steps

- Read the API reference to understand which abstractions are available for handle translation and device creation.
- Validate the configuration on each Windows version you support, especially when targeting high DPI or HDR displays.

