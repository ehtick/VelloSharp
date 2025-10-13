# VelloSharp.Windows.Shared (preview)

`VelloSharp.Windows.Shared` centralises the swapchain presenter, diagnostics pipeline, and dispatcher glue that used to live inside the Uno control. WinUI, UWP, MAUI, and Uno bindings now depend on the same `VelloSwapChainPresenter`, removing divergent lifecycle code paths and making future hosts easier to build.

- Targets `net8.0-windows10.0.17763` today; an AppContainer (`uap10.0.19041`) build is planned alongside the UWP refactor.
- Exposes `IVelloSwapChainPresenterHost`, `VelloSwapChainPresenter`, `VelloSwapChainRenderEventArgs`, and diagnostics abstractions reused across all Windows XAML hosts.
- Packages the presenter in `bindings/VelloSharp.Windows.Shared/VelloSwapChainPresenter.cs` and is exercised by the WinUI/UWP samples.

See also:

- [WinUI getting started guide](../../guides/winui-vello-getting-started.md)
- [UWP preview notes](../../guides/uwp-vello-getting-started.md)
