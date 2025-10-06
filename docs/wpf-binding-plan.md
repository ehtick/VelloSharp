# WPF Binding Implementation Plan

## Findings Snapshot
- WinForms integration exposes `VelloRenderControl` with GPU swapchain + CPU bitmap fallback, `PaintSurface` event, `RenderMode`, `PreferredBackend`, and central WGPU plumbing via `WinFormsGpuContext`.
- Research confirms wgpu can participate in WPF composition without an air-space gap by sharing a DXGI BGRA8 texture from the D3D11/D3D12 backend into D3D9Ex and handing the resulting surface to `D3DImage`, matching Microsoft guidance on surface sharing.
- `HwndHost` hosting remains useful for diagnostics or scenarios that require a raw swapchain (exclusive full-screen, debugging, deep interop). Keep it as an opt-in control renamed to `VelloNativeSwapChainHost` to avoid confusing it with the composition-based default.

## Phase 1 – Shared Windows GPU Infrastructure
- [x] Inventory WinForms Core classes and document which are WGPU/platform abstractions versus System.Drawing or Control-specific helpers.
- [x] Introduce `VelloSharp.Windows.Core` and migrate reusable types (`WindowsGpuContext`, `WindowsGpuContextLease`, `WindowsGpuResourcePool`, `WindowsGpuBufferLease`, `WindowsSwapChainSurface`, `WindowsGpuDiagnostics`, `VelloGraphicsDevice`, `VelloGraphicsDeviceOptions`, `VelloGraphicsSession`); keep `VelloPaintSurfaceEventArgs` under WinForms due to its System.Drawing dependency.
- [x] Rename namespaces/types to `VelloSharp.Windows.*` and update WinForms assemblies/tests to consume the shared code (type-forwarders still optional if external consumers require them).
- [x] Extract a shared surface abstraction (`IWindowsSurfaceSource` + `WindowsSurfaceFactory`) so each UI stack can supply HWND sizing and lifecycle hooks without duplicating swapchain setup logic.

## Phase 2 – D3DImage Interop Bridge (Primary Path)
- [x] Introduce a Windows interop shim around wgpu that can create a BGRA8 render target with `D3D11_RESOURCE_MISC_SHARED` (or an 11on12 alias when only D3D12 is exposed) and surface the underlying `ID3D11Texture2D` / `IDXGIResource` handle.
- [x] Obtain the legacy shared handle via `IDXGIResource::GetSharedHandle`, validate adapter LUIDs, and propagate diagnostics through `WindowsGpuDiagnostics` when the handle cannot be opened.
- [x] Create and cache an `IDirect3D9Ex` device bound to the WPF adapter; open the shared handle through `IDirect3DDevice9Ex::CreateTexture` and project level 0 `IDirect3DSurface9` instances for composition.
- [x] Implement optional `IDXGIKeyedMutex` synchronisation (acquire/release on D3D11 side, wait before `D3DImage` unlock); provide a fallback flush path for hardware lacking keyed mutexes.
- [x] Handle resize and device-loss by regenerating the shared texture, re-opening the corresponding D3D9 surface, and issuing `D3DImage.SetBackBuffer`/`AddDirtyRect` updates on the dispatcher thread.

## Phase 3 – Composition-Hosted WPF Control
- [x] Pilot composition hosting inside `VelloView` via `D3DImageBridge`; plan to split into a dedicated `VelloCompositionView` once the API surface settles.
- [x] Integrate dispatcher render loop wiring (`CompositionTarget.Rendering` and `RenderLoopDriver`) so continuous/on-demand modes drive the composition bridge.
- [x] Manage `D3DImage` locking (`Lock → SetBackBuffer → AddDirtyRect → Unlock`) and keyed-mutex coordination through the bridge, including resize/device-loss resets.
- [ ] Provide design-time and CPU-backend placeholders that bypass GPU interop, ensuring Blend/VS designers can instantiate the control safely.
- [x] Expose diagnostics (frame timing, device status) through dependency properties backed by `WindowsGpuDiagnostics`.

## Phase 4 - Optional HWND Swapchain Host
- [x] Rename `VelloSwapChainHost` to `VelloNativeSwapChainHost` and adjust namespaces/usages so the type explicitly reads as the HWND-based implementation.
- [x] Wrap the host in a `VelloNativeSwapChainView : Decorator` to match the `VelloCompositionView` API surface while forwarding to the shared GPU infrastructure.
- [x] Update samples and documentation to explain when to opt into the native host (e.g., when embedding third-party DirectX UI or debugging swapchain behaviour).
  - `VelloView` now always drives the composition/D3DImage path so overlay controls stay visible; the HWND swapchain host is opt-in via `VelloNativeSwapChainView`.

## Phase 5 – CPU Rendering Path
- [ ] Reuse `VelloGraphicsDevice` to rasterize into a `WriteableBitmap` sized for DPI-scaled bounds, updating stride + back buffer pinning when the control resizes.
- [ ] Implement a lightweight CPU render loop using `CompositionTarget.Rendering` (continuous) or `InvalidateVisual` (on-demand) aligned with dependency property settings.
- [ ] Ensure CPU buffers marshal back to the UI thread safely and that `WriteableBitmap.AddDirtyRect` updates are throttled to avoid layout thrash.

## Phase 6 – Lifecycle, Threading, and Diagnostics
- [x] Acquire/release shared GPU context leases on `Loaded`/`Unloaded`, handling multiple WPF view instances safely.
- [x] Suspend rendering when the control is `IsVisible == false`, window minimised, or application deactivated, resuming cleanly on visibility return.
- [x] Publish aggregated diagnostics (fps estimate, swapchain resets, keyed mutex contention) via a bindable `Diagnostics` view-model.

## Phase 7 – Validation, Samples, and Documentation
- [ ] Expand the WPF sample (net8.0-windows) to showcase CPU/native-host toggles and DPI stress; current build exercises the composition bridge with render-mode switching but still lacks backend swapping.
- [x] Add automated smoke tests for shared infrastructure (initial `D3DImageBridge` mutex/interop coverage in `VelloSharp.Windows.Core.Tests`; extend CPU surface coverage later).
- [ ] Document setup, API parity vs WinForms, and known limitations in `docs/` and update the main README with WPF guidance.

## Reuse Assessment Summary
- Shareable after neutralisation: `WinFormsGpuContext` → `WindowsGpuContext`, context lease/resource pool/buffer lease, swapchain surface, diagnostics, `VelloGraphicsDevice`, `VelloGraphicsDeviceOptions`, `VelloGraphicsSession`, `VelloPaintSurfaceEventArgs`, plus the new D3D11/D3D9Ex bridge helpers.
- WinForms-specific (stay put): GDI+/System.Drawing abstractions (`VelloGraphics`, brushes, pens, regions, bitmap helpers) and WinForms control plumbing (`VelloRenderControl`, message pump helpers).
- New abstractions needed: shared DXGI/D3D9Ex bridge, dispatcher-aware render loop helper, keyed-mutex coordination, dependency-property-friendly wrappers for device options, and dual-host (composition/native) selection logic.

## Open Questions / Risks
- [x] Verify wgpu exposes enough backend handles to create shared textures without patching upstream; continue to track upstream HAL changes around the D3D12/11-on-12 path.
- [ ] Confirm keyed mutex or flush-based synchronisation maintains frame pacing when WPF is under load; profile impact across adapters.
- [ ] Decide how CPU fallback integrates with the composition view (switching `Image.Source` vs. layered hosting) to avoid tearing during transitions.
- [ ] Ensure render callbacks never touch WPF UI elements from non-dispatcher threads; clarify threading expectations in documentation and samples.

## Supporting Design Notes
- `docs/wpf-d3dimage-interop.md` captures the shared texture shim prototype (BGRA8 + legacy handle), adapter validation, and the D3D9Ex device manager/keyed mutex coordination sketch prepared on 2025-10-06.






