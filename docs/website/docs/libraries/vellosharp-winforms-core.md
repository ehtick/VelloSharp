# VelloSharp.WinForms.Core

`VelloSharp.WinForms.Core` supplies the Win32 interop, control rendering helpers, and message-loop utilities that underpin the WinForms integration layer.

## Getting Started

1. Install with `dotnet add package VelloSharp.WinForms.Core`.
2. Import `using VelloSharp.WinForms.Core;` inside your WinForms shared libraries or application.
3. Use the helper types to bind WinForms control handles to GPU surfaces and to synchronize rendering with the WinForms message pump.
4. Pair the core package with `VelloSharp.Integration.WinForms` to access ready-made controls for embedding Vello scenes.

## Usage Example

```csharp
using System.Drawing;
using VelloSharp.WinForms;

var bitmap = new Bitmap(256, 256, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
using var velloBitmap = VelloBitmap.FromBitmap(bitmap);
Console.WriteLine($"Vello image size: {velloBitmap.Width}x{velloBitmap.Height}");
```

## Next Steps

- Check the API reference for window handle adapters, dispatcher helpers, and frame pump utilities.
- Review the WinForms samples for guidance on threading, double buffering, and resource cleanup.

