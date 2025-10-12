# WinUI & UWP Vello Full GPU Integration Plan

## Vision and Goals
- Deliver first-party `VelloSharp.WinUI` and `VelloSharp.Uwp` controls with parity to the existing WPF, WinForms, MAUI, and Uno experiences, running fully on GPU via the shared Vello/WGPU stack.
- Unify Windows presenter infrastructure so Uno, WinUI 3, UWP, and MAUI reuse the same swapchain, diagnostics, and lifetime management components instead of duplicating logic.
- Ensure native runtime deployment, diagnostics overlays, and developer tooling (including DocFX docs and samples) stay consistent across all Windows desktop/mobile targets.
- Ship sample applications and validation assets that demonstrate WinUI/UWP usage patterns, including swapchain integration, input, accessibility, and diagnostics, to unblock downstream adopters.

## Existing Foundations to Reuse
- `bindings/VelloSharp.Windows.Core` render loop drivers, swapchain presenters, and diagnostics hooks already power WPF/WinForms/Uno; extend these abstractions rather than creating new Windows-only paths.
- `bindings/VelloSharp.Uno/Controls/VelloSwapChainPresenter.cs` encapsulates lease management and Uno/WinUI interop; refactor it into a shared presenter surface (`VelloSwapChainPresenterHost`) consumable by WinUI 3 and UWP handlers.
- MAUI/Uno efforts introduced packaging, native library probing, and diagnostics overlays that can be refactored into shared services for all Windows XAML stacks.
- Existing packaging projects under `packaging/VelloSharp.Native.*` produce Windows runtimes (dx12, vulkan, wgpu, accesskit); extend manifests to cover WinUI/UWP asset flows instead of creating new packages.
- DocFX, NuGet publishing, and CI scripts used by other bindings (MAUI, WPF) already encode release and documentation steps; mirror their structure to maintain a consistent developer experience.

## Target Platforms & Requirements
- **WinUI 3 (Windows App SDK)** – Support Desktop (Win32) and unpackaged MSIX scenarios, including SwapChainPanel and Composition rendering hosts, with fallback paths when GPU devices are unavailable.
- **UWP (Windows 10 19041+)** – Enable Store-compatible control that works on desktop, Xbox, and Surface Hub SKUs; respect restricted API surface and packaging rules.
- **Shared Presenter Infrastructure** – Abstract lease management, diagnostics, and render loop configuration so Uno, WinUI, and MAUI can share presenter implementations via multi-targeted libraries.
- **GPU Backend Coverage** – Provide configurable D3D12, Vulkan (via WARP fallback where appropriate), and future WGPU backends, matching WPF/WinForms options and exposing diagnostics overlays.
- **Accessibility & Input** – Preserve AccessKit integration and pointer/keyboard routing already available in Uno/WinForms hosts, adapting them to the WinUI/UWP dispatcher requirements.

## Architecture Overview
- `src/VelloSharp.Windows.Shared` – new shared project (multi-targeted net6.0-windows10.0.19041/net8.0-windows10.0.19041) containing presenter hosts, diagnostics services, and platform abstractions shared by Uno, WinUI, MAUI, and UWP.
- `bindings/VelloSharp.WinUI` – new WinUI-specific control library targeting `net8.0-windows10.0.19041` (Windows App SDK 1.5+) exposing `VelloSwapChainControl` and optional `VelloCompositionControl`.
- `bindings/VelloSharp.Uwp` – new UWP control library targeting `uap10.0.19041` with XAML controls and toolkit helpers, multi-configured to share the presenter host.
- `samples/WinUIVelloGallery` & `samples/UwpVelloGallery` – apps demonstrating composition, text, input, diagnostics, and native asset validation, mirroring the Avalonia/Maui gallery structure.
- `docs/guides/winui-uwp-getting-started.md` plus DocFX navigation updates describing setup, packaging, and troubleshooting.
- Updated CI pipelines to build/test WinUI/UWP samples, publish DocFX docs, and validate native asset deployment for Store/MSIX scenarios.

## Workstreams

### Shared Rendering Infrastructure
- [ ] Audit `bindings/VelloSharp.Uno` and `bindings/VelloSharp.Windows.Core` to extract shared presenter contracts (`IVelloSwapChainPresenterHost`, diagnostics, AccessKit bridge) into `src/VelloSharp.Windows.Shared`.
- [ ] Introduce a Windows dispatcher abstraction (`IVelloWindowsDispatcher`) so Uno/WinUI/UWP/Maui can map to it without platform-specific `CoreDispatcher` references.
- [ ] Consolidate render loop configuration (`RenderLoopDriver`, frame pacing) into the shared project and expose common bindable properties/events for all Windows XAML hosts.
- [ ] Add unit/integration tests under `tests/VelloSharp.Windows.Shared.Tests` covering lease lifetime, dispatcher marshaling, and diagnostic toggles.

### WinUI Control Implementation
- [ ] Create `bindings/VelloSharp.WinUI/VelloSwapChainControl.cs` deriving from `Microsoft.UI.Xaml.Controls.Grid`, embedding the shared presenter and supporting dependency properties for backend selection, frame rate, diagnostics overlays, and input.
- [ ] Implement a composition-based control variant (`VelloCompositionControl`) that leverages `ElementCompositionPreview` where SwapChainPanel is not ideal (transparent overlays, clipping).
- [ ] Wire native asset loading using the shared loader, ensuring unpackaged and packaged WinUI apps resolve runtimes (`AppContext.BaseDirectory\runtimes`, MSIX installed path).
- [ ] Integrate AccessKit and pointer routing with WinUI event sources, reusing the shared dispatcher abstraction.
- [ ] Provide extension methods for app startup (`IHostBuilder`) mirroring WPF/Uno registration patterns for dependency injection scenarios.

### UWP Control Implementation
- [ ] Port the WinUI control logic into `bindings/VelloSharp.Uwp`, swapping Windows App SDK APIs for UWP equivalents (`Windows.UI.Xaml.Controls.SwapChainPanel`, `CoreDispatcher`).
- [ ] Ensure native asset deployment works with UWP packaging (`ContentGroupMap`, `PRI` manifests), updating `.targets`/`.props` to copy runtimes into `AppX` layout.
- [ ] Implement capability declarations and restricted API usage (e.g., `rescap` for D3D12) with fallback to WARP when restricted.
- [ ] Add AccessKit/automation integration that complies with UWP automation peers.

### GPU Runtime & Native Packaging
- [ ] Update `packaging/VelloSharp.Native.Vello/build/VelloSharp.Native.Vello.targets` to include WinUI/UWP runtime asset flows and mark MSIX/AppX metadata as needed.
- [ ] Add RID-specific verification scripts (`scripts/verify-winui-native-assets.ps1`, `scripts/verify-uwp-native-assets.ps1`) ensuring all binaries ship and are properly signed.
- [ ] Extend `bindings/VelloSharp/NativeLibraryLoader.cs` with WinUI/UWP probing (AppData, `Package.Current.InstalledLocation`) and align with MAUI/Uno logic.
- [ ] Confirm GPU backend binaries (D3D12, Vulkan, WGPU) ship for x64/arm64 and add WARP fallback toggles matching WPF/WinForms hosts.
- [ ] Coordinate with docfx/nuget packaging to ensure new assets are included in `buildTransitive` outputs and central package metadata.

### Samples & Tooling
- [ ] Scaffold `samples/WinUIVelloGallery` using Windows App SDK templates with scenes covering text, shapes, animation, diagnostics overlay toggles, and backend selection UI.
- [ ] Scaffold `samples/UwpVelloGallery` targeting UWP (x64, x86, arm64) with similar scenarios and instrumentation for `DeviceInformation`.
- [ ] Add smoke tests or scripted launchers to validate sample startup and GPU backend selection (`scripts/run-winui-gallery.ps1`, `scripts/run-uwp-gallery.ps1`).
- [ ] Update sample solution files and documentation to include the new projects in the root `VelloSharp.sln`.

### Documentation & DocFX
- [ ] Author `docs/guides/winui-uwp-getting-started.md` covering setup, control usage, backend configuration, and troubleshooting.
- [ ] Add API conceptual docs under `docs/docfx/articles` for WinUI/UWP controls, including code snippets and sample screenshots.
- [ ] Update `docs/ffi-api-coverage.md` with WinUI/UWP coverage rows and GPU backend status.
- [ ] Amend DocFX `toc.yml` (under `docs/docfx`) to surface new guides, and ensure build pipelines validate the docset.
- [ ] Refresh root `README.md` and `docs/ci-release-process.md` sections referencing supported platforms and release steps.

### CI, Validation & QA
- [ ] Extend GitHub Actions/Azure Pipelines to restore Windows App SDK workloads, build WinUI/UWP bindings, run unit tests, and package AppX/MSIX artifacts.
- [ ] Add automated UI smoke tests utilizing WinAppDriver or `Microsoft.UI.Xaml.Hosting` integration tests for WinUI; for UWP, use `AppContainer` smoke tests or ctest wrappers.
- [ ] Integrate native asset verification scripts into CI to prevent missing binaries across arches.
- [ ] Document manual validation matrices (Desktop, Xbox, Surface Hub, ARM64 devices) in `docs/logs/winui-uwp-validation.md`.

### Release & NuGet Publishing
- [ ] Create or update NuGet packages (`VelloSharp.WinUI`, `VelloSharp.Uwp`, `VelloSharp.Windows.Shared`) with appropriate `nuspec` metadata, dependency chains, and icon/licenses.
- [ ] Ensure packaging scripts (`scripts/publish-nuget.ps1`, `scripts/package-all.ps1`) include new projects and produce symbol/source packages.
- [ ] Update release notes templates and DocFX changelog entries to highlight WinUI/UWP support.
- [ ] Verify signing/strong naming policies align with Store/MSIX requirements.

## Exit Criteria
- WinUI and UWP controls are implemented, share presenter infrastructure with Uno/Maui, and render Vello scenes with GPU acceleration (D3D12/Vulkan) across supported architectures.
- Sample galleries for WinUI and UWP build and run in CI, showcasing feature parity and diagnostics overlays matching other Windows hosts.
- Native assets deploy without `DllNotFoundException` in packaged/unpackaged scenarios, validated by automated scripts and manual smoke tests.
- Documentation (DocFX + guides) and release tooling are updated, and new NuGet packages publish successfully alongside existing Windows bindings.
- No regressions are introduced to existing Uno, MAUI, WPF, or WinForms integrations; shared presenter tests cover cross-host lifetimes and dispatchers.
