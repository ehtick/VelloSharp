# VelloSharp .NET bindings

This repository hosts the managed bindings that expose the [`vello`](https://github.com/linebender/vello)
renderer to .NET applications. The bindings are layered:

- `vello_ffi` – a Rust `cdylib` that wraps the core Vello renderer behind a C ABI.
- `VelloSharp` – a C# library that provides a safe, idiomatic API over the native exports.
- `samples/AvaloniaVelloDemo` – an Avalonia desktop sample that renders an animated scene and blits it
  to a `WriteableBitmap` (making it easy to integrate with Skia-backed UI frameworks).

## Building the native library

Install the Rust toolchain (Rust 1.86 or newer) before building the managed projects. The `VelloSharp`
MSBuild project now drives `cargo build -p vello_ffi` automatically for the active .NET runtime identifier
and configuration. Running any of the following commands produces the native artifact and copies it to the
managed output directory under `runtimes/<rid>/native/` (and alongside the binaries for convenience):

```bash
dotnet build VelloSharp/VelloSharp.csproj
dotnet build samples/AvaloniaVelloDemo/AvaloniaVelloDemo.csproj
dotnet run --project samples/AvaloniaVelloDemo/AvaloniaVelloDemo.csproj
```

By default the current host target triple is used. To build for an alternate RID, pass `-r <rid>` when
invoking `dotnet build` or set `RuntimeIdentifier` in your consuming project; make sure the corresponding
Rust target is installed (`rustup target add <triple>`). The produced files are named:

| RID        | Triple                       | Artifact                 |
| ---------- | ---------------------------- | ------------------------ |
| `win-x64`  | `x86_64-pc-windows-msvc`     | `vello_ffi.dll`          |
| `win-arm64`| `aarch64-pc-windows-msvc`    | `vello_ffi.dll`          |
| `osx-x64`  | `x86_64-apple-darwin`        | `libvello_ffi.dylib`     |
| `osx-arm64`| `aarch64-apple-darwin`       | `libvello_ffi.dylib`     |
| `linux-x64`| `x86_64-unknown-linux-gnu`   | `libvello_ffi.so`        |
| `linux-arm64`| `aarch64-unknown-linux-gnu`| `libvello_ffi.so`        |

## Packing NuGet packages

The `VelloSharp` project is now NuGet-ready. Packing requires the native artifacts for each
runtime you want to redistribute:

1. Build the native libraries (e.g., via CI) and collect them under a directory layout such as
   `runtimes/<rid>/native/<library>`.
2. Set the `VelloNativeAssetsDirectory` property to that directory when invoking `dotnet pack`
   (for example, `dotnet pack VelloSharp/VelloSharp.csproj -c Release -p:VelloSkipNativeBuild=true -p:VelloNativeAssetsDirectory=$PWD/artifacts/runtimes`).
3. Optionally verify all runtimes by keeping the default `VelloRequireAllNativeAssets=true`, or
   relax the check with `-p:VelloRequireAllNativeAssets=false` when experimenting locally.

The generated `.nupkg` and `.snupkg` files are emitted under `artifacts/nuget/`.

In addition to the aggregate `VelloSharp` package, each runtime is also packed individually as
`VelloSharp.Native.<rid>` containing only the native asset under `runtimes/<rid>/native/`. These
packages can be consumed directly when you need granular control over native deployment.

## Using `VelloSharp`

Reference the `VelloSharp` project from your solution or publish it as a NuGet package.
A minimal render loop looks like:

```csharp
using System.Numerics;
using VelloSharp;

using var renderer = new Renderer(width: 1024, height: 768);
using var scene = new Scene();
var path = new PathBuilder();
path.MoveTo(100, 100).LineTo(700, 200).LineTo(420, 540).Close();
scene.FillPath(path, FillRule.NonZero, Matrix3x2.Identity, RgbaColor.FromBytes(0x47, 0x91, 0xF9));

var buffer = new byte[1024 * 768 * 4];
renderer.Render(
    scene,
    new RenderParams(1024, 768, RgbaColor.FromBytes(0x10, 0x10, 0x12))
    {
        Format = RenderFormat.Bgra8,
    },
    buffer,
    strideBytes: 1024 * 4);
```

`buffer` now contains BGRA pixels ready for presentation via SkiaSharp, Avalonia or any other API; omit the assignment to `Format` to receive RGBA output instead.

## Brushes and Layers

`Scene.FillPath` and `Scene.StrokePath` accept the `Brush` hierarchy, enabling linear/radial gradients and image brushes in addition to solid colors. Example:

```csharp
var brush = new LinearGradientBrush(
    start: new Vector2(0, 0),
    end: new Vector2(256, 0),
    stops: new[]
    {
        new GradientStop(0f, RgbaColor.FromBytes(255, 0, 128)),
        new GradientStop(1f, RgbaColor.FromBytes(0, 128, 255)),
    });
scene.FillPath(path, FillRule.NonZero, Matrix3x2.Identity, brush);
```

Layer management is accessible through `Scene.PushLayer`, `Scene.PushLuminanceMaskLayer`, and `Scene.PopLayer`, giving full control over blend modes and clip groups.

## Images and Glyphs

Use `Image.FromPixels` and `Scene.DrawImage` to render textures directly. Glyph runs can be issued via `Scene.DrawGlyphRun`, which takes a `Font`, a glyph span, and `GlyphRunOptions` for fill or stroke rendering.

## Renderer Options

`Renderer` exposes an optional `RendererOptions` argument to select CPU-only rendering or limit the available anti-aliasing pipelines at creation time.

## Avalonia integration

`VelloSharp.Integration` includes a reusable `VelloView` control that owns the renderer, scene, and
backing `WriteableBitmap`. Subscribe to `RenderFrame` or override `OnRenderFrame` to populate the
scene — the control manages size changes, render-loop invalidation, and stride/format negotiation for you:

```csharp
using VelloSharp.Integration.Avalonia;

public sealed class DemoView : VelloView
{
    public DemoView()
    {
        RenderParameters = RenderParameters with
        {
            BaseColor = RgbaColor.FromBytes(18, 18, 20),
            Antialiasing = AntialiasingMode.Msaa8,
        };

        RenderFrame += context =>
        {
            var scene = context.Scene;
            scene.Reset();
            // build your Vello scene here
        };
    }
}
```

Set `IsLoopEnabled` to `false` if you prefer to drive the control manually via `RequestRender()`.

### Surface-backed rendering

For swapchain integration the `VelloSurfaceContext`, `VelloSurface`, and `VelloSurfaceRenderer`
types wrap the new native surface API. They keep Vello's renderer associated with a platform window
and avoid CPU readbacks:

```csharp
var context = new VelloSurfaceContext();
var descriptor = new SurfaceDescriptor
{
    Width = 1920,
    Height = 1080,
    PresentMode = PresentMode.AutoVsync,
    Handle = SurfaceHandle.FromWin32(hwnd),
};
using var surface = new VelloSurface(context, descriptor);
using var renderer = new VelloSurfaceRenderer(surface);

renderer.Render(surface, scene, new RenderParams(1920, 1080, RgbaColor.FromBytes(0, 0, 0))
{
    Format = RenderFormat.Rgba8,
});
```

`VelloSurfaceView` mirrors the bitmap-based `VelloView` control but first attempts to acquire a native
surface for the hosting `TopLevel` (using `HWND` on Windows and `NSView` on macOS). When the platform
does not expose a compatible handle the control automatically falls back to the software path. The
current implementation targets the entire window swap chain, so place the control at the root of your
layout when exercising the GPU path. At present only `AntialiasingMode.Area` is honoured when the
surface renderer is active; other modes are coerced to `Area` until the native shaders gain MSAA
permutations. `SurfaceHandle.Headless` is available for headless testing.

Run the sample with:

```bash
cd samples/AvaloniaVelloDemo
dotnet run
```

The native `vello_ffi` library is copied next to the managed binaries automatically; no additional setup is
required as long as the Rust toolchain is installed.

## SkiaSharp interop

`VelloSharp.Integration.Skia.SkiaRenderBridge` renders straight into `SKBitmap` or `SKSurface`
instances and picks the correct render format based on the underlying color type and stride:

```csharp
using SkiaSharp;
using VelloSharp.Integration.Skia;

void RenderToSurface(SKSurface surface, Renderer renderer, Scene scene, RenderParams renderParams)
{
    SkiaRenderBridge.Render(surface, renderer, scene, renderParams);
    surface.Canvas.Flush();
}

void RenderToBitmap(SKBitmap bitmap, Renderer renderer, Scene scene, RenderParams renderParams)
    => SkiaRenderBridge.Render(bitmap, renderer, scene, renderParams);
```

The helper inspects the target stride and format, and falls back to an intermediate bitmap when Skia
does not expose CPU pixels for GPU-backed surfaces.

## CPU/GPU render paths

For advanced scenarios you can work directly with raw buffers using
`VelloSharp.Integration.Rendering.VelloRenderPath`:

```csharp
Span<byte> span = GetBuffer();
var descriptor = new RenderTargetDescriptor((uint)width, (uint)height, RenderFormat.Bgra8, stride);
VelloRenderPath.Render(renderer, scene, span, renderParams, descriptor);
```

The descriptor validates stride and size, while `Render` adjusts `RenderParams` to the negotiated format
before invoking the GPU or CPU pipeline.

## Repository layout recap

- `vello_ffi`: Rust source for the native shared library.
- `VelloSharp`: C# wrapper library with `Scene`, `Renderer`, and path-building helpers.
- `VelloSharp.Integration`: optional Avalonia and Skia helpers with render-path negotiation utilities.
- `samples/AvaloniaVelloDemo`: Avalonia desktop sample that exercises the bindings.
