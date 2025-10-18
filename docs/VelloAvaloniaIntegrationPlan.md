# Vello/Avalonia Integration Plan

## Goals
- Create a reusable Vello/WGPU integration layer consumable by both `bindings/VelloSharp.Avalonia.Vello` and `bindings/Avalonia.Skia`.
- Enable `samples/ControlCatalog` (and the .NET Core variant) to exercise the complete Vello rendering pipeline in Avalonia with minimal duplication and clear extension points.
- Preserve upstream compatibility: keep the original `Avalonia.Skia` sources unmodified, introducing custom hooks only via additional files/links so the upstream layout remains intact.

## Milestones & Tasks

1. ☐ **Audit Existing Integrations**
   - ☑ Review `bindings/VelloSharp.Avalonia.Vello` to document device creation, swapchain setup, render loop, and disposal patterns ([notes](docs/VelloAvaloniaVelloRendering.md)).
   - ☑ Inspect `bindings/Avalonia.Skia` to identify current Skia-specific paths and entry points for injecting a Vello-backed renderer ([notes](docs/AvaloniaSkiaRendering.md)).
   - ☑ Capture configuration differences (platform handles, feature flags, threading requirements) that must be normalized ([notes](docs/VelloSkiaConfigurationGaps.md)).

2. ☐ **Define Shared Abstractions**
   - ☑ Propose interfaces/records for device initialization, surface/swapchain ownership, and render submission lifecycle ([proposal](docs/SharedGraphicsAbstractionProposal.md)).
   - ☑ Decide how to expose feature toggles (e.g., CPU fallback, validation layers) for both bindings ([strategy](docs/GraphicsFeatureToggleStrategy.md)).
   - ☑ Align naming, namespaces, and dependency graph to avoid circular references when adding a shared module ([plan](docs/SharedModuleDependencyPlan.md)).

3. ☐ **Extract Common Core**
   - ☑ Create a shared assembly (e.g., `bindings/VelloSharp.Avalonia.Core`) hosting the new abstractions and helper utilities while remaining Skia-free (see `bindings/VelloSharp.Avalonia.Core`).
   - ☑ Move duplicated code from `VelloSharp.Avalonia.Vello` into the shared assembly while preserving existing behavior through thin adapters (`WgpuGraphicsDeviceProvider`, `WgpuDeviceResources`, shared surface providers, and the updated Vello adapters in `bindings/VelloSharp.Avalonia.Vello`).
   - ☑ Add unit or smoke tests validating device initialization in the shared code (`tests/VelloSharp.Avalonia.Core.Tests/WgpuGraphicsDeviceProviderTests.cs`).

4. ☐ **Implement Avalonia.Skia Integration**
   - ☑ Scaffold a thin Skia bridge assembly (`bindings/VelloSharp.Avalonia.SkiaBridge`) to expose Skia-specific contexts without altering upstream sources.
   - ☑ Replace or augment the current Skia path in `bindings/Avalonia.Skia` to optionally initialize the Vello pipeline via the shared core (`SkiaVelloRenderer.Initialize` wrapping `VelloRenderer.Initialize`).
   - ☐ Ensure render loop interop (frame scheduling, resize, DPI changes) matches Avalonia expectations when Vello is active.
   - ☑ Maintain backward-compatible Skia-only behavior via configuration switches (see `SkiaBackendConfiguration` and `SkiaBackendInitializer` in the Skia bridge).

5. ☐ **Update ControlCatalog Samples**
   - ☑ Wire both `samples/ControlCatalog` and `samples/ControlCatalog.NetCore` to opt into the shared Vello pipeline (via `RenderingConfiguration` and `SkiaBackendInitializer`).
   - ☐ Expose runtime toggles or build flags to choose between Skia and Vello rendering paths for comparative testing.
   - ☐ Validate startup, navigation, and control rendering using the new pipeline on major desktop targets (Windows, macOS, Linux).

6. ☐ **Validation & Documentation**
   - ☐ Produce a manual test checklist covering window lifecycle, resizing, and GPU capability fallbacks.
   - ☐ Document prerequisites, configuration steps, and troubleshooting for enabling Vello in Avalonia projects.
   - ☐ Profile rendering performance and track any regressions compared to the existing Skia pipeline.
