# MAUI Vello Full GPU Integration Plan

## Vision and Goals
- Deliver first-party `VelloSharp.Maui` controls that light up Vello/WGPU rendering on Windows, MacCatalyst, iOS, and Android with feature parity to the already shipping WinUI host.
- Keep the programming model consistent with existing hosts (`VelloSurfaceView` for Avalonia, `VelloSwapChainPanel` for WinUI/Uno) so surface presenters, diagnostics, and lease APIs stay reusable.
- Ensure native GPU runtimes (`wgpu`, `vello`, `accesskit`, `harfbuzz`, etc.) deploy and load automatically on every MAUI target using improved resolver logic and packaging inspired by the SkiaSharp submodule.
- Ship documentation, samples, and validation assets so contributors can extend MAUI coverage without reverse engineering the Windows path.

## Existing Foundations to Reuse
- `bindings/VelloSharp.Integration/Avalonia/VelloSurfaceView.cs` and `bindings/VelloSharp.Uno/Controls/VelloSwapChainPresenter.cs` already encapsulate lease lifetimes, diagnostics hooks, and `RenderLoopDriver` wiring; MAUI presenters should wrap these helpers instead of duplicating logic.
- `bindings/VelloSharp.Windows.Core/RenderLoopDriver.cs` and related enums/patterns (used by WPF/Uno hosts) provide a common render loop contract that can be surfaced as MAUI bindable properties.
- `docs/avalonia-vello-renderer-plan.md` and `docs/avalonia-winit-platform-plan.md` describe the swapchain and feature negotiation story across platforms; reuse the same abstraction layers when binding MAUI heads.
- `extern/SkiaSharp/source/SkiaSharp.Views.Maui` demonstrates how multi-targeted MAUI handlers, registration extensions, and packaging are structured; mirror that layout for VelloSharp to reduce risk.
- Existing native packaging projects under `packaging/VelloSharp.Native.*` already emit RID-scoped runtimes; extend them rather than spinning new packaging pipelines.

## Cross-Platform Requirements
- **GPU & surface stack** – Promote the same `VelloGraphicsDeviceOptions`, `VelloRenderMode`, and swapchain orchestration used in WinUI/Uno so diagnostics and renderer selection remain uniform. Prefer deriving platform-specific surface adapters from the existing `IVelloSwapChainPresenterHost`.
- **MAUI handler shape** – Provide a single `VelloView` (derives `Microsoft.Maui.Controls.View`) with a mapper/handler pair per platform similar to `SKCanvasViewHandler`. Expose bindable properties for render loop selection, backend preferences, and diagnostics taps that map directly to the shared presenter.
- **Threading & lifecycle** – Respect MAUI’s dispatcher rules (UI thread affinity) while delegating background rendering to the shared presenter when safe. Align suspend/resume hooks with MAUI lifecycle events (`OnPause`, `OnResume`, `WillEnterForeground`, etc.).
- **Native runtime packaging** – Ensure each RID publishes the right native binaries and that `bindings/VelloSharp/NativeLibraryLoader.cs` can probe MAUI asset locations (especially Android/Apple bundle paths). Adopt SkiaSharp’s pattern of embedding RID assets in `.targets` files to copy into the final app bundle.
- **Diagnostics & tooling** – Maintain compatibility with the existing `WindowsGpuDiagnostics`/`VelloDiagnosticsOverlay` tooling so GPU information surfaces uniformly across hosts.

## Architecture Overview
- `src/VelloSharp.Maui.Core` – new multi-targeted library (net8.0/net9.0 for `android`, `ios`, `maccatalyst`, `windows10.0.19041`) containing shared view models, bindable properties, and abstractions for presenters and diagnostics.
- `bindings/VelloSharp.Maui` – platform-specific handler implementations that host the shared presenter against native surfaces (`UIView`/`MTKView`, `AndroidSurfaceView`, WinUI `SwapChainPanel` reusing `VelloSwapChainPresenter`).
- `samples/MauiVelloGallery` – end-to-end MAUI sample showcasing composition, text, input, and diagnostics across all heads, mirroring the structure of `samples/AvaloniaVelloExamples`.
- Packaging updates under `packaging/VelloSharp.Native.*` to add Android/Apple runtime outputs, plus `.targets`/`.props` that wire the files into MAUI build pipelines.
- Documentation updates (`docs/guides`, root `README.md`) explaining setup, platform caveats, and runtime deployment.

## Phase 0 – Shared Infrastructure & Handler Scaffolding
**Objectives** – Establish the cross-platform MAUI surface, reuse existing presenters, and wire up handler registration without platform-specific rendering yet.

**Deliverables**
- [ ] Create `src/VelloSharp.Maui.Core/VelloSharp.Maui.Core.csproj` with `$(MauiTargetFrameworks)` support, referencing `Microsoft.Maui.Core`, `bindings/VelloSharp.Windows.Core`, and shared integration helpers.
- [ ] Introduce `VelloView` (controls + bindable properties) describing lease callbacks, render loop selection, backend preference, and diagnostics events; implement partial classes per platform to attach native presenters.
- [ ] Factor a `MauiVelloPresenterAdapter` that wraps `VelloSwapChainPresenter` so MAUI handlers can drive the existing swapchain lifecycle without duplicating renderer logic.
- [ ] Add `UseVelloSharp()` MAUI extension mirroring `extern/SkiaSharp/source/SkiaSharp.Views.Maui/SkiaSharp.Views.Maui.Controls/AppHostBuilderExtensions.cs`, registering handlers and optional image sources.
- [ ] Wire analyzer, nullable, and private API props consistent with other bindings (reuse `Directory.Packages.props` entries and Avalonia pattern for unstable APIs).

## Phase 1 – Apple (MacCatalyst & iOS) GPU Hosts
**Objectives** – Provide Metal-backed surfaces and lifecycle management for Apple heads, sharing as much code as possible via partials.

**Deliverables**
- [ ] Implement `VelloViewHandler.MacCatalyst` hosting a `UIView` with a `CAMetalLayer` (or `MTKView`) that exposes an `ICoreAnimationMetalLayerSurfaceSource` to the shared presenter; handle orientation/scale via `UIScreen.MainScreen.Scale`.
- [ ] Extend the handler to support `OnWindowChanged`, `LayoutSubviews`, and `TraitCollectionDidChange` so swapchains resize with the MAUI layout system.
- [ ] Implement `VelloViewHandler.iOS` reusing the same Metal view but integrating with MAUI lifecycle events (`OnAppearing`/`OnDisappearing`) to stop/resume rendering and releasing GPU resources during backgrounding.
- [ ] Bridge MAUI touch events to the shared composition input pipeline (reuse gesture conversion logic from `bindings/VelloSharp.Integration/Avalonia/CompositionInputSource.cs` where applicable).
- [ ] Validate native runtime discovery by loading `libvello.dylib`, `libwgpu_native.dylib`, etc., through `NativeLibraryLoader` with bundle-relative probing paths; add integration tests that exercise metal device enumeration on simulator + device.

## Phase 2 – Android GPU Host
**Objectives** – Provide a Vulkan-backed host (`SurfaceView`/`TextureView`) that cooperates with MAUI’s lifecycle and resumes cleanly.

**Deliverables**
- [ ] Implement `VelloViewHandler.Android` wrapping a custom `VelloSurfaceView` derived from `SurfaceView` + `ISurfaceHolderCallback`, creating an `AndroidSurfaceSource` for `VelloSwapChainPresenter`.
- [ ] Handle lifecycle events (`OnAttachedToWindow`, `OnDetachedFromWindow`, `OnPause`, `OnResume`) to tear down or recreate the swapchain, mirroring patterns in `bindings/VelloSharp.Uno/Controls/VelloSwapChainPanel.cs`.
- [ ] Support fallback to `TextureView` on devices where `SurfaceView` conflicts with MAUI layouts; expose a bindable `UseTextureView` flag.
- [ ] Integrate Android-specific diagnostics (GPU vendor, Vulkan feature set) into the existing diagnostics overlay.
- [ ] Extend packaging to include `runtimes/android-arm64/native/*.so` and register them via `NativeLibraryLoader.RegisterRuntimeIdentifierProbing("android")`.

## Phase 3 – Windows (WinUI) MAUI Host Alignment
**Objectives** – Reuse existing WinUI GPU integration for MAUI’s Windows head without breaking current WinUI consumers.

**Deliverables**
- [ ] Implement `VelloViewHandler.WinUI` by composing the already shipping `VelloSwapChainPanel` (from `bindings/VelloSharp.Uno/Controls/VelloSwapChainPanel.cs`) inside the MAUI handler, ensuring input/diagnostics hooks flow through.
- [ ] Provide opt-in flags to disable MAUI’s default `GraphicsView` pipeline to avoid conflicts with WinUI composition.
- [ ] Add regression tests ensuring MAUI Windows head can coexist with existing WinUI apps referencing `VelloSharp.Windows.Core`.
- [ ] Document migration guidance for teams currently embedding WinUI controls directly so they can move to MAUI without behavioural regressions.

## Phase 4 – Samples, Tooling, and Validation Suites
**Objectives** – Prove the integration end-to-end with sample content, automated smoke tests, and instrumentation.

**Deliverables**
- [ ] Create `samples/MauiVelloGallery` with shared XAML pages demonstrating scenes, text shaping, composition overlays, and diagnostics toggles; reuse assets from `samples/AvaloniaVelloExamples`.
- [ ] Author platform-specific MAUI heads (Android, iOS, MacCatalyst, Windows) using `UseVelloSharp()` and ensure they build/run via `dotnet build`/`dotnet publish -t:Run`.
- [ ] Add automated smoke tests using `Microsoft.Maui.TestUtils.DeviceTests` to launch each head, validate swapchain creation, and exercise pause/resume sequences.
- [ ] Instrument GPU timing/diagnostic output in the sample and wire it into CI artifact capture (frames, logs) similar to `artifacts/samples/skiasharp`.

## Native Runtime Loading Enhancements
**Objectives** – Harden native library discovery across MAUI bundle layouts, reusing lessons from SkiaSharp and existing Vello packaging.

**Deliverables**
- [ ] Audit `bindings/VelloSharp/NativeLibraryLoader.cs` and extend it with platform-specific probing paths for MAUI (Android `AppContext.BaseDirectory/lib`, iOS/MacCatalyst `NSBundle.MainBundle`, Windows `AppContext.BaseDirectory/runtimes`).
- [ ] Introduce MAUI-friendly `.targets` that copy required native assets into `$(IntermediateOutputPath)\runtimes` and mark them for bundle inclusion, mirroring `extern/SkiaSharp/native/*` build outputs.
- [ ] Consolidate native asset manifests (e.g., `packaging/VelloSharp.Native.Vello/build/VelloSharp.Native.Vello.targets`) so each RID is declared once and reused by MAUI + WinUI consumers.
- [ ] Add verification scripts/tests that inspect the published MAUI app bundle to confirm native binaries exist and are signed/not stripped (leverage patterns from `extern/SkiaSharp/native/android` and `extern/SkiaSharp/native/maccatalyst`).
- [ ] Document best practices in `docs/guides/native-library-loader.md` for MAUI-specific deployment, including troubleshooting steps and environment variables.

## Validation & Documentation
- [ ] Extend CI to run `dotnet workload restore maui` and build all MAUI heads at least in Release configuration; gate merges on successful builds.
- [ ] Add MAUI coverage rows to `docs/ffi-api-coverage.md` and `README.md`, indicating GPU backend support and platform caveats.
- [ ] Publish developer guide (`docs/maui-vello-getting-started.md`) detailing setup, handler usage, dependency injection, and lifecycle considerations.
- [ ] Record manual validation checklist (device matrix: Windows, Surface Duo/Android, iPad/iOS, Mac) and track in `STATUS.md`.

## Exit Criteria
- All checkboxes above are complete and verified on physical hardware or emulators for each MAUI target.
- `samples/MauiVelloGallery` renders Vello scenes with GPU acceleration and diagnostics parity on Windows, MacCatalyst, iOS, and Android.
- Native runtime deployment produces zero `DllNotFoundException`/`EntryPointNotFoundException` across targets, validated by automated tests.
- Documentation and sample projects provide a clear migration path for teams currently using the WinUI/Avalonia hosts.
- No regressions introduced to existing WinUI, Avalonia, or Uno integrations (validated via smoke tests/samples).
