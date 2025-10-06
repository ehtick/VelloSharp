# Uno Binding Implementation Plan

## Findings Snapshot
- Uno Platform ships multiple Windows heads (WinAppSDK/WinUI 3, UWP, Win32 via XAML Islands) that all expose XAML visuals; WinAppSDK provides `ISwapChainPanelNative` while legacy heads provide HWND surfaces, making it feasible to leverage the existing `VelloSharp.Windows.Core` abstractions.
- `VelloSharp.Windows.Core` already factors WGPU device creation, swapchain lifecycle, leasing, and diagnostics for WinForms/WPF; the remaining gaps are the surface descriptors (SwapChainPanel/CoreWindow) and Uno dispatcher abstractions.
- Uno's Skia-based heads (Windows, macOS, Linux) render through a Skia compositor; full-speed WGPU needs a dedicated native surface path that can coexist with Skia or temporarily suspend it, similar to the HWND swapchain story already validated for WinForms/WPF.
- Uno uses `DispatcherQueue`/`CoreDispatcher` semantics instead of WPF's `Dispatcher`; render loops must plug into Uno's `IDispatcherQueueTimer` or composition clocks without blocking UI threads.

## Phase 1 - Platform Baseline & Packaging
- [x] Audit Uno heads we must support first (WinAppSDK/WinUI, UWP, Win32 XAML Islands) and confirm their minimum OS/SDK requirements for WGPU swapchain descriptors. → see `docs/uno-phase1-baseline.md` for the build matrix.
- [x] Add a `VelloSharp.Uno` multi-targeted project (`net8.0-windows10.0.19041`, `net8.0-windows10.0.17763`, `net8.0`) referencing `VelloSharp.Windows.Core` and sharing the same source link/package metadata as WinForms/WPF.
- [x] Verify wgpu-native surface descriptors for `SwapChainPanel`, `CoreWindow`, and HWND are available in the pinned wgpu commit; schedule an upstream bump if the Uno target matrix needs newer APIs. Evidence captured in `docs/uno-phase1-baseline.md`.
- [x] Stand up an Uno WinAppSDK sample app that references the new project and exercises the SwapChainPanel bridge to validate packaging and XAML registration (`samples/VelloSharp.Uno.WinAppSdkSample`).

## Phase 2 - Windows Surface Descriptor Bridge
- [x] Implement `UnoSwapChainPanelSurfaceSource : IWindowsSurfaceSource` that captures `ISwapChainPanelNative` handles, produces WGPU `SurfaceDescriptorFromSwapChainPanel`, and feeds size changes through the shared `WindowsSurfaceFactory` (`bindings/VelloSharp.Uno/Interop/UnoSwapChainPanelSurfaceSource.cs`).
- [x] Add `UnoCoreWindowSurfaceSource` for UWP/CoreWindow targets, reusing the existing HWND plumbing for resize/device-loss detection where possible.
- [x] Extend `WindowsSwapChainSurface` to accept a `WindowsSurfaceKind` enum (HWND, SwapChainPanel, CoreWindow) so reuse stays explicit and telemetry can discriminate between surface types.
- [x] Provide a compatibility shim for Uno's Win32 (WPF/XAML Islands) head, delegating to the `VelloNativeSwapChainView` path from the WPF plan to avoid duplication.

- [x] Create `VelloSwapChainPanel` (WinUI 3) and `VelloCoreWindowHost` (UWP) custom controls deriving from `FrameworkElement`, each internally instantiating the appropriate surface source and sharing a `VelloSwapChainPresenter` helper across heads. *(WinUI/UWP controls now live in `bindings/VelloSharp.Uno/Controls`, backed by the shared presenter.)*
- [x] Wire `Loaded`/`Unloaded` events to acquire and release `WindowsGpuContextLease` instances, mirroring the WPF composition view lifecycle but using `FrameworkElement.Unloaded` sequencing.
- [x] Mirror the WPF dependency property surface (`RenderMode`, `PreferredBackend`, `Diagnostics`, `IsContinuousRendering`, `ContentInvalidated`) so XAML bindings stay consistent across WinForms/WPF/Uno.
- [x] Integrate Uno's `ElementCompositionPreview.GetElementVisual` path to ensure the control opts out of Skia compositor participation when WGPU owns the swapchain, preventing redundant Skia draws.

## Phase 4 - Render Loop & Threading
- [x] Implement a `UnoRenderLoopDriver` that wraps `DispatcherQueueTimer` for continuous mode and `CompositionTarget.Rendering` (WinUI 3) or `CoreIndependentInputSource` ticks for high-frequency scenarios; align with the shared `RenderLoopDriver` contract from WPF where possible.
- [x] Ensure render callbacks execute on a background thread pool when possible, only marshaling to UI threads for surface commits, to match the WinForms/WPF high-performance paths.
- [x] Add throttling for zero-diff frames by checking `WindowsGpuDiagnostics` delta metrics before enqueuing redraws, preventing busy loops when Uno throttles the pump.
- [x] Validate suspend/resume behaviour (window hidden, app minimised, tab deactivated) and hook into Uno's lifecycle events to pause the render loop without tearing.

## Phase 5 - Diagnostics, Tooling, and Design-Time
- [x] Surface `Diagnostics` via Uno `DependencyProperty` metadata and expose a lightweight `IVelloDiagnosticsProvider` service for consumption by MVVM view-models.
- [ ] Integrate Uno-enabled Event Tracing (ETW) hooks or logging providers so GPU device-loss, swapchain resets, and keyed-mutex contention are visible through Uno's logging pipeline.
- [x] Provide a design-time safe fallback that renders a placeholder visual without initialising WGPU, guarding against Hot Reload/Live Visual Tree issues in Visual Studio and Uno Workstation. *(SwapChainPanel now displays a placeholder overlay; CoreWindow host skips GPU startup in design-mode scenarios.)*
- [ ] Author XAML theme resources/styles so the new controls follow Uno's Fluent styling, ensuring the control can be templated without breaking the GPU surface.

## Phase 6 - Cross-Head Strategy (Skia, WebAssembly, Mobile)
- [ ] Document how the WGPU-first control coexists with Uno's Skia heads; prototype a configuration toggle that detaches Skia rendering and hosts the WGPU surface in a native window overlay when running on macOS/Linux.
- [ ] Investigate feasibility of reusing the upcoming WinForms `Winit` backend for non-Windows platforms via Uno's `Uno.UI.Runtime.Skia.Wpf` path, sharing device creation but swapping the windowing abstraction.
- [ ] For WebAssembly, evaluate wgpu's `wgpu-wasm` backend status and decide whether a WebGPU path or CPU fallback is realistic; record gating issues and potential phased adoption.
- [ ] Define build-time feature flags (`VelloSharp.Uno.WgpuOnly`, `VelloSharp.Uno.CpuFallback`) so integrators can strip unused backends per platform head.

## Phase 7 - Validation, Samples, and Documentation
- [ ] Extend the Uno sample to include dynamic DPI changes, continuous/on-demand toggles, diagnostics overlays, and GPU adapter selection to mirror WinForms/WPF parity tests.
- [ ] Add automated smoke tests using Uno's UI test harness (WinUI head) to verify swapchain creation, resize handling, and render loop start/stop sequences without GPU leaks.
- [ ] Write step-by-step guidance in `docs/uno-wgpu-integration.md`, updating the main README matrix to list Uno support, prerequisites, and known limitations (Skia coexistence, WASM fallback state).
- [ ] Capture profiling runs comparing CPU fallback vs. WGPU paths under Uno to confirm "full speed" goals and highlight any remaining perf cliffs.

## Reuse Assessment Summary
- `VelloSharp.Windows.Core` covers GPU context leasing, diagnostics, and swapchain resource pooling; only surface descriptors and dispatcher abstractions need Uno-specific glue.
- The WPF `IWindowsSurfaceSource` and `WindowsSurfaceFactory` patterns transfer directly; new implementations simply wrap Uno-provided COM interfaces (SwapChainPanel, CoreWindow).
- Existing diagnostics and render-loop helpers become shared once adapted to Uno's dispatcher model, minimising duplicate logic across Windows UI stacks.

## Open Questions / Risks
- [ ] Confirm SwapChainPanel-based surfaces behave correctly under Uno's pixel scaling and high-DPI adjustments, especially when Uno's Skia compositor remains active for sibling controls.
- [ ] Determine how focus/input integration works when WGPU owns the surface; ensure Uno's input system (pointer, keyboard, gamepad) can still bubble events to the rest of the UI tree.
- [ ] Validate that wgpu's DX12 backend with 11-on-12 interop remains stable inside WinAppSDK sandbox restrictions; document fallbacks if device creation fails due to capability limits.
- [ ] Plan migration for Uno's legacy UWP head if future Windows SDKs deprecate CoreWindow swapchain descriptors; track upstream wgpu roadmap for UWP support longevity.
- [ ] Retry the WinAppSDK sample build with MSBuild/Visual Studio toolchain once those components are available on the target environment.

## Supporting Design Notes
- Uno's WinAppSDK head prefers `DispatcherQueue`; a shared abstraction over WPF's `Dispatcher` and WinForms' message pump should live beside `RenderLoopDriver` to avoid thread-affinity bugs.
- A future cross-platform story may align with the Avalonia/Winit plans: once winit hosting stabilises, Uno heads running on Skia could hand control to a native window created by the shared backend, reducing duplication across UI stacks.
