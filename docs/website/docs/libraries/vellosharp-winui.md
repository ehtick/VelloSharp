# VelloSharp.WinUI (preview)

`VelloSharp.WinUI` introduces `VelloSwapChainControl`, a WinUI 3 `SwapChainPanel` that renders Vello scenes through the shared GPU presenter. The control exposes dependency properties for backend selection (`PreferredBackend`), render loop selection (`RenderLoopDriver`/`RenderMode`), and diagnostics (`Diagnostics`, `DiagnosticsChanged`).

Key features:

- Runs on `net8.0-windows10.0.19041` with Windows App SDK 1.5.
- Reuses `VelloSharp.Windows.Shared` presenters, diagnostics, and surface abstractions.
- Ships with `samples/WinUIVelloGallery`, showcasing animated rendering and an overlay fed by `VelloSwapChainRenderEventArgs`.

Refer to [WinUI – Getting Started](../guides/winui-vello-getting-started.md) for integration steps and code snippets.
