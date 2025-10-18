# Vello vs. Skia Configuration Gaps

This note compares the configuration and lifecycle expectations of the existing Vello and Skia Avalonia bindings. The gaps highlight what needs to be normalized when extracting a shared integration layer.

## Surface & Handle Abstractions
- **Vello:** Rendering depends on `IVelloWinitSurfaceProvider` (`bindings/VelloSharp.Avalonia.Winit/Platform/IVelloWinitSurfaceProvider.cs:9`) to translate Avalonia to native `SurfaceHandle`s recognised by WebGPU. Platform-specific providers wrap `INativePlatformHandleSurface` to manufacture Win32 (`bindings/VelloSharp.Avalonia.Vello/Rendering/Win32SurfaceProvider.cs:15`), X11 (`…/Rendering/X11SurfaceProvider.cs:15`), or AppKit handles (`…/Rendering/AvaloniaNativeSurfaceProvider.cs:16`). Surface creation/disposal is tightly coupled with the WebGPU swapchain (`VelloSwapchainRenderTarget.cs:153`).
- **Skia:** The platform render interface consumes Avalonia’s `IPlatformGraphicsContext`/`IFramebufferPlatformSurface` abstractions. `PlatformRenderInterface.CreateBackendContext` elevates platform contexts into `ISkiaGpu` implementations for OpenGL/Metal/Vulkan or falls back to CPU framebuffers (`extern/Avalonia/src/Skia/Avalonia.Skia/PlatformRenderInterface.cs:31`). No explicit surface handle objects are exposed; everything ultimately resolves to `SKSurface` instances (`SurfaceRenderTarget.cs:21`).
- **Normalization target:** Introduce a surface descriptor contract that can produce either a WebGPU `SurfaceHandle` or an `ISkiaGpuRenderTarget` without duplicating native handle discovery. This likely means wrapping Avalonia’s `INativePlatformHandleSurface` once and sharing it with both pipelines.

## Renderer Options & Feature Flags
- **Vello:** `VelloPlatformOptions` governs clear color, present mode, FPS, and `RendererOptions` that encode CPU fallback plus MSAA support (`bindings/VelloSharp.Avalonia.Vello/VelloPlatformOptions.cs:8`). Runtime capability probing adjusts MSAA availability in the swapchain (`VelloSwapchainRenderTarget.cs:348`). Additional WebGPU capability logs surface through `VelloPlatform` when running in-browser (`VelloPlatform.cs:70`).
- **Skia:** `SkiaOptions` exposes memory-budget tuning and an opacity save-layer toggle (`extern/Avalonia/src/Skia/Avalonia.Skia/SkiaOptions.cs:9`). GPU feature negotiation happens through `ISkiaGpu.TryGetFeature` and is published for texture sharing/external object support (`SkiaBackendContext.cs:20`).
- **Normalization target:** Define a unified renderer configuration (e.g., `GraphicsBackendOptions`) that captures presentation mode, MSAA, resource budgets, and feature toggles, then translate to the backend-specific structures.

## Threading & Lifecycle Constraints
- **Vello:** macOS requires swapchain creation on the UI thread, so `VelloSwapchainRenderTarget` defers surface construction via `Dispatcher.UIThread.Post` and tracks pending descriptors (`bindings/VelloSharp.Avalonia.Vello/Rendering/VelloSwapchainRenderTarget.cs:439`). Surface providers use Avalonia’s dispatcher to query handles/pixel sizes and to schedule redraws (`AvaloniaNativeSurfaceProvider.cs:41`, `Win32SurfaceProvider.cs:33`). The WebGPU resource stack is guarded with locks (`VelloGraphicsDevice.cs:17`).
- **Skia:** GPU contexts are created synchronously; `DrawingContextImpl` locks the `GRContext` to guard threaded access (`extern/Avalonia/src/Skia/Avalonia.Skia/DrawingContextImpl.cs:131`). `FramebufferRenderTarget` performs framebuffer locks per frame and manages shim surfaces when pixel formats differ (`…/FramebufferRenderTarget.cs:22`). No UI-thread affinity is enforced by default.
- **Normalization target:** The shared layer must define where surface creation is allowed (UI vs render thread) and provide async hooks so both backends respect platform requirements. Resource locking semantics (e.g., `Monitor.Enter` vs dispatcher posts) should be encapsulated.

## Feature Exposure & Interop Hooks
- **Vello:** Exposes WGPU surface callbacks for additional command encoding (`WgpuSurfaceRenderContext.cs:5`) and tracks WebGPU capabilities via `VelloPlatform.LatestWebGpuCapabilities` (`VelloPlatform.cs:59`).
- **Skia:** Publishes backend features (`IOpenGlTextureSharingRenderInterfaceContextFeature`, `IExternalObjectsRenderInterfaceContextFeature`) through `SkiaContext.PublicFeatures` (`SkiaBackendContext.cs:20`) and offers SkiaSharp leasing (`DrawingContextImpl.cs:102`).
- **Normalization target:** A shared feature registry should map high-level capabilities (texture sharing, raw command access) onto whichever backend is active so Avalonia callers do not lose functionality when switching renderers.

These differences inform the shape of the shared abstraction layer needed to drive both Vello (WebGPU) and Skia pipelines from the same Avalonia integration point.
