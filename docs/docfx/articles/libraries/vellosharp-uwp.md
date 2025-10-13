# VelloSharp.Uwp (preview)

The `VelloSharp.Uwp` package delivers a `VelloSwapChainPanel` that mirrors the WinUI control while we finish AppContainer support. The current build targets `net8.0-windows10.0.19041` (WinAppSDK desktop). Work is underway to add a native `uap10.0.19041` target, dispatcher shims, and AppX asset wiring so the control can light up in true UWP environments.

- Hosts the shared presenter (`VelloSharp.Windows.Shared`) and exposes the same dependency properties and events as the WinUI control.
- Sample app: `samples/UwpVelloGallery` (WinAppSDK preview) demonstrates scene updates and diagnostics overlays.
- Follow the progress and outstanding tasks in `docs/winui-uwp-vello-full-gpu-integration-plan.md`.

For current setup guidance see [UWP – Preview Notes](../../guides/uwp-vello-getting-started.md).
