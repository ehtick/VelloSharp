# VelloSharp.Rendering

`VelloSharp.Rendering` packages shared rendering helpers that sit on top of the core `VelloSharp` APIs. It provides host-agnostic render path utilities and buffer descriptors so platforms can feed `Renderer.Render` without duplicating stride and format negotiation code.

## Getting Started

1. Install the package with `dotnet add package VelloSharp.Rendering`.
2. Import the namespace via `using VelloSharp.Rendering;` wherever you assemble render targets.
3. Describe your output surface with `RenderTargetDescriptor`, supplying width, height, pixel format, and stride.
4. Call `VelloRenderPath.Render` with a `Renderer`, `Scene`, and either a managed `Span<byte>` buffer or an unmanaged pointer.

## Usage Example

```csharp
using System;
using System.Buffers;
using System.Numerics;
using VelloSharp;
using VelloSharp.Rendering;

var descriptor = new RenderTargetDescriptor(
    Width: 1280,
    Height: 720,
    Format: RenderFormat.Bgra8,
    StrideBytes: 1280 * 4);

var buffer = ArrayPool<byte>.Shared.Rent(descriptor.RequiredBufferSize);
try
{
    using var scene = new Scene();
    using var renderer = new Renderer(descriptor.Width, descriptor.Height);

    // Populate the scene with Vello primitives.
    var path = new PathBuilder()
        .MoveTo(0, 0)
        .LineTo(1280, 0)
        .LineTo(1280, 720)
        .LineTo(0, 720)
        .Close();
    scene.FillPath(path, FillRule.NonZero, Matrix3x2.Identity, RgbaColor.FromBytes(0, 144, 255));

    var renderParams = new RenderParams(descriptor.Width, descriptor.Height, RgbaColor.FromBytes(18, 18, 18));
    VelloRenderPath.Render(renderer, scene, buffer.AsSpan(0, descriptor.RequiredBufferSize), renderParams, descriptor);
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}
```

## Next Steps

- Keep `RenderTargetDescriptor` alongside platform-specific swapchain or framebuffer code to validate buffer sizes before calling into the renderer.
- When interoperating with native surfaces, use the `VelloRenderPath.Render` overload that accepts an `IntPtr` to avoid extra copies.
- Combine the helpers with `VelloSharp.Integration` packages (Avalonia, WPF, WinForms, Uno) to present the rendered buffers inside UI frameworks.
