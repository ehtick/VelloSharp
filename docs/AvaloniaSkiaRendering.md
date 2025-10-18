# Avalonia.Skia Rendering Overview

## Platform Initialization
- `SkiaPlatform.Initialize` wires Avalonia services to the Skia backend (`extern/Avalonia/src/Skia/Avalonia.Skia/SkiaPlatform.cs:8`). It instantiates `PlatformRenderInterface`, sets it as `IPlatformRenderInterface`, and registers Skia-specific font/text implementations.
- Any alternative backend must either replace this binding step or provide compatible services before Avalonia bootstraps the renderer.

## Render Interface & Context Creation
- `PlatformRenderInterface` is the central factory for render primitives and backend contexts (`extern/Avalonia/src/Skia/Avalonia.Skia/PlatformRenderInterface.cs:20`). It handles geometry/bitmap helpers and determines how Avalonia surfaces map to Skia.
- `CreateBackendContext` inspects the supplied `IPlatformGraphicsContext` and wraps it in an `ISkiaGpu` where possible (`…/PlatformRenderInterface.cs:31`). Supported paths include:
  - Pre-existing `ISkiaGpu` implementations.
  - OpenGL, Metal, or Vulkan contexts promoted to `ISkiaGpu` via `GlSkiaGpu`, `SkiaMetalGpu`, or `VulkanSkiaGpu`.
  - A null context, which yields a CPU-only `SkiaContext`.
- `SkiaContext` (the backend context) owns the optional `ISkiaGpu` and exposes Avalonia render targets (`extern/Avalonia/src/Skia/Avalonia.Skia/SkiaBackendContext.cs:12`). It publishes GPU-specific features for texture sharing or external object interop.

## Surface Selection & Render Target Flow
- `SkiaContext.CreateRenderTarget` iterates the provided surfaces (`…/SkiaBackendContext.cs:44`):
  - If the GPU can create a custom target (`ISkiaGpu.TryCreateRenderTarget`), the path returns a `SkiaGpuRenderTarget` wrapper and stays GPU-backed.
  - Otherwise, it falls back to CPU rendering via `FramebufferRenderTarget` when an `IFramebufferPlatformSurface` is present.
- GPU render targets yield `ISkiaGpuRenderSession`s, which provide `SKSurface`, `GRContext`, and scale factor data for drawing (`extern/Avalonia/src/Skia/Avalonia.Skia/Gpu/SkiaGpuRenderTarget.cs:32`).
- CPU framebuffers lock the platform surface, create/retain `SKSurface` instances, and manage format shims when the framebuffer pixel layout is incompatible (`extern/Avalonia/src/Skia/Avalonia.Skia/FramebufferRenderTarget.cs:22`).

## Drawing Context Lifecycle
- Both GPU and CPU paths funnel into `DrawingContextImpl`, Skia’s `IDrawingContextImpl` implementation (`extern/Avalonia/src/Skia/Avalonia.Skia/DrawingContextImpl.cs:17`). It tracks the Skia canvas/surface, manages render options, and exposes leasing of raw SkiaSharp APIs for advanced interop.
- GPU sessions are disposed alongside the drawing context, ensuring `GRContext` flush/reset semantics are honored (`…/DrawingContextImpl.cs:140`).

## Offscreen & Layer Surfaces
- `SkiaContext.CreateOffscreenRenderTarget` builds `SurfaceRenderTarget` instances for composition layers or render surfaces (`extern/Avalonia/src/Skia/Avalonia.Skia/SkiaBackendContext.cs:64`).
- `SurfaceRenderTarget` optionally reuses GPU surfaces via `ISkiaGpu.TryCreateSurface`; otherwise it creates a Skia raster surface (`extern/Avalonia/src/Skia/Avalonia.Skia/SurfaceRenderTarget.cs:19`). The resulting drawing context mirrors the main flow.

## Extension Points for Vello Integration
- **Custom `ISkiaGpu` implementation:** Supplying a new `IPlatformGraphicsContext` that returns a Vello-backed `ISkiaGpu` (or replacing the Skia GPU factory) would allow the existing Skia plumbing to treat Vello swapchains as “Skia surfaces.” You would need to implement `ISkiaGpuRenderTarget`/`ISkiaGpuRenderSession` to bridge Vello textures to `SKSurface` (or a compatible abstraction).
- **Render-target substitution:** `SkiaContext.CreateRenderTarget` evaluates surfaces sequentially; introducing a `IVelloWinitSurfaceProvider`-style surface that the GPU path recognizes provides an insertion point for a WGPU swapchain.
- **Platform render interface swap:** Replacing `PlatformRenderInterface` with a composite that delegates geometry/bitmap work to Skia but routes drawing contexts through the shared Vello pipeline could minimize duplication, aligning with the extraction goals.
- **Feature propagation:** Any integration must preserve feature exposure (texture-sharing, external object leases) by populating `PublicFeatures` in `SkiaContext` so upstream Avalonia components continue to function.

These entry points highlight where the Skia binding currently couples to SkiaSharp primitives and where a shared Vello/WGPU abstraction would need to plug in.
