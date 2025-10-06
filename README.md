# VelloSharp .NET bindings

This repository hosts the managed bindings that expose the Linebender graphics stack—[`vello`](https://github.com/linebender/vello)
for rendering, [`wgpu`](https://github.com/gfx-rs/wgpu) for portable GPU access, [`winit`](https://github.com/rust-windowing/winit)
for cross-platform windowing, `peniko`/`kurbo` for brush and geometry data, and the text pipeline built on
`parley`, `skrifa`, `swash`, and `fontique`—to .NET applications. `VelloSharp` wraps these native crates together
with [`AccessKit`](https://accesskit.dev) so scene building, surface presentation, input, accessibility, and text
layout share a cohesive managed API while still exposing low-level control over `wgpu` devices, queues, and surfaces.

The codebase is split into native FFI crates, managed bindings, integration helpers, and sample applications:

- Native FFI crates (under `ffi/`):
  - `ffi/vello_ffi` – wraps the renderer, shader, SVG, Velato/Lottie, text, and wgpu stacks behind a single C ABI,
    including surface/swapchain helpers for GPU presentation.
  - `ffi/peniko_ffi` – bridges paint/brush data so gradients, images, and style metadata can be inspected or
    constructed from managed code.
  - `ffi/kurbo_ffi` – exposes the geometry primitives used across the stack (affine transforms, Bézier paths,
    and bounding-box helpers) without pulling in the full Rust curve library.
  - `ffi/winit_ffi` – forwards windowing, input, and swap-chain negotiation so native event loops can be driven
    from .NET when desired.
  - `ffi/accesskit_ffi` – serialises/deserialises [AccessKit](https://accesskit.dev) tree updates and action requests
    so accessibility data can flow between managed code and platform adapters.
- Managed assemblies (under `bindings/`):
  - `VelloSharp` - idiomatic C# wrappers for all native exports: scenes, fonts, images, surface renderers,
    the wgpu device helpers, and the `KurboPath`/`KurboAffine` and `PenikoBrush` utilities.
  - `VelloSharp.Windows.Core` - shared Windows GPU plumbing (device/queue leasing, diagnostics, swapchain helpers, D3D11 ↔ D3D9 interop) used by WinForms and WPF hosts.
  - `VelloSharp.Integration.Wpf` - WPF controls (`VelloView`, `VelloNativeSwapChainView`) with composition-based rendering, keyed-mutex coordination, diagnostics, and backend switching.
  - `VelloSharp.Integration.WinForms` - WinForms control hosting that now delegates GPU lifetime management to `VelloSharp.Windows.Core`.
  - `VelloSharp.WinForms.Core` - Windows Forms drawing surface abstractions (`Graphics`, `Pen`, `Brush`, `Region`) powered by the shared renderer.
  - `VelloSharp.Skia` - a Skia-inspired helper layer that maps `SKCanvas`/`SKPath`-style APIs onto Vello
    primitives for easier porting of existing SkiaSharp code.
  - `VelloSharp.Integration` - optional helpers for Avalonia, SkiaSharp interop, and render-path negotiation.
  - `VelloSharp.Avalonia.Winit` - Avalonia host glue that drives the winit-based surface renderer through the
    managed bindings.
  - `VelloSharp.Avalonia.Vello` - Avalonia platform abstractions that adapt Vello surfaces and inputs into
    application-friendly controls.
- Samples:
  - `samples/AvaloniaVelloWinitDemo` - minimal Avalonia desktop host covering CPU and GPU render paths through the AvaloniaNative/Vello stack.
  - `samples/AvaloniaVelloX11Demo` - Linux-focused host that locks Avalonia to the X11 platform for backend validation.
  - `samples/AvaloniaVelloWin32Demo` - Windows host configured for the Win32 platform while exercising the Vello renderer.
  - `samples/AvaloniaVelloNativeDemo` - macOS host forced onto AvaloniaNative to vet the Vello integration end-to-end.
  - `samples/AvaloniaVelloExamples` - expanded scene catalogue with renderer option toggles and surface fallbacks.
  - `samples/AvaloniaSkiaMotionMark` - a side-by-side Skia/Vello motion-mark visualiser built on the integration layer.
  - `samples/AvaloniaSkiaSparseMotionMarkShim` - CPU sparse MotionMark shim that routes Vello scenes through the Velato Skia bridge without touching the GPU backend.
  - `samples/VelloSharp.WpfSample` - WPF composition host showcasing `VelloView`, backend toggles, diagnostics binding, and a MotionMark fast-path page driven through the new GPU render-surface API.
  - `samples/WinFormsMotionMarkShim` - Windows Forms MotionMark GPU shim built atop the shared Windows GPU context, demonstrating `VelloRenderControl`, the `RenderSurface` fast path, backend switching, and DPI-aware swapchain handling.

## Quick start builds

Run these from the repository root to go from a clean clone to native artifacts and managed binaries.

### Windows (PowerShell)

```powershell
pwsh -ExecutionPolicy Bypass -File scripts\bootstrap-windows.ps1
pwsh -File scripts\build-native-windows.ps1
dotnet build VelloSharp.sln -c Release
pwsh -File scripts\copy-runtimes.ps1
```

### macOS (bash/zsh)

```bash
./scripts/bootstrap-macos.sh
./scripts/build-native-macos.sh
dotnet build VelloSharp.sln -c Release
./scripts/copy-runtimes.sh
```

### Linux (bash)

```bash
./scripts/bootstrap-linux.sh
./scripts/build-native-linux.sh
dotnet build VelloSharp.sln -c Release
./scripts/copy-runtimes.sh
```

Each `build-native-*` script compiles every FFI crate and stages the libraries under `artifacts/runtimes/<rid>/native/`. The copy script fans the native payloads into the managed project `bin/` folders so the samples can run immediately. On Windows it also mirrors the runtimes into `net8.0-windows` outputs so the WinForms shim and MotionMark sample work out of the box.

## WPF integration

`VelloSharp.Integration.Wpf` provides a composition-first hosting experience that aligns with the shared Windows GPU stack introduced in `VelloSharp.Windows.Core`. Key capabilities include:

- `VelloView`, a WPF `Decorator` that renders into a `D3DImage` using shared textures, honours `RenderMode`, `PreferredBackend`, and `RenderLoopDriver`, and exposes a bindable `Diagnostics` property (frame-rate smoothing, swapchain/device reset counters, keyed mutex contention).
- `VelloNativeSwapChainView`, an opt-in HWND swapchain host for diagnostics, exclusive full-screen scenarios, or interop with other DirectX components.
- Automatic leasing of the shared GPU context across multiple controls, render suspension based on visibility, window state, or application activation, and keyed-mutex fallbacks with detailed diagnostics sourced from `VelloSharp.Windows.Core`.
- Both `VelloView` and the WinForms `VelloRenderControl` expose a `RenderSurface` event (via `VelloSurfaceRenderEventArgs`) so advanced callers can feed pre-recorded scenes or custom render graphs straight into the underlying `Renderer` and target surface.

The WinForms integration now consumes the very same shared Windows GPU primitives, so both `VelloRenderControl` and `VelloView` participate in the unified leasing, diagnostics, and asset-copy workflows. Refer to `samples/VelloSharp.WpfSample` for an end-to-end MVVM-friendly example that binds diagnostics, toggles render backends, and demonstrates clean suspension/resume behaviour.

## Developer setup

Bootstrap the native toolchains and Rust before building the FFI crates or running the packaging scripts:

- `scripts/bootstrap-linux.sh` installs the required build essentials, GPU/windowing headers, and rustup on Debian/Ubuntu, Fedora, Arch, and openSUSE systems (run with sudo or as root).
- `scripts/bootstrap-macos.sh` ensures the Xcode Command Line Tools, Homebrew packages (CMake, Ninja, pkg-config, LLVM, Python), and rustup are installed.
- `scripts/bootstrap-windows.ps1` must be executed from an elevated PowerShell session; it installs Visual Studio Build Tools, CMake, Ninja, Git, and rustup via winget/Chocolatey when available.

Each script is idempotent and skips packages that are already present.
## Rust dependency reference

The FFI crates (`vello_ffi`, `peniko_ffi`, `kurbo_ffi`, `winit_ffi`, `accesskit_ffi`) share the same high-level
dependencies from the vendored Vello workspace. The table summarises the crates you interact with most when
updating or auditing the bindings:

| Dependency | Role in the bindings |
| --- | --- |
| `vello` | Core renderer that powers all scene, surface, and GPU/CPU pipelines exposed through `vello_ffi`. |
| `wgpu` | Graphics abstraction used to request adapters, devices, and swapchains for the GPU-backed render paths. |
| `vello_svg` & `velato` | Asset loaders surfaced via the FFI for SVG scenes and Lottie/After Effects playback. |
| `peniko` & `kurbo` | Brush, gradient, and geometry primitives re-exported in managed code for path building. |
| `skrifa`, `swash`, `parley`, `fontique` | Font parsing, shaping, and layout stack that feeds glyph runs into Vello. |
| `accesskit` & `accesskit_winit` | Accessibility tree interchange, shared with the Avalonia integrations. |
| `winit` & `raw-window-handle` | Window/event loop abstractions used by `winit_ffi` and the Avalonia platform glue. |
| `wgpu-profiler`, `pollster`, `futures-intrusive` | Utilities that bridge async renderer calls and surface GPU timings through the bindings. |

## WGPU integration

`VelloSharp` ships first-class wrappers for the `wgpu` API so .NET applications can configure adapters, devices, and
surfaces directly before handing targets to the renderer. The binding layer mirrors the Rust API surface closely:

- `WgpuInstance`, `WgpuAdapter`, `WgpuDevice`, and `WgpuQueue` map one-to-one with the native objects and expose all
  safety checks via `IDisposable` lifetimes.
- `SurfaceHandle.FromWin32`, `.FromAppKit`, `.FromWayland`, and `.FromXlib` let you construct swapchains from any
  window handle obtained via `winit_ffi` or Avalonia's `INativePlatformHandleSurface`.
- `WgpuSurface` configuration accepts the full backend matrix (DX12, Metal, Vulkan, or GLES) so the renderer can
  adopt whatever the host platform supports without recompiling native code.
- `WgpuTexture`, `WgpuCommandEncoder`, and `WgpuBuffer` bindings make it feasible to interleave custom compute or
  upload work with Vello's own render passes when you need advanced scenarios.

The higher-level helpers in `VelloSharp.Integration` build on these primitives: Avalonia controls negotiate
swapchains through the same APIs, while the profiler hooks exposed by `wgpu-profiler` surface GPU timings back to
managed code. If you prefer a software path, the bindings can skip `wgpu` entirely and fall back to CPU rendering
without changing the managed API surface.

## Project status

- **FFI crates** – `vello_ffi`, `peniko_ffi`, `winit_ffi`, and `accesskit_ffi` expose 100% of their exported
  functions to .NET. `kurbo_ffi` is feature-complete for the bindings in this repository, with only six geometry
  helpers intentionally left unbound (see `docs/ffi-api-coverage.md`). Native builds are validated across Windows,
  macOS, Linux, Android, iOS, and WebAssembly RIDs, and they share the same `cargo` feature flags as upstream Vello.
- **Managed bindings** – `VelloSharp` surfaces the full renderer, scene graph, surface contexts, glyph and
  image helpers, SVG/Velato decoders, and the wgpu device/surface management APIs. Disposable wrappers and span
  validators guard the native lifetimes, and the `RendererOptions`/`RenderParams` mirrors keep behaviour in sync
  with the Rust implementation.
- **Integration libraries** - `VelloSharp.Windows.Core` centralises Windows-specific GPU context leasing, diagnostics,
  and swapchain/texture interop that are now consumed by both WinForms and WPF. `VelloSharp.Integration.Wpf`
  introduces the composition-based `VelloView`, the opt-in `VelloNativeSwapChainView`, keyed-mutex management, and
  bindable diagnostics, while `VelloSharp.Integration.WinForms` reuses the same core for `VelloRenderControl` and
  MotionMark shims. Cross-platform glue continues to live in `VelloSharp.Integration` (Avalonia helpers, Skia bridges),
  with `VelloSharp.Avalonia.*` layering in the winit event loop bridge and Avalonia platform abstractions.
- **Samples and tooling** – the Avalonia demos ship with automated runtime asset copying, configurable frame
  pacing, and software/GPU fallbacks. `STATUS.md` and the plans under `docs/` track the remaining backlog for
  surface handles, validation, and additional platform glue.
- **Packaging** – `dotnet pack` produces the aggregate `VelloSharp` NuGet plus the `VelloSharp.Native.<rid>`
  runtime packages. The managed package now declares dependencies on the RID-specific native packages so
  consuming projects restore the correct binaries automatically. Helper scripts in `scripts/` collect, copy,
  and repackage the native artifacts for CI and local workflows.

## Building the native library

Install the Rust toolchain (Rust 1.86 or newer) before building the managed projects. The `VelloSharp`
MSBuild project now drives `cargo build` for every required native crate (`accesskit_ffi`, `vello_ffi`,
`kurbo_ffi`, `peniko_ffi`, and `winit_ffi`) for the active .NET runtime identifier and configuration.
Running any of the following commands produces the native artifacts and copies them to the managed output
directory under `runtimes/<rid>/native/` (and alongside the binaries for convenience):

```bash
dotnet build bindings/VelloSharp/VelloSharp.csproj
dotnet build samples/AvaloniaVelloWinitDemo/AvaloniaVelloWinitDemo.csproj
dotnet run --project samples/AvaloniaVelloWinitDemo/AvaloniaVelloWinitDemo.csproj
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
   (for example, `dotnet pack bindings/VelloSharp/VelloSharp.csproj -c Release -p:VelloSkipNativeBuild=true -p:VelloNativeAssetsDirectory=$PWD/artifacts/runtimes`).
3. Optionally verify all runtimes by keeping the default `VelloRequireAllNativeAssets=true`, or
   relax the check with `-p:VelloRequireAllNativeAssets=false` when experimenting locally.

The generated `.nupkg` and `.snupkg` files are emitted under `artifacts/nuget/`.

In addition to the aggregate `VelloSharp` package, the repository now produces per-FFI runtime
packages. Each native crate (`AccessKit`, `Kurbo`, `Peniko`, `Vello`, `VelloSparse`, `Winit`) emits:

- `VelloSharp.Native.<Ffi>.<rid>` – a single native asset for the specified RID.
- `VelloSharp.Native.<Ffi>` – a meta package that depends on all supported RIDs for that crate.

When packing the managed package you can toggle native-package dependencies with
`-p:VelloUseNativePackageDependencies=true`; provide a subset via
`-p:VelloNativePackageIds="VelloSharp.Native.Vello;VelloSharp.Native.VelloSparse"` when testing locally.

### Native asset NuGet packages

Per-FFI packaging projects live under `packaging/VelloSharp.Native.<Ffi>/`. Each RID-specific project wraps
the corresponding native library into a standalone NuGet package so downstream applications can pull in only the
assets they need. A typical workflow looks like this:

- Build the native crates for the desired RID(s) (`dotnet build -r osx-arm64 bindings/VelloSharp/VelloSharp.csproj`).
- Run `./scripts/copy-runtimes.sh` to sync the generated artifacts into both sample outputs and each
  `packaging/VelloSharp.Native.<Ffi>/runtimes/<rid>/native` directory.
- Pack the runtime you care about, e.g. `dotnet pack packaging/VelloSharp.Native.Vello/VelloSharp.Native.Vello.osx-arm64.csproj`,
  or pack the meta package via `dotnet pack packaging/VelloSharp.Native.Vello/VelloSharp.Native.Vello.csproj` to create a bundle
  that depends on every RID.
- Reference the RID-specific packages or the meta package from your application depending on how much granularity you need.

The packaging props also emit fallback copies (for example `osx` alongside `osx-arm64`) so that RID roll-forward
continues to work when .NET probes `runtimes/<baseRid>/native`. When only managed assets are required, the sample projects reference the meta packaging projects so the native dylibs land in `bin/<TFM>/runtimes/` without custom MSBuild logic, and the conditional references inside those metas ensure only the available RIDs flow through.

## Available NuGet packages

- `VelloSharp` – managed bindings that surface the renderer, scene graph, wgpu interop, SVG, Velato, and surface
  helpers.
- `VelloSharp.Skia` – Skia-inspired drawing primitives built on top of the Vello renderer.
- `VelloSharp.Integration` – auxiliary helpers for Avalonia, SkiaSharp interop, and host render loops.
- `VelloSharp.WinForms.Core` – Windows Forms drawing shim that maps System.Drawing-friendly APIs onto the Vello renderer.
- `VelloSharp.Integration.WinForms` – WinForms control hosting, swapchain/device management, and GPU/CPU fallback services built on the core shim.
- `VelloSharp.Avalonia.Winit` – Avalonia-facing abstractions for driving the winit surface renderer.
- `VelloSharp.Avalonia.Vello` – Avalonia platform integration that wires Vello surfaces into desktop applications.

### Native runtime packages

- Meta bundles: `VelloSharp.Native.AccessKit`, `VelloSharp.Native.Kurbo`, `VelloSharp.Native.Peniko`,
  `VelloSharp.Native.Vello`, `VelloSharp.Native.VelloSparse`, `VelloSharp.Native.Winit`.
- Per-RID packages follow the pattern `VelloSharp.Native.<Ffi>.<rid>` (for example,
  `VelloSharp.Native.Vello.osx-arm64`, `VelloSharp.Native.VelloSparse.win-x64`).

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

## Windows Forms integration

`VelloSharp.Windows.Core`, `VelloSharp.WinForms.Core`, and `VelloSharp.Integration.WinForms` bring the Vello renderer to Windows Forms.

- `VelloSharp.Windows.Core` centralises HWND-compatible `wgpu` device management, swapchain configuration, staging buffers, and diagnostics for Windows targets.
- `VelloSharp.WinForms.Core` mirrors familiar `System.Drawing` drawing types (`Graphics`, `Pen`, `Brush`, `Region`, `Bitmap`, `Font`, `StringFormat`) on top of Vello scenes recorded through `VelloGraphics` and `VelloGraphicsSession`.
- `VelloSharp.Integration.WinForms` ships the `VelloRenderControl`, shared `WindowsGpuContext` swapchain management, DPI-aware sizing, diagnostics, and automatic CPU fallbacks when the device is lost.

```csharp
using VelloSharp.Windows;
using VelloSharp.WinForms;
using VelloSharp.WinForms.Integration;

var renderControl = new VelloRenderControl
{
    Dock = DockStyle.Fill,
    PreferredBackend = VelloRenderBackend.Gpu,
    DeviceOptions = new VelloGraphicsDeviceOptions
    {
        Format = RenderFormat.Bgra8,
        ColorSpace = WindowsColorSpace.Srgb,
    },
};

renderControl.PaintSurface += (sender, e) =>
{
    var scene = e.Session.Scene;
    var path = new PathBuilder();
    path.MoveTo(32, 32).LineTo(320, 96).LineTo(160, 240).Close();
    scene.FillPath(path, FillRule.NonZero, Matrix3x2.Identity, RgbaColor.FromBytes(0x47, 0x91, 0xF9));
};

Controls.Add(renderControl);
```

Set `PreferredBackend` to `VelloRenderBackend.Cpu` for software rendering, and reuse `DeviceOptions` across controls to tune swapchain format, color space, MSAA, and diagnostics. A single `WindowsGpuContext` instance is shared and reference-counted so multiple controls can share the same `wgpu` device safely.

`samples/WinFormsMotionMarkShim` demonstrates continuous animation, backend switching, and DPI-aware resizing on top of these APIs. `dotnet add package VelloSharp.Integration.WinForms` pulls in both WinForms assemblies (target `net8.0-windows` with `<UseWindowsForms>true</UseWindowsForms>`) and transitively restores the required native runtimes.

## Avalonia integration

Avalonia support is split across three managed packages:

- `VelloSharp.Integration` – reusable controls, render-path helpers, and utility services shared by Avalonia and SkiaSharp hosts.
- `VelloSharp.Avalonia.Winit` – a winit-based windowing backend that plugs into Avalonia's `IWindowingPlatform`, dispatcher, clipboard, and screen services.
- `VelloSharp.Avalonia.Vello` – a Vello-powered rendering backend that implements Avalonia's platform render interfaces on top of `wgpu`.

Opt in to the stack by extending your `AppBuilder`:

```csharp
AppBuilder.Configure<App>()
    .UseWinit()
    .UseVello()
    .WithInterFont();
```

`UseWinit` registers a single-threaded winit event loop, raw handle surfaces, and clipboard/screen implementations for Windows and macOS today, with Wayland/X11 plumbing staged next. Unsupported capabilities such as tray icons, popups, or embedded top levels currently throw `NotSupportedException` so consumers can branch cleanly.

`UseVello` wires Avalonia's composition pipeline to the Vello renderer. It negotiates swapchains via `wgpu`, surfaces the profiler hooks, and falls back to the software `VelloView` path whenever swapchain creation is denied. The renderer shares the same `accesskit_ffi` bridge as the windowing layer, keeping accessibility metadata flowing into screen readers.

### Managed controls

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

### Platform comparison

| Capability | Winit + Vello stack | Built-in Avalonia stack |
| --- | --- | --- |
| Platform coverage | Windows and macOS shipping today, with X11/Wayland support staged; exposes `RawWindowHandle` values for swapchains. | Win32, AppKit, X11, and Wayland backends ship with the framework today. |
| Windowing features | Single dispatcher thread with one top-level window; tray icons, popups, and embedding report `NotSupportedException`. | Mature support for multiple windows, popups, tray icons, and embedding scenarios. |
| Rendering backend | Vello on top of `wgpu` (DX12, Metal, Vulkan, or GLES) with automatic fallbacks to the software path. | Skia renderer with GPU backends (OpenGL, Vulkan, Metal, ANGLE/DirectX) and CPU fallback managed by Avalonia's compositor. |
| Swapchain control | Applications choose surface formats and `PresentMode` via `WgpuSurface` descriptors. | Swapchain setup is internal to Avalonia; apps rely on compositor defaults and the Skia backend configuration. |
| GPU extensibility | Full `wgpu` device/queue access lets you mix custom compute or capture passes with Vello rendering. | GPU access limited to compositor hooks; extending beyond Skia requires custom native backends. |
| Accessibility | AccessKit updates flow through `winit_ffi` so assistive tech stays in sync. | Platform accessibility stacks (UIA/AX/AT-SPI) driven by Avalonia's native backends. |

### Samples and runtime configuration

Run the Avalonia Vello desktop samples to exercise the platform-specific hosting stacks end-to-end:

```bash
dotnet run --project samples/AvaloniaVelloWinitDemo/AvaloniaVelloWinitDemo.csproj
dotnet run --project samples/AvaloniaVelloX11Demo/AvaloniaVelloX11Demo.csproj
dotnet run --project samples/AvaloniaVelloWin32Demo/AvaloniaVelloWin32Demo.csproj
dotnet run --project samples/AvaloniaVelloNativeDemo/AvaloniaVelloNativeDemo.csproj
```

The Avalonia examples catalogue continues to showcase the controls on the stock platforms:

```bash
dotnet run --project samples/AvaloniaVelloExamples/AvaloniaVelloExamples.csproj
```

Both samples copy the native `vello_ffi` library next to the managed binaries automatically; installing the Rust toolchain is the only prerequisite.

## SkiaSharp shim layer

`VelloSharp.Skia` provides a compatibility layer that mirrors the public API of SkiaSharp types while delegating
all rendering to the Vello engine. The shim keeps porting friction low by re-implementing familiar entry points
such as `SKCanvas`, `SKPaint`, `SKPath`, `SKImage`, and `SKTypeface` on top of the managed bindings. Highlights:

- Existing SkiaSharp rendering code can be recompiled by switching `using SkiaSharp;` to `using VelloSharp.Skia;`.
- The shim exposes `SKSurface.Create(VelloSurface surface)` extensions so Vello swapchains or the Avalonia
  `VelloSurfaceView` render directly into SkiaSharp abstractions.
- Text and font services integrate with the same `parley`/`skrifa`/`swash` stack used by the native renderer.
  Call `AppBuilder.UseVelloSkiaTextServices()` when bootstrapping Avalonia to replace Skia text backends with the
  shimmed implementations.
- Recording APIs (`SKPictureRecorder`, `SKPicture`) emit Vello scenes, letting you replay existing Skia display
  lists through the Vello renderer.

Minimal example creating a shim surface and drawing into it:

```csharp
using VelloSharp;
using VelloSharp.Skia;

var info = new SKImageInfo(512, 512, SKImageInfo.PlatformColorType, SKAlphaType.Premul);
using var surface = SKSurface.Create(info);

var canvas = surface.Canvas;
canvas.Clear(new SKColor(0x12, 0x12, 0x14));

using var paint = new SKPaint
{
    Color = new SKColor(0x47, 0x91, 0xF9),
    IsAntialias = true,
};

canvas.DrawCircle(256, 256, 200, paint);

using var renderer = new Renderer((uint)info.Width, (uint)info.Height);
var renderParams = new RenderParams(
    (uint)info.Width,
    (uint)info.Height,
    RgbaColor.FromBytes(0x12, 0x12, 0x14))
{
    Format = RenderFormat.Bgra8,
};

var stride = info.RowBytes;
var pixels = new byte[stride * info.Height];
renderer.Render(surface.Scene, renderParams, pixels, stride);
```

`pixels` now contains BGRA output that you can upload to textures or pass to existing SkiaSharp consumers. For
zero-copy presentation, pair the shim with `VelloSharp.Integration.Skia.SkiaRenderBridge.Render(surface, renderer,
surface.Scene, renderParams)` so Vello writes straight into `SKSurface`/`SKBitmap` instances.

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

- `scripts/build-native-macos.sh [target] [profile] [sdk] [rid]` – builds all FFI crates for macOS/iOS targets
  (Vello, Peniko, Kurbo, AccessKit, Winit). Pass an Apple SDK name (for example `macosx` or `iphoneos`) to
  compile against a specific SDK, and optionally override the runtime identifier. Defaults to
  `x86_64-apple-darwin` in `release` mode.
- `scripts/build-native-linux.sh [target] [profile] [rid]` – cross-compiles the native crates for GNU/Linux
  platforms, defaulting to `x86_64-unknown-linux-gnu`. Supply `aarch64-unknown-linux-gnu` to produce the ARM64
  variant.
- `scripts/build-native-windows.ps1 [target] [profile] [rid]` – a PowerShell helper for the Windows MSVC builds.
  Run from PowerShell or pwsh. Automatically maps the target triple to `win-x64`/`win-arm64` unless a RID is
  provided.
- `scripts/build-native-android.sh [target] [profile] [rid]` – targets Android via the NDK and builds the entire
  FFI set. Requires `ANDROID_NDK_HOME` and adds the toolchain binaries to `PATH` before calling `cargo`.
- `scripts/build-native-wasm.sh [target] [profile] [rid]` – compiles the WebAssembly and static library variants
  for the FFI crates targeting `wasm32-unknown-unknown`.

All build scripts copy the produced library into `artifacts/runtimes/<rid>/native/`, making the payload immediately
available to packaging steps.

### Artifact management and packaging

- `scripts/collect-native-artifacts.sh [source-dir] [dest-dir]` – normalises arbitrary build outputs into the
  `runtimes/<rid>/native/` layout by scanning for `native` folders and copying their contents into the destination.
  Used by CI to gather per-RID outputs before packing.
- `scripts/copy-runtimes.sh [artifacts-dir] [targets…]` / `scripts/copy-runtimes.ps1 [artifactsDir] [targets…]` – copies the assembled runtime folder into project outputs
  and sample applications. The script defaults to propagating assets into `Debug`/`Release` `net8.0` builds for the
  library, integrations, and samples, but you can override the target projects, configurations, or frameworks via
  `COPY_CONFIGURATIONS` / `COPY_TARGET_FRAMEWORKS`.
- `scripts/pack-native-nugets.sh [runtimes-dir] [output-dir]` / `scripts/pack-native-nugets.ps1 [runtimesDir] [outputDir]` – iterates the collected runtimes and packs the
  corresponding `VelloSharp.Native.<rid>` NuGet packages. Each package simply embeds the `native` folder for its RID.
- `scripts/pack-managed-nugets.sh [output-dir] [native-feed]` / `scripts/pack-managed-nugets.ps1 [nugetOutput] [nativeFeed]` – builds the managed projects in `Release`, registers a
  temporary NuGet source pointing at the native packages, and packs the aggregate `VelloSharp` NuGet with
  `VelloUseNativePackageDependencies=true`. Run this after `pack-native-nugets.sh` / `pack-native-nugets.ps1` to produce a
  coherent set of packages under `artifacts/nuget/`.
- `scripts/remove-runtimes.sh [targets…]` / `scripts/remove-runtimes.ps1 [targets…]` – deletes copied runtime folders
  from the default build outputs (or the ones supplied through `REMOVE_RUNTIMES_CONFIGURATIONS` /
  `REMOVE_RUNTIMES_TARGET_FRAMEWORKS`), keeping local trees tidy between packaging runs.

## Repository layout recap

- `ffi/vello_ffi`: Rust source for the native shared library.
- `ffi/*_ffi`: Companion crates exposing AccessKit, Kurbo, Peniko, and Winit bindings consumed by the
  managed layer.
- `VelloSharp`: C# wrapper library with `Scene`, `Renderer`, and path-building helpers.
- `VelloSharp.Integration`: optional Avalonia and Skia helpers with render-path negotiation utilities.
- `samples/AvaloniaVelloWinitDemo`: Avalonia desktop sample that exercises the bindings through the AvaloniaNative/Vello path.
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




