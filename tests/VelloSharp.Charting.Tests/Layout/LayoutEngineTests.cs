using System.Collections.Generic;
using VelloSharp.Charting.Layout;
using Xunit;

namespace VelloSharp.Charting.Tests.Layout;

public sealed class LayoutEngineTests
{
    [Fact]
    public void ArrangesAxesWithDpiAwareRounding()
    {
        var engine = new ChartLayoutEngine();
        var request = new ChartLayoutRequest(
            viewportWidth: 800d,
            viewportHeight: 600d,
            devicePixelRatio: 1.5d,
            axes: new List<AxisLayoutRequest>
            {
                new(AxisOrientation.Left, thickness: 60d),
                new(AxisOrientation.Bottom, thickness: 40d),
            });

        var result = engine.Arrange(request);

        Assert.Equal(new LayoutRect(60d, 0d, 740d, 560d), result.PlotArea);

        var leftAxis = Assert.Single(result.Axes, axis => axis.Orientation == AxisOrientation.Left);
        Assert.Equal(new LayoutRect(0d, 0d, 60d, 560d), leftAxis.Bounds);

        var bottomAxis = Assert.Single(result.Axes, axis => axis.Orientation == AxisOrientation.Bottom);
        Assert.Equal(new LayoutRect(60d, 560d, 740d, 40d), bottomAxis.Bounds);
    }

    [Fact]
    public void HandlesMissingAxes()
    {
        var engine = new ChartLayoutEngine();
        var result = engine.Arrange(new ChartLayoutRequest(400d, 200d, 2d));

        Assert.Equal(new LayoutRect(0d, 0d, 400d, 200d), result.PlotArea);
        Assert.Empty(result.Axes);
    }
}
