# VelloSharp.Skia.Core

`VelloSharp.Skia.Core` provides the shared surface and resource glue needed to host Vello content inside Skia contexts.

## Getting Started

1. Install with `dotnet add package VelloSharp.Skia.Core`.
2. Add `using VelloSharp.Skia.Core;` where you configure Skia surfaces.
3. Create bridge objects (surface providers, framebuffer adapters, etc.) through the factory methods in this package before submitting work to Skia.
4. Coordinate lifetimes with `VelloSharp.Gpu` so buffers and textures remain synchronized across Skia and Vello.

## Usage Example

```csharp
using SkiaSharp;

var surface = SKSurface.Create(new SKImageInfo(256, 256));
surface.Canvas.Clear(SKColors.White);
using var paint = new SKPaint { Color = SKColors.DarkOrange, IsAntialias = true };
surface.Canvas.DrawCircle(128, 128, 96, paint);
surface.Flush();
```

## Next Steps

- See the API reference for the available surface adapters and device helpers.
- Pair with `VelloSharp.Skia.Gpu` or `VelloSharp.Skia.Cpu` depending on your target environment.

