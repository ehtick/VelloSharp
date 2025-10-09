# VelloSharp.Skia.Gpu

`VelloSharp.Skia.Gpu` enables GPU-backed rendering paths that combine Skia surfaces with Vello scene composition.

## Getting Started

1. Install the package: `dotnet add package VelloSharp.Skia.Gpu`.
2. Import `using VelloSharp.Skia.Gpu;` in the projects that host Skia on discrete or integrated GPUs.
3. Instantiate the GPU bridge types exposed by the package, wiring them into your Skia swapchain or render loop.
4. Coordinate with `VelloSharp.Skia.Core` for surface creation and with the relevant platform integration to present frames.

## Usage Example

```csharp
using SkiaSharp;

var gpuSurface = SKSurface.Create(new SKImageInfo(1024, 512));
gpuSurface.Canvas.DrawRect(SKRect.Create(0, 0, 1024, 512), new SKPaint { Color = SKColors.CornflowerBlue });
gpuSurface.Flush();
```

## Next Steps

- Review the API reference to learn about the GPU bridge objects and lifecycle hooks.
- Validate support for your target GPU backend (Vulkan, Metal, Direct3D) before deploying to production.

