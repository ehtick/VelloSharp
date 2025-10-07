#nullable enable

using System.Collections.Generic;
using VelloSharp.Charting.Axis;
using VelloSharp.Charting.Layout;
using VelloSharp.Charting.Scales;
using VelloSharp.Charting.Styling;
using VelloSharp.Charting.Ticks;
using Xunit;
using ChartRgbaColor = VelloSharp.Charting.Styling.RgbaColor;

namespace VelloSharp.Charting.Tests.Axis;

public sealed class AxisComposerTests
{
    [Fact]
    public void ComposesAxisModelsWithTicks()
    {
        var xScale = new LinearScale(0d, 10d);
        var yScale = new LinearScale(0d, 100d);

        var axes = new List<AxisDefinition>
        {
            new AxisDefinition<double>("x", AxisOrientation.Bottom, thickness: 40d, xScale),
            new AxisDefinition<double>("y", AxisOrientation.Left, thickness: 60d, yScale,
                style: new AxisStyle(
                    ChartRgbaColor.FromHex(0xFFFFFFFF),
                    ChartRgbaColor.FromHex(0xFFFFFFFF),
                    tickLength: 8d,
                    ChartTypography.Default))
        };

        var composer = new AxisComposer(
            new ChartLayoutEngine(),
            new AxisTickGeneratorRegistry()
                .Register(ScaleKind.Linear, new LinearTickGenerator())
                .Register(ScaleKind.Logarithmic, new LinearTickGenerator())
                .Register(ScaleKind.Time, new TimeTickGenerator())
                .Register(ScaleKind.Ordinal, new OrdinalTickGenerator<string>()));

        var surface = composer.Compose(800d, 600d, devicePixelRatio: 1.0d, axes);

        Assert.Equal(new LayoutRect(60d, 0d, 740d, 560d), surface.PlotArea);
        Assert.Equal(2, surface.Axes.Count);

        var xAxis = Assert.Single(surface.Axes, axis => axis.Id == "x");
        Assert.Equal(AxisOrientation.Bottom, xAxis.Orientation);
        Assert.True(xAxis.Ticks.Count > 0);

        var yAxis = Assert.Single(surface.Axes, axis => axis.Id == "y");
        Assert.Equal(8d, yAxis.Style.TickLength);
    }

    [Fact]
    public void HonorsCustomTickGenerator()
    {
        var scale = new LinearScale(0d, 1d);
        var customGenerator = new StubTickGenerator();

        var axis = new AxisDefinition<double>("x", AxisOrientation.Bottom, 40d, scale, tickGenerator: customGenerator);
        var composer = new AxisComposer(new ChartLayoutEngine(), new AxisTickGeneratorRegistry().Register(ScaleKind.Linear, new LinearTickGenerator()));

        var surface = composer.Compose(400d, 200d, 1d, new[] { axis });

        var model = Assert.Single(surface.Axes);
        Assert.Single(model.Ticks);
        Assert.Equal(1, customGenerator.Generated);
    }

    private sealed class StubTickGenerator : IAxisTickGenerator<double>
    {
        public int Generated { get; private set; }

        public IReadOnlyList<AxisTick<double>> Generate(IScale<double> scale, TickGenerationOptions<double>? options = null)
        {
            Generated++;
            return new[] { new AxisTick<double>(0d, 0d, "0") };
        }
    }
}
