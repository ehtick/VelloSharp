# Avalonia controls powered by VelloSharp

`VelloSharp.Avalonia.Controls` provides ready-to-use Avalonia UI components that sit directly on top of the Vello renderer.
They complement the lower-level `VelloSharp.Avalonia.Vello`/`VelloSharp.Avalonia.Winit` backends by exposing canvases,
animation surfaces, and SVG presenters that already understand `IVelloApiLeaseFeature`.

## Package reference

Add the controls alongside the existing Avalonia platform integrations:

```bash
dotnet add package VelloSharp.Avalonia.Controls
```

At runtime you still need to opt into the Vello renderer and whichever windowing backend you target:

```csharp
AppBuilder.Configure<App>()
    .UseWinit()
    .UseVello()
    .WithInterFont();
```

## Control overview

### `VelloCanvasControl`

- Surfaces the live `Scene`, `RenderParams`, and `Matrix` transform through the `Draw` event.
- Set `ShowFallbackMessage="true"` (default) to render a helpful overlay if the rendering pipeline cannot expose
  `IVelloApiLeaseFeature`.
- `IsVelloAvailable` and `UnavailableReason` report the outcome of the last draw call for diagnostics.

```xml
<controls:VelloCanvasControl Draw="OnCanvasDraw"
                             HorizontalAlignment="Stretch"
                             VerticalAlignment="Stretch"/>
```

```csharp
private void OnCanvasDraw(object? sender, VelloDrawEventArgs e)
{
    var scene = e.Scene;
    var transform = e.GlobalTransform;
    var bounds = e.Bounds;

    var backdrop = new PathBuilder();
    backdrop.MoveTo(bounds.X, bounds.Y);
    backdrop.LineTo(bounds.Right, bounds.Y);
    backdrop.LineTo(bounds.Right, bounds.Bottom);
    backdrop.LineTo(bounds.X, bounds.Bottom);
    backdrop.Close();

    scene.FillPath(backdrop, FillRule.NonZero, transform, new SolidColorBrush(RgbaColor.FromBytes(14, 20, 30)));
}
```

### `VelloAnimatedCanvasControl`

- Inherits from `VelloCanvasControl` but drives a DispatcherTimer-based render loop.
- `IsPlaying` toggles the loop, `FrameRate` sets the target FPS (defaults to 60), and `TotalTime` tracks accumulated playback.
- The `Draw` event receives `TotalTime`/`DeltaTime` via the shared `VelloDrawEventArgs`, making it easy to build kinetic scenes.
- Call `Reset()` to zero the clock while keeping the control subscribed to the render loop.

```xml
<controls:VelloAnimatedCanvasControl x:Name="AnimatedCanvas"
                                     Draw="OnAnimatedDraw"
                                     IsPlaying="True"
                                     MinHeight="320"/>
```

```csharp
private void OnAnimatedDraw(object? sender, VelloDrawEventArgs e)
{
    var time = (float)e.TotalTime.TotalSeconds;
    // Build animated content using time, e.Scene, and e.GlobalTransform.
}
```

### `VelloSvgControl`

- Renders `VelloSvg` documents without rasterisation artifacts.
- Assign `Source` (URI or file path) or inject an existing `VelloSvg` instance via the `Svg` property.
- `Stretch`, `StretchDirection`, and `NoSvgMessage` mirror Avalonia image semantics; `LoadError` reports failures.
- Supports Avalonia asset URIs: `Source="avares://MyApp/Assets/logo.svg"`.

```xml
<controls:VelloSvgControl Source="avares://AvaloniaVelloControlsSample/Assets/Svg/aurora.svg"
                          Stretch="Uniform"
                          MinHeight="320"/>
```

## Sample application

The repository ships with `samples/AvaloniaVelloControlsSample`, a desktop project that demonstrates:

- Custom drawing on `VelloCanvasControl` with gradients, strokes, and lease fallbacks.
- Time-based effects driven by `VelloAnimatedCanvasControl` and its timing APIs.
- Asset loading, responsive layout, and error reporting in `VelloSvgControl`.

Build and run it from the repository root:

```bash
dotnet run --project samples/AvaloniaVelloControlsSample/AvaloniaVelloControlsSample.csproj
```

Ensure native runtimes are staged (e.g., via `scripts/copy-runtimes`) before running so the Vello renderer can resolve its binaries.

