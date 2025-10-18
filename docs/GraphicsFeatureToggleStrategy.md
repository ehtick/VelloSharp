# Graphics Feature Toggle Strategy

## Objectives
- Provide a single configuration surface that Avalonia hosts can use to toggle GPU features regardless of the active backend.
- Preserve upstream project layouts: `Avalonia.Skia` stays linked to Avalonia’s sources, while new feature plumbing lives in the shared layer.
- Enable runtime probing (e.g., WebGPU validation layers availability) without duplicating capability logic in each binding.

## Unified Options
The previously proposed `GraphicsFeatureSet` (see `docs/SharedGraphicsAbstractionProposal.md:17`) captures the canonical toggles:

```csharp
public sealed record GraphicsFeatureSet(
    bool EnableCpuFallback,
    bool EnableMsaa8,
    bool EnableMsaa16,
    bool EnableAreaAa,
    bool EnableOpacityLayers,
    long? MaxGpuResourceBytes);
```

Additional flags—such as WebGPU validation layers or shader debugging—can be appended as optional booleans without breaking binary compatibility thanks to C# record positional parameters with defaults.

## Mapping to Vello
- `EnableCpuFallback` → `RendererOptions.UseCpu` in `VelloPlatformOptions` (`bindings/VelloSharp.Avalonia.Vello/VelloPlatformOptions.cs:17`).
- `EnableMsaa8` / `EnableMsaa16` / `EnableAreaAa` → `RendererOptions.SupportMsaa8`, `SupportMsaa16`, `SupportArea`, with runtime capability refinement already handled inside `VelloSwapchainRenderTarget.ResolveRendererOptions` (`bindings/VelloSharp.Avalonia.Vello/Rendering/VelloSwapchainRenderTarget.cs:348`).
- `EnableOpacityLayers` → informs the Avalonia composition pipeline whether to pre-flatten opacity clips or rely on the backend (Vello currently uses layer blends; flag becomes advisory).
- `MaxGpuResourceBytes` → unused (Vello ignores it) but retained for parity so shared UI does not branch.
- **Validation layers:** add `bool EnableValidationLayers` to `GraphicsFeatureSet`. The Vello adapter maps it to `WgpuDeviceDescriptor` flags and stores the choice alongside the cached device in `VelloGraphicsDevice` (`bindings/VelloSharp.Avalonia.Vello/Rendering/VelloGraphicsDevice.cs:33`). Capability fallbacks remain inside Vello.

## Mapping to Skia
- `EnableCpuFallback` toggles whether to request GPU contexts from `ISkiaGpu`. When false and no GPU is available, the manager falls back to `FramebufferRenderTarget` (`extern/Avalonia/src/Skia/Avalonia.Skia/SkiaBackendContext.cs:44`).
- `EnableMsaa8` / `EnableMsaa16` inform render target creation: `SkiaGpuRenderTarget.BeginRenderingSession` already supports MSAA via Skia surfaces; the shared layer decides sample count when constructing the `SKSurface`.
- `EnableAreaAa` has no direct Skia equivalent; the bridge can expose it via paint configuration when constructing `DrawingContextImpl`.
- `EnableOpacityLayers` maps to `SkiaOptions.UseOpacitySaveLayer` (`extern/Avalonia/src/Skia/Avalonia.Skia/SkiaOptions.cs:18`).
- `MaxGpuResourceBytes` maps to `SkiaOptions.MaxGpuResourceSizeBytes`.
- `EnableValidationLayers` becomes a no-op (Skia lacks an equivalent). The bridge records the choice so serialization remains symmetrical but ignores it when configuring Skia.

## Exposure to Consumers
1. Introduce a shared `GraphicsBackendOptions` type that aggregates `GraphicsFeatureSet`, `GraphicsPresentationOptions`, and backend selection. This object is registered with Avalonia’s service locator during application bootstrap.
2. Provide an Avalonia `IOptions<GraphicsBackendOptions>` binding (or static helper) so `VelloPlatform.Initialize` and the Skia adapter both pull configuration from the same source.
3. Offer a simple extension API (e.g., `UseVelloBackend(options => ...)`) that clones the defaults and lets callers tweak flags without referencing backend-specific structures.

## Validation & Telemetry
- The shared layer reports the effective feature set (after capability probing) via diagnostics events so UI toggles can reflect downgraded settings (e.g., MSAA disabled on unsupported hardware).
- When `EnableValidationLayers` is set, Vello attaches `WebGpuRuntime.LogMessage` (`bindings/VelloSharp.Avalonia.Vello/VelloPlatform.cs:93`). For Skia, the flag triggers additional logging around context loss if desired.

This strategy gives both bindings a consistent set of switches while allowing each backend to interpret flags according to its capabilities. The shared layer acts as the adapter that maps the abstract toggles onto concrete options without altering the upstream source files.
