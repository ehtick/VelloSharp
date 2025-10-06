# Uno Windows Head Baseline (Phase 1)

## Platform Heads and Minimums
| Head | Primary Surface | Min Windows Build / SDK | Notes |
| --- | --- | --- | --- |
| WinAppSDK (WinUI 3) | `Microsoft.UI.Xaml.Controls.SwapChainPanel` via `ISwapChainPanelNative` | Windows 10 19041 (20H1) when targeting `net8.0-windows10.0.19041`; supported down to 17763 with fallbacks | WinAppSDK officially supports Windows 10 1809+, so dual targeting `net8.0-windows10.0.19041` and `net8.0-windows10.0.17763` keeps new composition features while covering the lowest supported build. |
| UWP (CoreWindow) | `Windows.UI.Core.CoreWindow` surface descriptor | Windows 10 17763 (1809) | UWP apps still ship against the 1809 SDK; the CoreWindow swapchain path aligns with the existing Win32 HWND interop in `WindowsSurfaceFactory`. |
| Win32 XAML Islands | HWND (via `DesktopWindowXamlSource`) | Windows 10 17763 (1809) | Reuses the HWND-backed `WindowsSwapChainSurface`, allowing the same plumbing used by WinForms/WPF. |

## WGPU Surface Descriptor Availability
- `extern/wgpu/wgpu-hal/src/dx12/mod.rs` exposes `SurfaceTarget::SwapChainPanel` and the corresponding `ISwapChainPanelNative` bindings, confirming the pinned wgpu commit already includes SwapChainPanel support.
- `extern/wgpu/wgpu/src/api/surface.rs` contains the `SurfaceTargetUnsafe::SwapChainPanel` case alongside the existing HWND/CoreWindow paths, so no upstream bump is needed for Uno’s Windows heads.
- The wgpu changelog (`extern/wgpu/CHANGELOG.md`) references “Add WinUI 3 SwapChainPanel support”, matching the APIs required for Uno’s SwapChainPanel integration.

## Packaging Implications
- Multi-targeting `net8.0-windows10.0.19041;net8.0-windows10.0.17763;net8.0` lets Uno controls light up Windows-only projections while still compiling neutral helper code for shared logic and tests.
- `VelloSharp.Windows.Core` already centralises WGPU device leases and diagnostics, so the Uno integration project only needs to supply surface descriptors and dispatcher abstractions for the heads above.

## Sample Stub
- The WinAppSDK smoke test in `samples/VelloSharp.Uno.WinAppSdkSample` references `VelloSharp.Uno` and instantiates `VelloSwapChainPanel`, confirming the SwapChainPanel bridge initializes cleanly ahead of the Phase 3 rendering work.
