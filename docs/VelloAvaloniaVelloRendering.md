# VelloSharp.Avalonia.Vello Rendering Overview

This note captures how the existing Avalonia-specific Vello binding initializes WebGPU resources, manages the swapchain, drives the render loop, and disposes of GPU state. The goal is to inform extraction of reusable pieces for other bindings (e.g., `bindings/Avalonia.Skia`).

## Device Initialization
- The lifetime of the WebGPU instance/adapter/device/queue is now managed by `WgpuGraphicsDeviceProvider` (`bindings/VelloSharp.Avalonia.Core/Device/WgpuGraphicsDeviceProvider.cs`), which lives in the shared core assembly.
- Vello requests resources by creating `GraphicsDeviceOptions` (carrying presentation data and the resolved `RendererOptions`) and calling `Acquire`, which returns a lease exposing `WgpuDeviceResources`.
- When the resolved renderer options change, the provider recreates the `WgpuInstance`, adapter, device, queue, and `WgpuRenderer`. Disposing the provider walks the renderer → queue → device → adapter → instance chain to release native handles.

## Swapchain & Surface Management
- `VelloSwapchainRenderTarget` (`bindings/VelloSharp.Avalonia.Vello/Rendering/VelloSwapchainRenderTarget.cs`) implements `IRenderTarget2` and manages swapchain surfaces while consuming the shared `WgpuDeviceResources`.
- `EnsureSurface` handles creating/configuring `WgpuSurface` objects per frame:
  - Fetches a platform `SurfaceHandle` via `IVelloWinitSurfaceProvider`.
  - Recreates the surface if the window handle, instance, adapter, or device changes.
  - On macOS the surface creation is deferred to the UI thread (`ScheduleSurfaceCreation`) to honor platform threading rules.
  - Configures the surface (`WgpuSurface.Configure`) with usage, format, size, present mode, alpha mode, and view formats.
  - Normalizes texture formats and tracks whether an extra blit is required for non-RGBA8 surfaces.
- `ResolveRendererOptions` adapts anti-aliasing support for the browser by querying the latest WebGPU capabilities surfaced by `VelloPlatform`.

## Render Loop Flow
- Drawing starts from `CreateDrawingContext`, which produces a `VelloDrawingContextImpl` that records into a `Vello Scene`.
- When the context is disposed, `OnContextCompleted` executes:
  1. Lease the recorded `Scene`.
  2. Resolve renderer options and acquire GPU resources.
  3. Ensure the swapchain surface is ready.
  4. Render via `RenderScene`, which:
     - Acquires the next swapchain texture, adjusts render parameters, and calls `WgpuRenderer.Render`/`RenderSurface`.
     - Invokes any queued `WgpuSurfaceRenderContext` callbacks before presenting the texture.
- Errors tear down the surface and request a redraw while logging debug output.

## Disposal & Resource Reset
- `VelloSwapchainRenderTarget.Dispose` synchronizes destructor work and delegates to `DisposeSurface_NoLock`, which releases the surface, clears cached handles, resets configuration state, and invalidates pending creation requests.
- `SceneLease.Dispose` (via `VelloDrawingContextImpl`) returns the scene to the render target, ensuring per-frame state is cleaned up.
- `VelloPlatform.DisposeSurface_NoLock` is used whenever context loss or configuration mismatches occur; it increments a `requestId` so stale async surface creations are ignored.
- The shared `WgpuGraphicsDeviceProvider` is reused across contexts; disposing it is safe and tears down the cached resources when the platform shuts down.
