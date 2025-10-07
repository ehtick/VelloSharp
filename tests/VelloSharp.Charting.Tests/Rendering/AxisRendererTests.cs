using System.Collections.Generic;
using VelloSharp.Charting.Axis;
using VelloSharp.Charting.Layout;
using VelloSharp.Charting.Rendering;
using VelloSharp.Charting.Scales;
using VelloSharp.Charting.Ticks;
using Xunit;
using ChartRgbaColor = VelloSharp.Charting.Styling.RgbaColor;
using VelloSharp.Charting.Styling;

namespace VelloSharp.Charting.Tests.Rendering;

public sealed class AxisRendererTests
{
    [Fact]
    public void ProducesLineTickAndLabelVisuals()
    {
        var xScale = new LinearScale(0d, 10d);
        var axes = new List<AxisDefinition>
        {
            new AxisDefinition<double>("x", AxisOrientation.Bottom, 40d, xScale)
        };

        var composer = new AxisComposer(
            new ChartLayoutEngine(),
            AxisTickGeneratorRegistry.CreateDefault());

        var surface = composer.Compose(500d, 400d, 1d, axes);

        var renderer = new AxisRenderer();
        var result = renderer.Render(surface);

        Assert.Equal(surface.PlotArea, result.PlotArea);
        var axisVisual = Assert.Single(result.Axes);
        Assert.Equal(AxisOrientation.Bottom, axisVisual.Model.Orientation);
        Assert.True(axisVisual.Ticks.Count > 0);
        Assert.True(axisVisual.Labels.Count > 0);

        var line = axisVisual.AxisLine;
        Assert.Equal(surface.PlotArea.X, line.X1);
        Assert.Equal(surface.PlotArea.X + surface.PlotArea.Width, line.X2);
    }

    [Fact]
    public void HonorsAxisStyleTickLengthAndLabelMargin()
    {
        var scale = new LinearScale(0d, 1d);
        var style = new AxisStyle(
            ChartRgbaColor.FromHex(0xFFFFFFFF),
            ChartRgbaColor.FromHex(0xFFAA00FF),
            tickLength: 12d,
            labelTypography: ChartTypography.Default,
            labelMargin: 10d);

        var axis = new AxisDefinition<double>("left", AxisOrientation.Left, 60d, scale, style);

        var composer = new AxisComposer(new ChartLayoutEngine(), AxisTickGeneratorRegistry.CreateDefault());
        var surface = composer.Compose(400d, 300d, 1d, new[] { axis });
        var visual = new AxisRenderer().Render(surface).Axes[0];

        Assert.NotEmpty(visual.Ticks);
        var tick = visual.Ticks[0];
        Assert.Equal(style.TickColor, tick.Color);
        Assert.Equal(visual.Model.Layout.Bounds.Right - style.TickLength, tick.X2);

        Assert.NotEmpty(visual.Labels);
        var label = visual.Labels[0];
        Assert.Equal(style.TickLength + style.LabelMargin, visual.Model.Layout.Bounds.Right - label.X);
    }
}
