# VelloSharp.Avalonia.Controls

`VelloSharp.Avalonia.Controls` bundles reusable Avalonia UI controls that render directly through the Vello renderer.
It builds on top of `VelloSharp.Avalonia.Vello`, exposing a drawing canvas, an animation surface, and an SVG presenter so
you can light up Vello scenes without hand-writing the lease plumbing each time.

## Getting Started

1. Install with `dotnet add package VelloSharp.Avalonia.Controls`.
2. Make sure your application registers the Vello backends (`UseWinit()` and/or `UseVello()`).
3. Import the namespace in XAML:  
   `xmlns:controls="clr-namespace:VelloSharp.Avalonia.Controls;assembly=VelloSharp.Avalonia.Controls"`.
4. Drop the controls into your layouts and handle the provided events/properties.

## Key Controls

- **`VelloCanvasControl`** – exposes a `Draw` event with the live `Scene`, `RenderParams`, and global transform.
- **`VelloAnimatedCanvasControl`** – adds a managed render loop with `IsPlaying`, `FrameRate`, `TotalTime`, and `DeltaTime`.
- **`VelloSvgControl`** – renders `VelloSvg` documents from resources, files, or in-memory payloads with familiar `Stretch` behaviour.

## Usage Example

```xml
<controls:VelloAnimatedCanvasControl x:Name="AnimatedSurface"
                                     Draw="OnAnimatedDraw"
                                     MinHeight="320"/>
```

```csharp
private void OnAnimatedDraw(object? sender, VelloDrawEventArgs e)
{
    var scene = e.Scene;
    var transform = e.GlobalTransform;
    var bounds = e.Bounds;

    var rect = new PathBuilder();
    rect.MoveTo(bounds.X, bounds.Y);
    rect.LineTo(bounds.Right, bounds.Y);
    rect.LineTo(bounds.Right, bounds.Bottom);
    rect.LineTo(bounds.X, bounds.Bottom);
    rect.Close();

    scene.FillPath(rect, FillRule.NonZero, transform, new SolidColorBrush(RgbaColor.FromBytes(20, 28, 42)));
}
```

## Sample Application

See `samples/AvaloniaVelloControlsSample` in the repository for a complete walkthrough that demonstrates:

- Custom drawing on `VelloCanvasControl`.
- Time-based compositions on `VelloAnimatedCanvasControl`.
- Resource loading and sizing behaviour in `VelloSvgControl`.

Run it with:

```bash
dotnet run --project samples/AvaloniaVelloControlsSample/AvaloniaVelloControlsSample.csproj
```

