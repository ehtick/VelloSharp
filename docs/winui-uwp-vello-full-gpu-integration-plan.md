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
- `src/VelloSharp.Windows.Shared` – shared Windows host infrastructure targeting `net8.0-windows10.0.17763`, providing presenters, diagnostics, and surface abstractions consumed by Uno, WinUI, MAUI, and forthcoming UWP work.
- `bindings/VelloSharp.WinUI` – WinUI 3 control library (`net8.0-windows10.0.19041`) that exposes `VelloSwapChainControl` backed by the shared presenter.
- `bindings/VelloSharp.Uwp` – preview UWP/WinUI hybrid control library (`net8.0-windows10.0.19041`) delivering `VelloSwapChainPanel` today, with an upcoming `uap10.0.19041` target planned for store packaging.
- `samples/WinUIVelloGallery` & `samples/UwpVelloGallery` – reference apps showcasing animated rendering, diagnostics overlays, and backend switching via the new controls.
- `docs/guides/winui-vello-getting-started.md` and `docs/guides/uwp-vello-getting-started.md` (preview) plus DocFX navigation updates describing setup, packaging, and troubleshooting.
- Updated CI pipelines to build/test WinUI/UWP bindings, publish DocFX docs, and validate native asset deployment for Store/MSIX scenarios.

## Workstreams

### Shared Rendering Infrastructure
- [x] Audit `bindings/VelloSharp.Uno` and `bindings/VelloSharp.Windows.Core` to extract shared presenter contracts (`IVelloSwapChainPresenterHost`, diagnostics, AccessKit bridge) into `src/VelloSharp.Windows.Shared`.
- [x] Introduce a Windows dispatcher abstraction (`IVelloWindowsDispatcher`) so Uno/WinUI/UWP/Maui can map to it without platform-specific `CoreDispatcher` references.
- [x] Consolidate render loop configuration (`RenderLoopDriver`, frame pacing) into the shared project and expose common bindable properties/events for all Windows XAML hosts.
- [x] Add unit/integration tests under `tests/VelloSharp.Windows.Shared.Tests` covering lease lifetime, dispatcher marshaling, and diagnostic toggles.

### WinUI Control Implementation
- [x] Create `bindings/VelloSharp.WinUI/VelloSwapChainControl.cs` deriving from `Microsoft.UI.Xaml.Controls.SwapChainPanel`, embedding the shared presenter and supporting dependency properties for backend selection, diagnostics, and render loop configuration.
- [x] Implement a composition-based control variant (`VelloCompositionControl`) that leverages `ElementCompositionPreview` where SwapChainPanel is not ideal (transparent overlays, clipping).
- [x] Wire native asset loading using the shared loader, ensuring unpackaged and packaged WinUI apps resolve runtimes (`AppContext.BaseDirectory\runtimes`, MSIX installed path).
- [x] Integrate AccessKit and pointer routing with WinUI event sources, reusing the shared dispatcher abstraction.
- [x] Provide extension methods for app startup (`IHostBuilder`) mirroring WPF/Uno registration patterns for dependency injection scenarios.

### UWP Control Implementation
- [x] Port the WinUI control logic into `bindings/VelloSharp.Uwp`, swapping Windows App SDK APIs for UWP equivalents (`Windows.UI.Xaml.Controls.SwapChainPanel`, `CoreDispatcher`).
- [x] Ensure native asset deployment works with UWP packaging (`ContentGroupMap`, `PRI` manifests), updating `.targets`/`.props` to copy runtimes into `AppX` layout.
- [x] Implement capability declarations and restricted API usage (e.g., `rescap` for D3D12) with fallback to WARP when restricted.
- [ ] Add AccessKit/automation integration that complies with UWP automation peers.

### GPU Runtime & Native Packaging
- [x] Update `packaging/VelloSharp.Native.Vello/build/VelloSharp.Native.Vello.targets` to include WinUI/UWP runtime asset flows and mark MSIX/AppX metadata as needed.
- [x] Add RID-specific verification scripts (`scripts/verify-winui-native-assets.ps1`, `scripts/verify-uwp-native-assets.ps1`) ensuring all binaries ship and are properly signed.
- [x] Extend `bindings/VelloSharp/NativeLibraryLoader.cs` with WinUI/UWP probing (AppData, `Package.Current.InstalledLocation`) and align with MAUI/Uno logic.
- [x] Confirm GPU backend binaries (D3D12, Vulkan, WGPU) ship for x64/arm64 and add WARP fallback toggles matching WPF/WinForms hosts.
- [x] Coordinate with docfx/nuget packaging to ensure new assets are included in `buildTransitive` outputs and central package metadata.

### Samples & Tooling
- [x] Scaffold `samples/WinUIVelloGallery` using Windows App SDK templates with scenes covering text, simple animation, diagnostics overlay toggles, and backend selection UI hooks.
- [x] Scaffold `samples/UwpVelloGallery` targeting Windows App SDK preview builds (future UWP packaging) mirroring the WinUI gallery structure.
- [x] Add smoke tests or scripted launchers to validate sample startup and GPU backend selection (`scripts/run-winui-gallery.ps1`, `scripts/run-uwp-gallery.ps1`).
- [x] Update sample solution files and documentation to include the new projects in the root `VelloSharp.sln`.

### Documentation & DocFX
- [x] Author `docs/guides/winui-vello-getting-started.md` and `docs/guides/uwp-vello-getting-started.md` covering setup, control usage, backend configuration, and troubleshooting (preview).
- [x] Add API conceptual docs under `docs/docfx/articles` for WinUI/UWP controls, including code snippets and sample screenshots.
- [ ] Update `docs/ffi-api-coverage.md` with WinUI/UWP coverage rows and GPU backend status.
- [x] Amend DocFX `toc.yml` (under `docs/docfx`) to surface new guides, and ensure build pipelines validate the docset.
- [ ] Refresh root `README.md` and `docs/ci-release-process.md` sections referencing supported platforms and release steps.

### CI, Validation & QA
- [x] Extend GitHub Actions/Azure Pipelines to restore Windows App SDK workloads, build WinUI/UWP bindings, run unit tests, and package AppX/MSIX artifacts (`.github/workflows/build-pack.yml`).
- [x] Add automated UI smoke tests utilizing WinAppDriver or `Microsoft.UI.Xaml.Hosting` integration tests for WinUI; for UWP, use `AppContainer` smoke tests or ctest wrappers (`tests/VelloSharp.Windows.UiSmokeTests`).
- [x] Integrate native asset verification scripts into CI to prevent missing binaries across arches (`verify-winui-native-assets.ps1`, `verify-uwp-native-assets.ps1` in pipeline).
- [x] Document manual validation matrices (Desktop, Xbox, Surface Hub, ARM64 devices) in `docs/logs/winui-uwp-validation.md`.

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
