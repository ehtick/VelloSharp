# WinUI & UWP Validation Matrix

This log captures the manual validation matrix for the WinUI 3 and UWP bindings to complement the automated `winui-uwp-validation` GitHub Actions job. Update the status column with `✅` when a scenario has been executed on the target hardware/OS build and include any notes (device model, OS build, blockers).

## Desktop (Windows 10/11, Win32 + MSIX)

| Scenario | WinUI 3 (MSIX/Unpackaged) | UWP (AppX/MSIX) | Notes |
| --- | --- | --- | --- |
| Build & install package | ✅ (CI packaging & local smoke test) | ✅ (CI packaging) | Validate sideload install on Windows 11 23H2 + Windows 10 19045 |
| Launch & swapchain rendering | ☐ Pending | ☐ Pending | Confirm sample renders animated content with `PreferredBackend=Gpu` |
| Diagnostics overlay toggle | ☐ Pending | ☐ Pending | Toggle diagnostics overlay (Ctrl+Shift+D) and ensure metrics update |
| Pointer, touch & keyboard input | ☐ Pending | ☐ Pending | Exercise pointer capture, focus traversal, text input |
| AccessKit/Automation smoke | ☐ Pending | ☐ Pending | Inspect automation tree with Narrator/Accessibility Insights |

## Xbox Series X|S (AppContainer)

| Scenario | Status | Notes |
| --- | --- | --- |
| Deploy packaged build via Device Portal | ☐ Pending | Target OS: Xbox OS 10.0.25398.4874 |
| GPU backend fallback (WARP) | ☐ Pending | Force `PreferredBackend=Gpu` and ensure WARP fallback stays responsive |
| Gamepad navigation | ☐ Pending | Validate focus + Invoke actions using Xbox controller |
| Diagnostics overlay | ☐ Pending | Confirm overlay renders legibly on 4K HDR output |

## Surface Hub 2S (AppContainer)

| Scenario | Status | Notes |
| --- | --- | --- |
| App sideload & launch | ☐ Pending | OS build 10.0.19044 or later |
| Touch + pen interaction | ☐ Pending | Validate multitouch, pen hover/press mapping |
| Idle & resume | ☐ Pending | Suspend/resume app, confirm swapchain resiliency |
| Accessibility (Narrator) | ☐ Pending | Run Narrator, ensure controls surface expected names/actions |

## ARM64 (Surface Pro X / Windows Dev Kit 2023)

| Scenario | WinUI 3 | UWP | Notes |
| --- | --- | --- | --- |
| Native asset integrity | ✅ (CI verification) | ✅ (CI verification) | `verify-*-native-assets` scripts cover win-arm64 & win10-arm64 |
| Local build & run | ☐ Pending | ☐ Pending | Build on ARM64 host, ensure no x86 dependency regressions |
| GPU backend coverage | ☐ Pending | ☐ Pending | Validate D3D12 + WARP toggles on ARM64 hardware |

## Manual Test Checklist

- [ ] Capture screenshots/video of each platform run to attach to release notes.
- [ ] File bugs in `docs/logs/winui-uwp-validation.md` (link to issue) for any regressions found.
- [ ] Record OS build numbers, device SKUs, and GPU adapters used during validation.

## References

- Automated pipeline: `.github/workflows/build-pack.yml` (`winui-uwp-validation` job)
- UI smoke tests: `tests/VelloSharp.Windows.UiSmokeTests`
- Native asset verification scripts: `scripts/verify-winui-native-assets.ps1`, `scripts/verify-uwp-native-assets.ps1`
