using System;
using System.Collections.Generic;
using System.Linq;
using VelloSharp.Charting.Layout;
using VelloSharp.Charting.Ticks;

namespace VelloSharp.Charting.Axis;

/// <summary>
/// Composes axis render models using layout and tick generator registries.
/// </summary>
public sealed class AxisComposer
{
    private readonly ChartLayoutEngine _layoutEngine;
    private readonly AxisTickGeneratorRegistry _tickRegistry;

    public AxisComposer(ChartLayoutEngine? layoutEngine = null, AxisTickGeneratorRegistry? tickRegistry = null)
    {
        _layoutEngine = layoutEngine ?? new ChartLayoutEngine();
        _tickRegistry = tickRegistry ?? AxisTickGeneratorRegistry.CreateDefault();
    }

    public AxisRenderSurface Compose(double viewportWidth, double viewportHeight, double devicePixelRatio, IReadOnlyList<AxisDefinition> axes)
    {
        ArgumentNullException.ThrowIfNull(axes);

        var layoutRequest = new ChartLayoutRequest(
            viewportWidth,
            viewportHeight,
            devicePixelRatio,
            axes.Select(a => a.LayoutRequest).ToList());

        var layoutResult = _layoutEngine.Arrange(layoutRequest);
        var axisLayouts = layoutResult.Axes.ToDictionary(a => a.Orientation);

        var models = new List<AxisRenderModel>();
        foreach (var axis in axes)
        {
            if (!axisLayouts.TryGetValue(axis.Orientation, out var layout))
            {
                continue;
            }

            var model = axis.Build(layout, _tickRegistry);
            models.Add(model);
        }

        return new AxisRenderSurface(layoutResult.PlotArea, models);
    }
}
