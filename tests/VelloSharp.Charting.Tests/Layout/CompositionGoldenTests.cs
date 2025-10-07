using System;
using System.Collections.Generic;
using VelloSharp.Composition;
using Xunit;

namespace VelloSharp.Charting.Tests.Layout;

public sealed class CompositionGoldenTests
{
    [Fact]
    public void PlotArea_UsesSharedHeuristics()
    {
        var plot = CompositionInterop.ComputePlotArea(1920.0, 1080.0);
        Assert.Equal(48.0, plot.Left, 6);
        Assert.Equal(86.4, plot.Top, 6);
        Assert.Equal(1795.2, plot.Width, 6);
        Assert.Equal(961.6, plot.Height, 6);
    }

    [Fact]
    public void PlotArea_ClampsToMinimumDimensions()
    {
        var plot = CompositionInterop.ComputePlotArea(40.0, 28.0);
        Assert.Equal(4.8, plot.Left, 6);
        Assert.Equal(0.0, plot.Top, 6);
        Assert.Equal(33.6, plot.Width, 6);
        Assert.Equal(32.0, plot.Height, 6);
    }

    [Fact]
    public void LinearLayout_SolvesGoldenScenario()
    {
        Span<CompositionInterop.LinearLayoutChild> children = stackalloc CompositionInterop.LinearLayoutChild[3];
        children[0] = new CompositionInterop.LinearLayoutChild(40.0, 60.0, 200.0, 1.0, 4.0, 4.0);
        children[1] = new CompositionInterop.LinearLayoutChild(40.0, 80.0, 120.0, 2.0, 8.0, 8.0);
        children[2] = new CompositionInterop.LinearLayoutChild(30.0, 50.0, 90.0, 1.0, 2.0, 2.0);

        Span<CompositionInterop.LinearLayoutResult> slots = stackalloc CompositionInterop.LinearLayoutResult[3];
        var solved = CompositionInterop.SolveLinearLayout(children, 360.0, 8.0, slots);

        Assert.Equal(3, solved);
        Assert.Equal(4.0, slots[0].Offset, 6);
        Assert.Equal(127.846153, slots[0].Length, 5);
        Assert.Equal(151.846153, slots[1].Offset, 5);
        Assert.Equal(118.769231, slots[1].Length, 5);
        Assert.Equal(288.615385, slots[2].Offset, 5);
        Assert.Equal(69.384615, slots[2].Length, 5);
    }

    [Theory]
    [MemberData(nameof(LabelSamples))]
    public void MeasureLabel_ProducesStableMetrics(string text, float fontSize, double expectedWidth, double expectedHeight, double expectedAscent)
    {
        var metrics = CompositionInterop.MeasureLabel(text, fontSize);
        Assert.False(metrics.IsEmpty);
        Assert.Equal(expectedWidth, metrics.Width, 3);
        Assert.Equal(expectedHeight, metrics.Height, 3);
        Assert.Equal(expectedAscent, metrics.Ascent, 3);
    }

    public static IEnumerable<object[]> LabelSamples()
    {
        yield return new object[] { "Price", 14f, 31.711914, 16.40625, 12.988281 };
        yield return new object[] { "Δ Volume", 12f, 52.3125, 14.0625, 11.1328125 };
    }
}
