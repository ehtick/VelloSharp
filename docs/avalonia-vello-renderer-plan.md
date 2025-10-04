# Avalonia Vello Renderer Implementation Plan

## Background
- The repository already exposes Vello and WGPU bindings (`VelloSharp`, `VelloSharp.Integration`) and provides managed helpers for render contexts, surfaces, and scenes.
- An Avalonia windowing backend using winit is in progress (`VelloSharp.Avalonia.Winit`), but rendering currently relies on existing Avalonia render pipelines (Skia, etc.).
- Avalonia's Skia backend (in `Avalonia.Skia` inside the Avalonia repo) demonstrates how to implement `IRenderLoop`, `IWindowingPlatform`, `IPlatformRenderInterface`, and scene composition integration.
- Goal is to provide a first-class Avalonia rendering platform powered by Vello/WGPU, pair it with the winit windowing backend, and deliver a sample that exercises both subsystems.

## Goals
- Create a new project (e.g., `VelloSharp.Avalonia.Vello`) that implements Avalonia's rendering interfaces using Vello.
- Provide a `UseVello()` `AppBuilder` extension to register the renderer, mirroring how Avalonia's Skia backend wires up services.
- Integrate with `VelloSharp.Avalonia.Winit` to supply compatible rendering surfaces (`INativePlatformHandleSurface`, swapchain management, render loop, etc.).
- Surface GPU device/context creation via WGPU, matching the native winit sample capabilities (swapchain creation, frame submission, resize handling).
- Implement render target management, scene invalidation, and drawing using Vello's renderer to drive Avalonia composition visuals.
- Update MSBuild props to allow private Avalonia API usage (mirroring Consolonia settings) for the new project and sample.
- Deliver a sample app that uses `.UseWinit().UseVello()` to render Avalonia content via the new pipeline.
- Update docs/readme with status and usage instructions.

## Non-Goals & Constraints
- Focus on desktop platforms (Win32, X11/Wayland, macOS) supported by winit/Vello; mobile/browser deferred.
- No requirement to implement advanced features like render-to-texture or custom shader effects initially.
- Reuse existing VelloSharp scene building helpers where possible; do not rewrite scene graph logic.
- Keep initial implementation single-window; multi-window swapchains can follow later.

## High-Level Architecture
1. **Renderer bootstrap**: Provide `VelloApplicationExtensions.UseVello()` that registers a `VelloPlatformRenderInterface`, `VelloRenderLoop`, `VelloCompositor`, and related services within `AvaloniaLocator`.
2. **Device management**: Build a `VelloGraphicsDevice` wrapper that owns `wgpu::Device`, `Queue`, and swapchain configuration per window. Manage surface creation via handles exposed by `WinitWindowImpl`.
3. **Render loop & timing**: Implement `VelloRenderTimer` (or reuse Avalonia's default) to coordinate frame rendering, hooking into Avalonia's `IPlatformRenderInterfaceContext` and `IRenderLoop` contracts.
4. **Surface & swapchain**: For each window, construct a `VelloSwapchainSurface` that implements `IPlatformRenderTarget`/`IRenderTargetBitmapImpl` equivalents, binding to the native surface handle and resizing on demand.
5. **Scene rendering**: Implement `VelloCompositionRenderer` (inspired by `SkiaCompositionRenderer`) that traverses Avalonia visuals, builds a Vello scene (`SceneBuilder`), and submits frames via `Renderer.Render` into the swapchain texture.
6. **Integration with winit windowing**: Extend `WinitWindowImpl.TryGetFeature` to expose the Vello surface handle feature required by the renderer, ensuring the swapchain uses the correct window handle and DPI scaling.
7. **Resource lifetime & resize**: Handle window resize events to recreate swapchains and adjust render scaling; integrate with `WinitDispatcher` for device idle/teardown on shutdown.
8. **Sample application**: Update or add `samples/AvaloniaWinitDemo` (or new sample) to call `.UseWinit().UseVello()`, present rich visuals verifying the pipeline.
9. **Documentation**: Describe the new renderer, usage pattern, and known limitations in `docs/avalonia-winit-platform-plan.md` (status update) or a new doc.

## Implementation Steps
1. **Project scaffolding**
   - Create `VelloSharp.Avalonia.Vello` project with necessary references (`Avalonia`, `VelloSharp`, `VelloSharp.Integration`).
   - Add MSBuild props enabling private API access, nullable, analyzers, etc., similar to `VelloSharp.Avalonia.Winit` and Consolonia.
   - Register project in solution (`VelloSharp.sln`) and central package props.

2. **Core renderer services**
   - Implement `VelloApplicationExtensions.UseVello()` and `VelloPlatform` static initializer to register Avalonia services.
   - Provide `VelloRenderInterface` implementing `IPlatformRenderInterface`, returning custom render targets, bitmaps, glyph runs (initial stub minimal support necessary for composition rendering).
   - Implement a render loop/timer (`VelloRenderTimer`) leveraging Avalonia's `IRenderLoop` or reuse default render loop while ensuring GPU submission occurs on UI thread or background thread as needed.

3. **Device and surface management**
   - Build `VelloGraphicsDevice` to encapsulate WGPU adapter/device creation; allow configuration via options (preferred backend, vsync, etc.).
   - Implement `VelloSwapchainSurface` (per window) to manage swapchain creation using `wgpu::Surface`, handle resizing, acquire/release textures, and expose `IPlatformRenderInterfaceContext` for Avalonia.
   - Hook into `WinitWindowImpl` to provide required native handles (raw window handle, size, scale) to the renderer.

4. **Rendering pipeline**
   - Implement `VelloCompositionRenderer` to translate Avalonia's `IDrawingContextImpl` operations into Vello scene updates. Consider starting by adapting existing Vello integration (e.g., `VelloView`) to convert Avalonia draw commands or render via `CompositionTarget` surfaces.
   - Ensure frame submission writes to swapchain textures and presents via WGPU queue.
   - Integrate clear color, clip, and transform handling consistent with Avalonia expectations.

5. **Integration with winit backend**
   - Extend `WinitPlatform.Initialize` to optionally attach the Vello renderer (or ensure renderer can locate window surfaces via service locator).
   - Ensure `WinitWindowImpl.TryGetFeature` can return objects the renderer expects (`INativePlatformHandleSurface`, custom `IVelloSurfaceSource`, etc.).
   - Wire resize and redraw events from `WinitWindowImpl` to trigger swapchain updates and rendering.

6. **Sample updates**
   - Update `samples/AvaloniaWinitDemo` to consume the new renderer, or create `samples/AvaloniaWinitVelloDemo` showcasing GPU rendering.
   - Provide usage instructions in README/docs and ensure project references the new renderer package.

7. **Validation & polishing**
   - Run `dotnet build VelloSharp.sln` (and any relevant cargo builds) to ensure managed and native layers compile.
   - Manual run instructions: `dotnet run --project samples/AvaloniaWinitDemo/AvaloniaWinitDemo.csproj`.
   - Update documentation (`docs/ffi-api-coverage.md`, `docs/avalonia-winit-platform-plan.md`) with new renderer status and TODOs.

## Incremental Milestones (current status)
- [x] Expose Vello-compatible surface handles and dispatcher hooks from the winit window backend.
- [x] Re-introduce/complete Vello geometry converters so Avalonia geometries translate into Vello paths.
- [x] Implement a limited Vello drawing context that maps core draw commands (rectangles, paths, glyph stubs) into `VelloSharp.Scene` updates.
- [x] Build swapchain management: create/resize WGPU surfaces per window, render scenes via `WgpuRenderer`, and present frames.
- [x] Integrate the swapchain path with Avalonia compositor/render loop so redraws flow through the dispatcher.
- [ ] Update the sample to call `.UseWinit().UseVello()`, exercise the pipeline end-to-end, and document setup/testing steps.

## Testing & Verification
- `dotnet build VelloSharp.sln`
- Manual run of the Avalonia sample with `.UseWinit().UseVello()` to confirm GPU rendering and resume/resizing behavior.
- Optionally add smoke test verifying renderer initialization (future CI scope).

## Open Questions / Follow-ups
- How to map Avalonia's drawing primitives most efficiently onto Vello? Initial version may need to limit features (e.g., text, effects).
- Resource caching (glyphs, images) may require follow-up work; initial approach can stub or fallback to CPU paths.
- Consider multi-threaded rendering and frame pacing improvements in subsequent iterations.
- MSAA currently falls back to area sampling unless the renderer options explicitly enable the corresponding shader support.
