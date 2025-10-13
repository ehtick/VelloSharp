# VelloSharp.WinUI – Getting Started

The `VelloSharp.WinUI` package provides a first-party `VelloSwapChainControl` that embeds the shared GPU presenter inside any Windows App SDK (WinUI 3) desktop application. The control exposes the same device options, backend selection, and diagnostics hooks that are available in the WPF and Uno hosts, while relying on the refactored `VelloSharp.Windows.Shared` infrastructure.

## Install the packages

Add the WinUI binding (and the shared infrastructure if you consume csproj references) to your project:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\bindings\VelloSharp.WinUI\VelloSharp.WinUI.csproj" />
  <ProjectReference Include="..\..\src\VelloSharp.Windows.Shared\VelloSharp.Windows.Shared.csproj" />
</ItemGroup>
```

When the NuGet packages are published, the equivalent `PackageReference` entries will be:

```xml
<ItemGroup>
  <PackageReference Include="VelloSharp.WinUI" Version="0.5.0-alpha.*" />
  <PackageReference Include="VelloSharp.Windows.Shared" Version="0.5.0-alpha.*" />
</ItemGroup>
```

Ensure your app targets `net8.0-windows10.0.19041` and enables WinUI tooling:

```xml
<PropertyGroup>
  <UseWinUI>true</UseWinUI>
  <WindowsAppSDKPlatforms>win10-x64</WindowsAppSDKPlatforms>
</PropertyGroup>
```

## Drop the control into XAML

```xml
<Window
    x:Class="Sample.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:vello="using:VelloSharp.Windows.Controls"
    Title="VelloSharp WinUI">

    <Grid>
        <vello:VelloSwapChainControl x:Name="SwapChain"
                                     PreferredBackend="Gpu"
                                     RenderMode="Continuous" />
    </Grid>
</Window>
```

## Render a frame

Hook the `PaintSurface` and `RenderSurface` events to submit a scene and monitor diagnostics. The snippet below mirrors the minimal sample shipped in `samples/WinUIVelloGallery`.

```csharp
using System.Numerics;
using Microsoft.UI.Xaml;
using VelloSharp;
using VelloSharp.WinForms.Integration; // VelloPaintSurfaceEventArgs
using VelloSharp.Windows.Shared.Contracts;
using VelloSharp.Windows.Shared.Presenters;

public sealed partial class MainWindow : Window
{
    private readonly PathBuilder _frame = new();
    private readonly DateTimeOffset _start = DateTimeOffset.UtcNow;

    public MainWindow()
    {
        InitializeComponent();
        SwapChain.PaintSurface += OnPaintSurface;
        SwapChain.RenderSurface += OnRenderSurface;
    }

    private void OnPaintSurface(object? sender, VelloPaintSurfaceEventArgs e)
    {
        var session = e.Session;
        var scene = session.Scene;
        scene.Reset();

        var width = session.Width;
        var height = session.Height;

        _frame.Clear();
        _frame.MoveTo(0, 0).LineTo(width, 0).LineTo(width, height).LineTo(0, height).Close();

        var elapsed = (float)(DateTimeOffset.UtcNow - _start).TotalSeconds;
        var background = new RgbaColor(0.2f + 0.2f * MathF.Sin(elapsed),
                                       0.3f + 0.2f * MathF.Sin(elapsed * 1.4f),
                                       0.6f, 1f);

        scene.FillPath(_frame, FillRule.NonZero, Matrix3x2.Identity, background);
    }

    private void OnRenderSurface(object? sender, VelloSwapChainRenderEventArgs e)
    {
        // keep the animation running in OnDemand mode
        if (SwapChain.RenderMode == VelloRenderMode.OnDemand)
        {
            SwapChain.RequestRender();
        }
    }
}
```

## GPU backends & WARP fallback

VelloSharp exposes the same backend toggles as the WPF/WinForms hosts. By default the WinUI control asks WGPU to probe both D3D12 and Vulkan. You can configure and validate the runtime as follows:

- Set `AppContext.SetData("Vello:Windows:Backends", "dx12;vulkan")` or export `VELLO_WINDOWS_BACKENDS=dx12,vulkan` to override the backend mask. Accepted tokens are `dx12`, `vulkan`, `gl`, `metal`, `webgpu`, or `auto`.
- Use `AppContext.SetSwitch("Vello:Windows:ForceWarp", true)` or `VELLO_WINDOWS_FORCE_WARP=1` to force the WARP fallback adapter, even when hardware succeeds.
- Block fallback entirely with `AppContext.SetSwitch("Vello:Windows:DisableWarp", true)` or `VELLO_WINDOWS_DISABLE_WARP=1` if your container disallows software adapters.
- Each NuGet package includes `runtimes/win-*/native/vello.backends.json` describing the shipped backends. Run `scripts/verify-winui-native-assets.ps1 -BundlePath <pkg-root> -RuntimeIdentifier win-x64` as part of CI to ensure the binaries and signatures are present.

## Diagnostics

`VelloSwapChainControl.Diagnostics` surfaces GPU counters captured by `VelloSharp.Windows.Shared`. Subscribe to `DiagnosticsChanged` (via `IVelloDiagnosticsProvider`) or `RenderSurface` to display FPS, swapchain resets, and backend health in your UI. The sample overlay in `samples/WinUIVelloGallery` is a good starting point.

## Next steps

- Review `samples/WinUIVelloGallery` for a more complete scene and animated diagnostics overlay.
- Integrate the control into your DI container by exposing a factory that wires up `VelloGraphicsDeviceOptions`.
- Track progress on UWP/AppContainer support in `docs/winui-uwp-vello-full-gpu-integration-plan.md`.
