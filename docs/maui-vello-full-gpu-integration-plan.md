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

## Current Status
- Cross-platform presenters now use real GPU surfaces on Windows, Android, and Apple heads with shared pointer/focus routing via `MauiCompositionInputSource`.
- Android ships a `UseTextureView` fallback and extended diagnostics surfaced in the gallery overlay; Metal presenter renders through `MTKView` but lifecycle suspend/resume work is still pending.
- Native library loader includes MAUI-specific probing plus `scripts/verify-maui-native-assets.ps1`, and RID assets flow through the `buildTransitive` target during MAUI builds.
- CI/bootstrap scripts run `dotnet workload restore maui`, and the Windows-only leg (`build-samples.*`) now builds `samples/MauiVelloGallery` targeting `net8.0-windows10.0.19041`.
- Automated Android/iOS/MacCatalyst builds and Apple native runtime validation are still outstanding before declaring the integration complete.

## Phase 0 – Shared Infrastructure & Handler Scaffolding
**Objectives** – Establish the cross-platform MAUI surface, reuse existing presenters, and wire up handler registration without platform-specific rendering yet.

**Deliverables**
- [x] Create `src/VelloSharp.Maui.Core/VelloSharp.Maui.Core.csproj` with `$(MauiTargetFrameworks)` support, referencing MAUI base packages and shared integration helpers.
- [x] Introduce `VelloView` (controls + bindable properties) describing lease callbacks, render loop selection, backend preference, and diagnostics events; implement partial classes per platform to attach native presenters.
- [x] Factor a `MauiVelloPresenterAdapter` that wraps platform presenters so MAUI handlers can drive the existing swapchain lifecycle without duplicating renderer logic (Windows host implemented, Metal/Vulkan adapters tracked under Phase 1/2).
- [x] Add `UseVelloSharp()` MAUI extension mirroring `extern/SkiaSharp/source/SkiaSharp.Views.Maui/SkiaSharp.Views.Maui.Controls/AppHostBuilderExtensions.cs`, registering handlers and optional image sources.
- [x] Wire analyzer, nullable, and private API props consistent with other bindings (reuse `Directory.Packages.props` entries and Avalonia pattern for unstable APIs).

## Phase 1 – Apple (MacCatalyst & iOS) GPU Hosts
**Objectives** – Provide Metal-backed surfaces and lifecycle management for Apple heads, sharing as much code as possible via partials.

**Deliverables**
- [x] Implement `VelloViewHandler.MacCatalyst` hosting a `UIView` with a `CAMetalLayer` (or `MTKView`) that exposes an `ICoreAnimationMetalLayerSurfaceSource` to the shared presenter; now wire it to the real WGPU surface via the new FFI handle support, handling orientation/scale via `UIScreen.MainScreen.Scale`.
- [ ] Extend the handler to support `OnWindowChanged`, `LayoutSubviews`, and `TraitCollectionDidChange` so swapchains resize with the MAUI layout system.
  - `MauiMetalView` forwards `LayoutSubviews`, but window and trait collection hooks still need wiring for orientation and scene changes.
- [ ] Implement `VelloViewHandler.iOS` reusing the same Metal view but integrating with MAUI lifecycle events (`OnAppearing`/`OnDisappearing`) to stop/resume rendering and releasing GPU resources during backgrounding, now binding directly to the WGPU surface via the exposed FFI handles.
  - Rendering flows through `MauiVelloMetalPresenter`, yet MAUI lifecycle suspend/resume and background resource teardown remain TODO.
- [x] Bridge MAUI touch events to the shared composition input pipeline (reuse gesture conversion logic from `bindings/VelloSharp.Integration/Avalonia/CompositionInputSource.cs` where applicable).
- [ ] Validate native runtime discovery by loading `libvello.dylib`, `libwgpu_native.dylib`, etc., through `NativeLibraryLoader` with bundle-relative probing paths; add integration tests that exercise metal device enumeration on simulator + device.
- [x] **Done:** Rust FFI exposes `CAMetalLayer` handles so the Metal presenter can hand surfaces to WGPU directly.

## Phase 2 – Android GPU Host
**Objectives** – Provide a Vulkan-backed host (`SurfaceView`/`TextureView`) that cooperates with MAUI’s lifecycle and resumes cleanly.

- [x] Implement `VelloViewHandler.Android` wrapping a custom `SurfaceView` + `ISurfaceHolderCallback`, creating an Android-backed Vulkan presenter now wired through the new FFI `ANativeWindow` handle support.
- [ ] Handle lifecycle events (`OnAttachedToWindow`, `OnDetachedFromWindow`, `OnPause`, `OnResume`) to tear down or recreate the swapchain, mirroring patterns in `bindings/VelloSharp.Uno/Controls/VelloSwapChainPanel.cs`.
  - Surface/texture callbacks cover attach/detach, but MAUI activity lifecycle (`OnPause`/`OnResume`) still needs to drive presenter `Suspend`/`Resume`.
- [x] Support fallback to `TextureView` on devices where `SurfaceView` conflicts with MAUI layouts; expose a bindable `UseTextureView` flag.
- [x] Integrate Android-specific diagnostics (GPU vendor, Vulkan feature set) into the existing diagnostics overlay.
- [x] Extend packaging to include `runtimes/android-arm64/native/*.so` and register them via `NativeLibraryLoader.RegisterRuntimeIdentifierProbing("android")`.
- [x] **Done:** Rust FFI exposes Android `ANativeWindow` handles so the Vulkan presenter can drive WGPU surfaces without placeholder shims.

## Next Steps

### Finish the Windows handler alignment (Phase 3)
- Land the remaining `VelloViewHandler.WinUI` work so the MAUI handler simply wraps `VelloSwapChainPanel`, forwards diagnostics/input, and exposes the same bindable settings already available to Avalonia/Uno hosts.
- Introduce a MAUI opt-in to suppress the default `GraphicsView` Skia compositor when Vello is active, and verify the opt-out via the WinUI visual tree inspector.
- Expand regression tests around the shared presenter (`bindings/VelloSharp.Uno`) so WinUI-first consumers and MAUI WinUI heads exercise identical code paths.
- Capture migration notes (handler registration, property parity, diagnostics toggles) and stage them for documentation under the [Phase 3 deliverables](#phase-3--windows-winui-maui-host-alignment).

### Harden native runtime probing and packaging
- [x] Extend `bindings/VelloSharp/NativeLibraryLoader.cs` with MAUI-aware search paths (Android `lib/`, Apple bundle roots, Windows RID subfolders) and add targeted unit coverage that simulates those layouts.
  - Loader now probes Android ABI folders, iOS/MacCatalyst `Frameworks`/`MonoBundle`, and exposes test hooks to validate the logic (`NativeLibraryLoaderMauiTests`).
- [x] Mirror SkiaSharp’s MAUI packaging pattern by adding RID-aware `.targets/.props` so native blobs flow into `$(IntermediateOutputPath)\runtimes` and ultimately into the MAUI app bundle.
  - `VelloSharp.Maui.Core` ships a `buildTransitive` target that copies native assets from the RID packages into the MAUI intermediate output and registers them via `AndroidNativeLibrary`/`IOSNativeLibrary`/`MacCatalystNativeLibrary`.
- [x] Script a publish verification step that inspects produced bundles for the expected dylib/so/dll payloads and signature/bitness, then document troubleshooting guidance in `docs/guides/native-library-loader.md`.
  - Added `scripts/verify-maui-native-assets.ps1` to check published bundles for platform-specific binaries and updated the native loader guide with MAUI-specific guidance.
- [x] Track these changes against the [Native Runtime Loading Enhancements](#native-runtime-loading-enhancements) checklist.

### Restore MAUI workloads and unblock automated builds
- [x] Update local/CI bootstrap scripts to run `dotnet workload restore maui` before any MAUI build, caching manifests where possible to reduce build leg latency.
  - `bootstrap-windows.ps1` and `bootstrap-macos.sh` now call `dotnet workload restore maui` after installing the .NET SDK.
- [x] Once workloads restore, wire a Windows-only `dotnet build samples/MauiVelloGallery/MauiVelloGallery.csproj -f net8.0-windows10.0.19041` validation leg and capture artifacts for manual review.
  - `build-samples.*` automatically target the Windows MAUI framework when invoked on Windows hosts so the gallery is exercised during CI and local validation.
- [x] Document the workload prerequisites (VS components, environment variables, cache paths) directly in `scripts/` readme files so contributors can mirror CI setup.
- [x] Use the [Workload & Build Setup](#workload--build-setup) checklist to track completion.

### Expand diagnostics, validation, and documentation
- [x] Add runtime discovery tests that verify Metal dylib resolution and Android Vulkan availability using the new probing hooks, and surface failures through the diagnostics overlay.
  - Added `NativeLibraryLoaderMauiTests` to exercise the new probing paths for Android and MacCatalyst.
- [x] Ensure keyboard/text routing regression coverage exists for the MAUI handler (especially Windows focus handoff) and add CI smoke tests via `Microsoft.Maui.TestUtils.DeviceTests`.
  - `MauiCompositionInputSourceTests` (device-test category) validate focus hand-off and `RequestFocus` behaviour against a WinUI host using the shared `MauiCompositionInputSource`.
- [x] Update high-level docs (`README.md`, `docs/maui-vello-getting-started.md`, `STATUS.md`) with platform caveats, walkthroughs, and validation matrices once the above tests pass.
- [x] Sync these tasks with the [Validation & Documentation](#validation--documentation) section so exit criteria remain measurable.

## Phase 3 – Windows (WinUI) MAUI Host Alignment
**Objectives** – Reuse existing WinUI GPU integration for MAUI’s Windows head without breaking current WinUI consumers.

**Deliverables**
- [x] Implement `VelloViewHandler.WinUI` by composing the already shipping `VelloSwapChainPanel` (from `bindings/VelloSharp.Uno/Controls/VelloSwapChainPanel.cs`) inside the MAUI handler, ensuring input/diagnostics hooks flow through.
  - Handler mapper now forwards `SuppressGraphicsViewCompositor`, `IsDiagnosticsEnabled`, backend/render loop updates, and input hooks to the shared presenter so MAUI mirrors WinUI behaviour.
- [x] Provide opt-in flags to disable MAUI’s default `GraphicsView` pipeline to avoid conflicts with WinUI composition.
  - Introduced `VelloView.SuppressGraphicsViewCompositor` (default `false`) which toggles the panel’s new `SuppressGraphicsViewCompositor` dependency property, preserving Uno defaults while letting MAUI disable the Skia compositor on demand.
- [x] Add regression tests ensuring MAUI Windows head can coexist with existing WinUI apps referencing `VelloSharp.Windows.Core`.
  - Added `TestSwapChainPresenterHost` under `bindings/VelloSharp.Uno` plus unit tests in `tests/VelloSharp.Windows.Core.Tests` that assert Skia opt-out and lifecycle paths remain identical when driven from MAUI or WinUI heads.
- [ ] Document migration guidance for teams currently embedding WinUI controls directly so they can move to MAUI without behavioural regressions.

## Phase 4 – Samples, Tooling, and Validation Suites
**Objectives** – Prove the integration end-to-end with sample content, automated smoke tests, and instrumentation.

**Deliverables**
- [x] Create `samples/MauiVelloGallery` with shared XAML pages demonstrating scenes, text shaping, composition overlays, and diagnostics toggles; reuse assets from `samples/AvaloniaVelloExamples`.
- [ ] Author platform-specific MAUI heads (Android, iOS, MacCatalyst, Windows) using `UseVelloSharp()` and ensure they build/run via `dotnet build`/`dotnet publish -t:Run` (currently waiting on MAUI workloads).
- [ ] Add automated smoke tests using `Microsoft.Maui.TestUtils.DeviceTests` to launch each head, validate swapchain creation, and exercise pause/resume sequences (blocked until MAUI workloads are restored on CI agents).
- [ ] Instrument GPU timing/diagnostic output in the sample and wire it into CI artifact capture (frames, logs) similar to `artifacts/samples/skiasharp`.

## Native Runtime Loading Enhancements
**Objectives** – Harden native library discovery across MAUI bundle layouts, reusing lessons from SkiaSharp and existing Vello packaging.

**Deliverables**
- [x] Audit `bindings/VelloSharp/NativeLibraryLoader.cs` and extend it with platform-specific probing paths for MAUI (Android `AppContext.BaseDirectory/lib`, iOS/MacCatalyst `NSBundle.MainBundle`, Windows `AppContext.BaseDirectory/runtimes`).
- [x] Introduce MAUI-friendly `.targets` that copy required native assets into `$(IntermediateOutputPath)\runtimes` and mark them for bundle inclusion, mirroring `extern/SkiaSharp/native/*` build outputs.
- [ ] Consolidate native asset manifests (e.g., `packaging/VelloSharp.Native.Vello/build/VelloSharp.Native.Vello.targets`) so each RID is declared once and reused by MAUI + WinUI consumers.
- [x] Add verification scripts/tests that inspect the published MAUI app bundle to confirm native binaries exist and are signed/not stripped (leverage patterns from `extern/SkiaSharp/native/android` and `extern/SkiaSharp/native/maccatalyst`).
- [x] Document best practices in `docs/guides/native-library-loader.md` for MAUI-specific deployment, including troubleshooting steps and environment variables.

### Workload & Build Setup
- [x] Run `dotnet workload restore maui` once per machine/CI agent before building any MAUI heads (bootstrap scripts invoke this ahead of sample builds).
- [x] Validate the Windows path via `dotnet build samples/MauiVelloGallery/MauiVelloGallery.csproj -f net8.0-windows10.0.19041` (now covered by `scripts/build-samples.*` on Windows hosts).
- [x] Wire the MAUI workloads into CI (`dotnet workload restore maui`) and add a Windows-only build leg for `samples/MauiVelloGallery` while Metal/Vulkan presenters are still blocked (currently enabled via the updated sample build leg).

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
