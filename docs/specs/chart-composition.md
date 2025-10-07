# Chart Composition Blueprint

## Goals

- Provide a declarative structure for multi-pane analytics layouts (price pane, volume pane, indicators, etc.).
- Enable explicit placement of annotation layers above/below selected panes.
- Offer enough metadata so that host controls can drive layout/legend/interaction in a predictable way.

## Concepts

- **ChartComposition** – container describing panes and annotation layers.
- **ChartPaneDefinition** – identifies a pane, the series it renders, whether it shares the primary X axis, and its height ratio.
- **CompositionAnnotationLayer** – annotation collection rendered either below, over, or above the active panes. Layers can scope annotations to specific panes via `ForPanes`.
- **ChartAnnotation** – discriminated hierarchy for horizontal/vertical guides, shaded zones, time ranges, and callouts with optional snapping helpers.
- **ChartCompositionBuilder** – fluent builder used to create compositions without manual list wiring.

## Example

```csharp
var composition = ChartComposition.Create(builder =>
{
    builder
        .Pane("price")
            .WithSeries(0, 1, 2)   // Line + scatter overlays
            .ShareXAxisWithPrimary()
            .WithHeightRatio(3)
            .Done();

    builder
        .Pane("volume")
            .WithSeries(3)         // Column series
            .ShareXAxisWithPrimary()
            .WithHeightRatio(1)
            .Done();

    builder.AnnotationLayer(
        id: "price-highlights",
        zOrder: AnnotationZOrder.AboveSeries,
        layer =>
        {
            layer.ForPanes("price");

            layer.Annotations.Add(new TimeRangeAnnotation(sessionStartSeconds, sessionEndSeconds, "Morning session")
            {
                Fill = new ChartColor(0x3A, 0xB8, 0xFF, 0x24),
                Border = new ChartColor(0x3A, 0xB8, 0xFF, 0x80),
            });

            layer.Annotations.Add(new CalloutAnnotation(sessionEndSeconds, breakoutPrice, "Breakout")
            {
                TargetPaneId = "price",
                PointerLength = 10.0,
                Color = new ChartColor(0xF4, 0x5E, 0x8C, 0xFF),
                Background = new ChartColor(0x10, 0x15, 0x1F, 0xE6),
            });
        });
});
```

## Integration Outline

1. Host controls query the composition to determine pane ordering, shared axes, and height ratios.
2. During layout, each pane receives a `ChartLayoutRequest` sized according to `NormalizedRatio`.
3. Legend and telemetry surfaces can filter series according to `pane.SeriesIds`.
4. Annotation layers are rendered in Z-order batches so hover interaction can remain deterministic.

> **Implementation note**  
> The builder currently captures intent only. Rendering hooks (pane → viewport mapping, event routing, layout scheduling) are planned in the next iteration once the composition data model has been validated with consumers.
