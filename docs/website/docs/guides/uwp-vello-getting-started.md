# VelloSharp.Uwp – Preview Notes

> **Preview**  
> The `VelloSharp.Uwp` binding currently targets `net8.0-windows10.0.19041` (WinUI 3 desktop) while we finish the AppContainer (`uap10.0.19041`) bring-up. The public API shape matches the final control, so you can wire the preview into bridged/desktop experiences today and prepare for genuine UWP packaging once the dispatcher and asset workstreams land.

## Package setup

```xml
<ItemGroup>
  <ProjectReference Include="..\..\bindings\VelloSharp.Uwp\VelloSharp.Uwp.csproj" />
  <ProjectReference Include="..\..\src\VelloSharp.Windows.Shared\VelloSharp.Windows.Shared.csproj" />
</ItemGroup>
```

With NuGet packages:

```xml
<ItemGroup>
  <PackageReference Include="VelloSharp.Uwp" Version="0.5.0-alpha.*" />
</ItemGroup>
```

## Using the `VelloSwapChainPanel`

```xml
<Window
    x:Class="Sample.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:vello="using:VelloSharp.Uwp.Controls">

    <Grid>
        <vello:VelloSwapChainPanel x:Name="SwapChain"
                                   PreferredBackend="Gpu"
                                   RenderMode="OnDemand" />
    </Grid>
</Window>
```

The code-behind matches the WinUI control—hook `PaintSurface`, fill a scene, and drive diagnostics. See `samples/UwpVelloGallery` for a reference implementation.

## GPU backends & WARP fallback

The preview control honours the shared Windows GPU toggles so you can validate Store packaging early:

- `Vello:Windows:Backends` / `VELLO_WINDOWS_BACKENDS` lets you restrict WGPU to specific backends (for example `dx12`, `vulkan`, or `auto`).
- Force the software adapter with `Vello:Windows:ForceWarp` / `VELLO_WINDOWS_FORCE_WARP`, or block it entirely via `Vello:Windows:DisableWarp` / `VELLO_WINDOWS_DISABLE_WARP`.
- Extract the package contents and run `scripts/verify-uwp-native-assets.ps1 -BundlePath <extracted-appx> -RuntimeIdentifier win10-x64` during CI to ensure the native binaries and signatures ship with your MSIX/AppX.
- Consult `runtimes/win10-*/native/vello.backends.json` inside the package to see which backends are declared for each RID.

## What’s left for true UWP support?

- Dispatcher abstraction so the shared presenter can target `CoreDispatcher` instead of `Microsoft.UI.Dispatching.DispatcherQueue`.
- AppX packaging glue to copy GPU runtime assets into `Package.Current.InstalledLocation`.
- Capability declarations and fallbacks required for Xbox/Surface Hub (WARP, optional D3D12).

Track progress in `docs/winui-uwp-vello-full-gpu-integration-plan.md`. Contributions are welcome—especially around dispatcher shims and packaging validation.
