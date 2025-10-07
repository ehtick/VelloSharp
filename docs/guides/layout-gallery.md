# Layout Gallery

The layout gallery captures a set of opinionated presets that blend axis thickness, theme pairing, and device DPI adjustments. They are intended as ready-to-use starting points when bootstrapping dashboards.

| Id | Display name | Theme | Notes |
| --- | --- | --- | --- |
| `single-pane-dark` | Single Pane — Dark | `ChartTheme.Dark` | Default dashboard layout with generous left axis padding and balanced bottom labels. |
| `single-pane-light` | Single Pane — Light | `ChartTheme.Light` | Compact axes for light backgrounds, leaving more real estate for the plot area. |
| `split-pane-analytics` | Split Pane — Analytics | `ChartTheme.Dark` | Designed for price plus derived indicators; adds a slimmer top axis prepared for stacked panes. |

## Usage

```csharp
var preset = LayoutGallery.Presets.First(p => p.Id == "single-pane-dark");
var request = new ChartLayoutRequest(width, height, renderScaling);
var layout = preset.Layout(request);

// layout.PlotArea -> feed into overlay renderer
// preset.Theme    -> assign to ChartView.Theme or custom legend surfaces
```

### Adaptive Sizing Tips

- Axis thickness is clamped against the device DPI via the preset helper; adjust min/max thresholds when cloning for bespoke experiences.
- When stacking multiple panes vertically, normalize `ChartPaneDefinition.HeightRatio` in the composition model and pair each pane with a preset.
- For dense screens (e.g., mobile dashboards) call the preset and then shrink the returned `AxisLayouts` by a custom factor before drawing.

The Avalonia sample (`VelloSharp.Charting.AvaloniaSample`) consumes these presets to keep the chart readable when toggling between dark and light modes without hand-tuning axis offsets.
