# VelloSharp .NET bindings

This repository hosts the managed bindings that expose the [`vello`](https://github.com/linebender/vello)
renderer to .NET applications. The codebase is split into native FFI crates, managed bindings, integration
helpers, and sample applications:

- Native FFI crates:
  - `vello_ffi` – wraps the renderer, shader, SVG, Velato/Lottie, text, and wgpu stacks behind a single C ABI,
    including surface/swapchain helpers for GPU presentation.
  - `peniko_ffi` – bridges paint/brush data so gradients, images, and style metadata can be inspected or
    constructed from managed code.
  - `kurbo_ffi` – exposes the geometry primitives used across the stack (affine transforms, Bézier paths,
    and bounding-box helpers) without pulling in the full Rust curve library.
  - `winit_ffi` – forwards windowing, input, and swap-chain negotiation so native event loops can be driven
    from .NET when desired.
  - `accesskit_ffi` – serialises/deserialises [AccessKit](https://accesskit.dev) tree updates and action requests
    so accessibility data can flow between managed code and platform adapters.
- Managed assemblies:
  - `VelloSharp` – idiomatic C# wrappers for all native exports: scenes, fonts, images, surface renderers,
    the wgpu device helpers, and the `KurboPath`/`KurboAffine` and `PenikoBrush` utilities.
  - `VelloSharp.Skia` – a Skia-inspired helper layer that maps `SKCanvas`/`SKPath`-style APIs onto Vello
    primitives for easier porting of existing SkiaSharp code.
  - `VelloSharp.Integration` – optional helpers for Avalonia, SkiaSharp interop, and render-path negotiation.
  - `VelloSharp.Avalonia.Winit` – Avalonia host glue that drives the winit-based surface renderer through the
    managed bindings.
  - `VelloSharp.Avalonia.Vello` – Avalonia platform abstractions that adapt Vello surfaces and inputs into
    application-friendly controls.
- Samples:
  - `samples/AvaloniaVelloDemo` – minimal Avalonia desktop host covering CPU and GPU render paths.
  - `samples/AvaloniaVelloExamples` – expanded scene catalogue with renderer option toggles and surface fallbacks.
  - `samples/AvaloniaSkiaMotionMark` – a side-by-side Skia/Vello motion-mark visualiser built on the integration
    layer.

## Project status

- **FFI crates** – `vello_ffi`, `peniko_ffi`, `winit_ffi`, and `accesskit_ffi` expose 100% of their exported
  functions to .NET. `kurbo_ffi` is feature-complete for the bindings in this repository, with only six geometry
  helpers intentionally left unbound (see `docs/ffi-api-coverage.md`). Native builds are validated across Windows,
  macOS, Linux, Android, iOS, and WebAssembly RIDs, and they share the same `cargo` feature flags as upstream Vello.
- **Managed bindings** – `VelloSharp` surfaces the full renderer, scene graph, surface contexts, glyph and
  image helpers, SVG/Velato decoders, and the wgpu device/surface management APIs. Disposable wrappers and span
  validators guard the native lifetimes, and the `RendererOptions`/`RenderParams` mirrors keep behaviour in sync
  with the Rust implementation.
- **Integration libraries** – `VelloSharp.Integration` supplies the Avalonia `VelloView`, the `SkiaRenderBridge`
  for direct `SKSurface` rendering, and CPU/GPU render-path abstractions. `VelloSharp.Avalonia.*` layers add the
  winit event loop bridge plus the platform abstraction that produces swapchain-backed Avalonia controls.
- **Samples and tooling** – the Avalonia demos ship with automated runtime asset copying, configurable frame
  pacing, and software/GPU fallbacks. `STATUS.md` and the plans under `docs/` track the remaining backlog for
  surface handles, validation, and additional platform glue.
- **Packaging** – `dotnet pack` produces the aggregate `VelloSharp` NuGet in addition to
  `VelloSharp.Native.<rid>` runtime packages. Helper scripts in `scripts/` collect, copy, and repackage the
  native artifacts for CI and local workflows.

## Building the native library

Install the Rust toolchain (Rust 1.86 or newer) before building the managed projects. The `VelloSharp`
MSBuild project now drives `cargo build` for every required native crate (`accesskit_ffi`, `vello_ffi`,
`kurbo_ffi`, `peniko_ffi`, and `winit_ffi`) for the active .NET runtime identifier and configuration.
Running any of the following commands produces the native artifacts and copies them to the managed output
directory under `runtimes/<rid>/native/` (and alongside the binaries for convenience):

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

> **Note:** The native crates enable the `std` feature for `kurbo`/`peniko` internally so the FFI layer can
> build against clean upstream submodules. If you invoke `cargo build` manually for a specific crate, pass
> the same feature flags (or build through the `VelloSharp` project) to avoid `kurbo requires the \`std\`
> feature` errors.

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
packages can be consumed directly when you need granular control over native deployment. When
packing the managed package you can toggle to native-package dependencies with
`-p:VelloUseNativePackageDependencies=true`; provide a subset via
`-p:VelloNativePackageIds="VelloSharp.Native.win-x64;VelloSharp.Native.win-arm64"` when testing locally.

### Native asset NuGet packages

The repository tracks per-RID packaging projects under `packaging/VelloSharp.Native.*`. Each project wraps
the native runtime built by `cargo` into a standalone NuGet package so downstream applications can reference
only the assets they need. A typical workflow looks like this:

- Build the native crates for the desired RID(s) (`dotnet build -r osx-arm64 VelloSharp/VelloSharp.csproj`).
- Run `./scripts/copy-runtimes.sh` to sync the generated artifacts into both sample outputs and each
  `packaging/VelloSharp.Native.<rid>/runtimes/<rid>/native` directory.
- Pack the native project you care about (e.g., `dotnet pack packaging/VelloSharp.Native.osx-arm64/VelloSharp.Native.osx-arm64.csproj`).
- Reference `VelloSharp.Native.<rid>` from your application or include it as an additional dependency inside a
  higher-level distribution.

The packaging props also emit fallback copies (for example `osx` alongside `osx-arm64`) so that RID roll-forward
continues to work when .NET probes `runtimes/<baseRid>/native`. When only managed assets are required, the sample
projects conditionally reference these packaging projects so the native dylibs land in `bin/<TFM>/runtimes/` without
custom MSBuild logic.

## Available NuGet packages

- `VelloSharp` – managed bindings that surface the renderer, scene graph, wgpu interop, SVG, Velato, and surface
  helpers.
- `VelloSharp.Skia` – Skia-inspired drawing primitives built on top of the Vello renderer.
- `VelloSharp.Integration` – auxiliary helpers for Avalonia, SkiaSharp interop, and host render loops.
- `VelloSharp.Avalonia.Winit` – Avalonia-facing abstractions for driving the winit surface renderer.
- `VelloSharp.Avalonia.Vello` – Avalonia platform integration that wires Vello surfaces into desktop applications.

### Native runtime packages (`VelloSharp.Native.<rid>`)

- `VelloSharp.Native.android-arm64`
- `VelloSharp.Native.browser-wasm`
- `VelloSharp.Native.ios-arm64`
- `VelloSharp.Native.iossimulator-x64`
- `VelloSharp.Native.linux-arm64`
- `VelloSharp.Native.linux-x64`
- `VelloSharp.Native.osx-arm64`
- `VelloSharp.Native.osx-x64`
- `VelloSharp.Native.win-arm64`
- `VelloSharp.Native.win-x64`

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

## Extended bindings

The native layer now ships with optional helpers for common scene sources and GPU surfaces. All of them
round-trip through the managed API so you can mix and match with the existing `Scene` primitives.

### SVG scenes

`VelloSvg` uses the bundled `vello_svg` parser to ingest an SVG asset and append the generated scene graph to any
existing `Scene`:

```csharp
using var scene = new Scene();
using var svg = VelloSvg.LoadFromFile("Assets/logo.svg", scale: 1.5f);
svg.Render(scene);

// Optionally apply additional transforms or authoring on the scene afterwards.
```

Use `LoadFromUtf8` for in-memory buffers and query the intrinsic size via the `Size` property to fit your layout.

### Lottie / Velato compositions

The [`velato`](https://github.com/linebender/velato) submodule provides high-quality Lottie playback. The managed
wrappers expose composition metadata and let you render into an existing `Scene` or build a standalone one per
frame:

```csharp
using var composition = VelatoComposition.LoadFromFile("Assets/intro_lottie.json");
using var renderer = new VelatoRenderer();

var info = composition.Info; // duration, frame rate, target size
using var scene = renderer.Render(composition, frame: 42);

// Blend multiple compositions into a shared scene
renderer.Append(scene, composition, frame: 43, alpha: 0.7);
```

### GPU interop via wgpu

When paired with `wgpu`, Vello can target swapchain textures directly. The managed side wraps the core handles so
you can drive the pipeline from your own windowing layer:

```csharp
using var instance = new WgpuInstance();
var surfaceDescriptor = new SurfaceDescriptor
{
    Width = width,
    Height = height,
    PresentMode = PresentMode.AutoVsync,
    Handle = SurfaceHandle.FromWin32(hwnd), // or FromAppKit / FromWayland / FromXlib
};
using var surface = WgpuSurface.Create(instance, surfaceDescriptor);
using var adapter = instance.RequestAdapter(new WgpuRequestAdapterOptions
{
    PowerPreference = WgpuPowerPreference.HighPerformance,
    CompatibleSurface = surface,
});
using var device = adapter.RequestDevice(new WgpuDeviceDescriptor
{
    Limits = WgpuLimitsPreset.Default,
});
using var renderer = new WgpuRenderer(device);

var surfaceTexture = surface.AcquireNextTexture();
using (var view = surfaceTexture.CreateView())
{
    renderer.Render(scene, view, new RenderParams(width, height, baseColor)
    {
        Format = RenderFormat.Bgra8,
    });
}
surfaceTexture.Present();
surfaceTexture.Dispose();
```

All handles are disposable and throw once released, making it easy to integrate with `using` scopes. See the
Avalonia helpers below for a higher-level example.

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

> Tip: When interoperating with native paint data, wrap `PenikoBrush` handles with `Brush.FromPenikoBrush` to
> reuse gradients or solid fills produced via `peniko_ffi`.

## Geometry helpers (Kurbo)

`KurboPath`, `KurboAffine`, and the rest of the managed geometry types are thin wrappers over `kurbo_ffi`. They
let you construct Bézier paths, apply affine transforms, and query bounds without pulling the full Rust library
into your application:

```csharp
using var path = new KurboPath();
path.MoveTo(0, 0);
path.LineTo(128, 64);
path.CubicTo(256, 64, 256, 256, 128, 256);
path.Close();

var bounds = path.GetBounds();
path.ApplyAffine(KurboAffine.FromMatrix3x2(Matrix3x2.CreateRotation(MathF.PI / 4)));
var elements = path.GetElements();
```

These helpers surface a managed-friendly representation of the geometry used throughout Vello without
introducing additional allocations in the hot path.

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

### Surface-backed rendering (wgpu)

The Avalonia integration now drives the shared wgpu wrappers. `VelloSurfaceView` tries to obtain a native
platform handle (`HWND`, `NSWindow`, and, in a future update, Wayland/X11). When the handle is available it creates
a `WgpuInstance`, `WgpuSurface`, and `WgpuRenderer`, rendering directly into swapchain textures and presenting
them via `wgpu`. If the platform cannot provide a compatible handle, or surface configuration fails, the control
transparently falls back to the software `VelloView` path. You can continue to update the scene through
`RenderFrame` without special casing either mode.

Applications that need deeper control can replicate the same sequence manually: construct a `SurfaceDescriptor`
from a window handle with `SurfaceHandle.FromWin32`, `.FromAppKit`, `.FromWayland`, or `.FromXlib`, configure the
surface with your preferred `PresentMode`, and call `WgpuRenderer.Render` with the acquired texture view. The
control exposes `RendererOptions`, `RenderParameters`, and `IsLoopEnabled` so you can tune anti-aliasing, swapchain
formats, or frame pacing at runtime.

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

## Automation scripts

The repository includes helper scripts that wire up the CI flow and simplify local builds. All scripts emit
artifacts under `artifacts/` and are safe to combine with `dotnet build`/`cargo` invocations.

### Native builds

- `scripts/build-native-macos.sh [target] [profile] [sdk] [rid]` – builds `vello_ffi` for macOS/iOS targets. Pass
  an Apple SDK name (for example `macosx` or `iphoneos`) to compile against a specific SDK, and optionally override
  the runtime identifier. Defaults to `x86_64-apple-darwin` in `release` mode.
- `scripts/build-native-linux.sh [target] [profile] [rid]` – cross-compiles the shared object for GNU/Linux
  platforms, defaulting to `x86_64-unknown-linux-gnu`. Supply `aarch64-unknown-linux-gnu` to produce the ARM64
  variant.
- `scripts/build-native-windows.ps1 [target] [profile] [rid]` – a PowerShell helper for the Windows MSVC builds.
  Run from PowerShell or pwsh. Automatically maps the target triple to `win-x64`/`win-arm64` unless a RID is provided.
- `scripts/build-native-android.sh [target] [profile] [rid]` – targets Android via the NDK. Requires
  `ANDROID_NDK_HOME` and adds the toolchain binaries to `PATH` before calling `cargo`.
- `scripts/build-native-wasm.sh [target] [profile] [rid]` – compiles the WebAssembly artifact (`vello_ffi.wasm`) for
  `wasm32-unknown-unknown`.

All build scripts copy the produced library into `artifacts/runtimes/<rid>/native/`, making the payload immediately
available to packaging steps.

### Artifact management and packaging

- `scripts/collect-native-artifacts.sh [source-dir] [dest-dir]` – normalises arbitrary build outputs into the
  `runtimes/<rid>/native/` layout by scanning for `native` folders and copying their contents into the destination.
  Used by CI to gather per-RID outputs before packing.
- `scripts/copy-runtimes.sh [artifacts-dir] [targets…]` – copies the assembled runtime folder into project outputs
  and sample applications. The script defaults to propagating assets into `Debug`/`Release` `net8.0` builds for the
  library, integrations, and samples, but you can override the target projects, configurations, or frameworks via
  `COPY_CONFIGURATIONS` / `COPY_TARGET_FRAMEWORKS`.
- `scripts/pack-native-nugets.sh [runtimes-dir] [output-dir]` – iterates the collected runtimes and packs the
  corresponding `VelloSharp.Native.<rid>` NuGet packages. Each package simply embeds the `native` folder for its RID.
- `scripts/pack-managed-nugets.sh [output-dir] [native-feed]` – builds the managed projects in `Release`, registers a
  temporary NuGet source pointing at the native packages, and packs the aggregate `VelloSharp` NuGet with
  `VelloUseNativePackageDependencies=true`. Run this after `pack-native-nugets.sh` to produce a coherent set of
  packages under `artifacts/nuget/`.

## Repository layout recap

- `vello_ffi`: Rust source for the native shared library.
- `VelloSharp`: C# wrapper library with `Scene`, `Renderer`, and path-building helpers.
- `VelloSharp.Integration`: optional Avalonia and Skia helpers with render-path negotiation utilities.
- `samples/AvaloniaVelloDemo`: Avalonia desktop sample that exercises the bindings.
- `samples/AvaloniaVelloExamples`: showcases the expanded scene catalogue on Avalonia with GPU fallback logic.
- `extern/vello`: upstream renderer sources (core crate, sparse strips, shaders, and examples).
- `extern/kurbo`: geometry primitives consumed by `kurbo_ffi` and Vello.
- `extern/peniko`: brush/image utilities re-exported through `extern/peniko_shim`.
- `extern/peniko_shim`: compatibility shim that preserves the legacy `peniko` crate API surface.
- `extern/velato`: submodule that powers the Lottie/After Effects pipeline.
- `extern/vello_svg`: submodule responsible for SVG parsing.
- `extern/wgpu`: vendored subset of wgpu used by the FFI for portable GPU access.
- `extern/winit`: upstream windowing stack used by the native event-loop bridge.

## License

The entire repository—including the managed bindings, native FFI crates, integrations, and samples—is distributed
under the GNU Affero General Public License v3.0. NuGet packages produced via `dotnet pack` ship with the same AGPLv3
license text (`LICENSE`) so the published artifacts match the source tree.

To honour upstream obligations, the packages also embed the MIT/Apache-2.0 notices from the Linebender components the
FFI layer depends on (`vello`, `kurbo`, `peniko`, `wgpu`, etc.). Vendored submodules retain their original licenses—
refer to each directory for the exact terms.
