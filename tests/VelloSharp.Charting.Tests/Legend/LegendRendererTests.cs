using VelloSharp.Charting.Axis;
using VelloSharp.Charting.Layout;
using VelloSharp.Charting.Legend;
using VelloSharp.Charting.Rendering;
using VelloSharp.Charting.Scales;
using VelloSharp.Charting.Styling;
using VelloSharp.Charting.Ticks;
using VelloSharp.ChartEngine;
using Xunit;

namespace VelloSharp.Charting.Tests.Legend;

public sealed class LegendRendererTests
{
    [Fact]
    public void PlacesLegendInsideTopRight()
    {
        var scale = new LinearScale(0d, 10d);
        var axis = new AxisDefinition<double>("x", AxisOrientation.Bottom, 40d, scale);
        var surface = new AxisComposer(new ChartLayoutEngine(), AxisTickGeneratorRegistry.CreateDefault())
            .Compose(600d, 400d, 1d, new[] { axis });

        var legend = new LegendDefinition(
            "legend",
            LegendOrientation.Vertical,
            LegendPosition.InsideTopRight,
            new[]
            {
                new LegendItem("Series A", ChartTheme.Light.Palette.Series[0], ChartSeriesKind.Line, 1.5, 0.2, 4),
                new LegendItem("Series B", ChartTheme.Light.Palette.Series[1], ChartSeriesKind.Line, 1.5, 0.2, 4),
            });

        var visual = new LegendRenderer().Render(legend, surface, ChartTheme.Light);

        Assert.Equal(legend, visual.Definition);
        Assert.Equal(ChartTheme.Light.Legend, visual.Style);
        Assert.InRange(visual.Bounds.X, surface.PlotArea.Right - visual.Bounds.Width - 8d - 0.01, surface.PlotArea.Right);
        Assert.True(visual.Items.Count == 2);
    }

    [Fact]
    public void SupportsHorizontalLayoutOutsideBottom()
    {
        var scale = new LinearScale(0d, 10d);
        var axis = new AxisDefinition<double>("x", AxisOrientation.Bottom, 40d, scale);
        var surface = new AxisComposer(new ChartLayoutEngine(), AxisTickGeneratorRegistry.CreateDefault())
            .Compose(600d, 400d, 1d, new[] { axis });

        var legend = new LegendDefinition(
            "legend",
            LegendOrientation.Horizontal,
            LegendPosition.OutsideBottom,
            new[]
            {
                new LegendItem("Ask", ChartTheme.Light.Palette.Series[0], ChartSeriesKind.Line, 1.5, 0.2, 4),
                new LegendItem("Bid", ChartTheme.Light.Palette.Series[1], ChartSeriesKind.Line, 1.5, 0.2, 4),
                new LegendItem("Last", ChartTheme.Light.Palette.Series[2], ChartSeriesKind.Line, 1.5, 0.2, 4),
            });

        var visual = new LegendRenderer().Render(legend, surface, ChartTheme.Dark);

        Assert.Equal(legend.Items.Count, visual.Items.Count);
        Assert.True(visual.Bounds.Y >= surface.PlotArea.Bottom);
        Assert.True(visual.Bounds.Width > 0d);
    }
}
