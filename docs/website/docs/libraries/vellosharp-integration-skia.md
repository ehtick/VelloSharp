# VelloSharp.Integration.Skia

`VelloSharp.Integration.Skia` packages the higher-level wiring required to embed Vello-rendered output into existing Skia-based UI frameworks and surfaces.

## Getting Started

1. Install the package through `dotnet add package VelloSharp.Integration.Skia`.
2. Import `using VelloSharp.Integration.Skia;` when bootstrapping your Skia host.
3. Use the helper classes in this package to connect Skia surfaces to Vello scenes, handling resize, invalidation, and resource reuse.
4. Pair the integration with either `VelloSharp.Skia.Gpu` or `VelloSharp.Skia.Cpu` depending on whether you render on the GPU or CPU.

## Usage Example

```csharp
using SkiaSharp;
using System.Numerics;
using VelloSharp;
using VelloSharp.Integration.Skia;
using VelloSharp.Rendering;

using var scene = new Scene();
scene.FillPath(new PathBuilder().MoveTo(0, 0).LineTo(256, 0).LineTo(256, 256).Close(), FillRule.NonZero, Matrix3x2.Identity, RgbaColor.Crimson);

using var surface = SKSurface.Create(new SKImageInfo(256, 256));
using var renderer = new Renderer(256, 256);
var renderParams = new RenderParams(256, 256, RgbaColor.FromBytes(0, 0, 0, 255));
SkiaRenderBridge.Render(surface, renderer, scene, renderParams);
surface.Flush();
```

## Next Steps

- Explore the API reference for control adapters, dispatcher hooks, and lifecycle helpers.
- Review the samples that host charting or tree data grid visuals on top of Skia for end-to-end patterns.

