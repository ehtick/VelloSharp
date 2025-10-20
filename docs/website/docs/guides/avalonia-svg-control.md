# Avalonia SVG control with Vello

`VelloSharp.Avalonia.Svg` delivers a drop-in replacement for the classic Avalonia SVG control, but every frame renders
through the Vello renderer instead of Skia. The API surface mirrors `Avalonia.Svg.Skia` so existing XAML still works,
and the control plugs directly into the `IVelloApiLeaseFeature` pipeline exposed by `VelloSharp.Avalonia.Vello`.

## Package reference

Install the library alongside the Vello renderer packages you already use:

```bash
dotnet add package VelloSharp.Avalonia.Svg
```

Make sure your Avalonia app opts into the Vello backend (and whichever windowing backend you prefer):

```csharp
AppBuilder.Configure<App>()
    .UseWinit()
    .UseVello()
    .WithInterFont();
```

## Using the `Svg` control in XAML

Reference the namespace from XAML and bind `Path` or `Source` just like the Skia-based control:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:svg="clr-namespace:VelloSharp.Avalonia.Svg;assembly=VelloSharp.Avalonia.Svg">
    <Grid RowDefinitions="Auto,*" ColumnDefinitions="*,*">
        <TextBlock Text="Aurora" Grid.ColumnSpan="2" Margin="0,0,0,12"/>
        <svg:Svg Grid.Column="0"
                 Path="avares://MyApp/Assets/Svg/aurora.svg"
                 Stretch="Uniform"
                 EnableCache="True" />
        <svg:Svg Grid.Column="1"
                 Source="{Binding InlineSvgText}"
                 Stretch="UniformToFill"
                 svg:Svg.Css="svg { color: #4AF; }"/>
    </Grid>
</Window>
```

- `Path` accepts file paths, Avalonia asset URIs, or HTTP URLs. The control caches decoded content when `EnableCache`
  is set.
- `Source` accepts inline SVG text and is handy for templated content or dynamically generated markup.
- Attached `Svg.Css` and `Svg.CurrentCss` properties let you stack stylesheet overrides without touching the SVG file.

If the drawing context cannot provide `IVelloApiLeaseFeature` (for example on a platform that still falls back to Skia),
the control skips rendering instead of rasterising through Skia so that visuals stay consistent with the rest of the
Vello pipeline.

## Loading SVG content programmatically

`SvgSource` mirrors the helper methods from `Avalonia.Svg.Skia.SvgSource` while returning Vello-backed documents:

```csharp
var svg = SvgSource.Load("avares://MyApp/Assets/Svg/chart.svg", baseUri: this.BaseUri);

// Apply CSS/Entity overrides when reloading the document.
svg.ReLoad(new SvgParameters(
    entities: new Dictionary<string, string> { ["seriesColor"] = "#FF2E63" },
    css: "svg { background: transparent; }"));

MySvgControl.SvgSource = svg;
```

- `SvgSource.Load`, `LoadFromStream`, and `LoadFromSvg` all return the same `SvgSource` wrapper used by the control.
- `SvgParameters` merges inline CSS or XML entity overrides with any values specified directly on the `Svg` control.
- Set `SvgSource.EnableThrowOnMissingResource` to surface file/asset lookup failures during development.

## Sample project

The `samples/AvaloniaVelloCommon` gallery now includes **SvgDemoPage**, which exercises:

- Mixing `Path` and inline `Source` bindings.
- Live CSS overrides via the attached properties.
- Zooming and panning through the control’s `Zoom`, `PanX`, and `PanY` direct properties.

Run the sample from the repository root:

```bash
dotnet run --project samples/AvaloniaVelloCommon/AvaloniaVelloCommon.csproj --framework net8.0
```

Ensure you have staged the native runtimes (for example by running `scripts/copy-runtimes.sh`) so the renderer can
acquire a GPU device when the gallery launches.
