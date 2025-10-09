# VelloSharp

`VelloSharp` is the primary managed façade over the Vello GPU renderer. It exposes the device, queue, and scene composition APIs that every other package in the ecosystem builds upon.

## Getting Started

1. Install the package with `dotnet add package VelloSharp`.
2. Import the root namespace via `using VelloSharp;` in the projects that initialize rendering.
3. Use the device/surface builder APIs provided by the package to create a rendering context, then connect it to your chosen presentation stack.
4. Combine the renderer with one of the platform integrations (Avalonia, WPF, WinForms, Uno, or Winit) to display frames on screen.

## Usage Example

```csharp
using System.Numerics;
using VelloSharp;

using var scene = new Scene();
var path = new PathBuilder()
    .MoveTo(0, 0)
    .LineTo(128, 0)
    .LineTo(64, 96)
    .Close();
scene.FillPath(path, FillRule.NonZero, Matrix3x2.Identity, RgbaColor.FromBytes(0, 128, 255, 255));

using var renderer = new Renderer(128, 96);
var buffer = new byte[128 * 96 * 4];
var renderParams = new RenderParams(128, 96, RgbaColor.FromBytes(0, 0, 0, 255));
renderer.Render(scene, renderParams, buffer, 128 * 4);
```

## Next Steps

- Review the API reference for guidance on device lifetimes, queue submission, and scene encoding.
- Explore the samples under `samples/VelloSharp.*` to see the renderer hosted in different environments.
- Pair with `VelloSharp.Composition` when you need a retained scene graph or layered content model.

