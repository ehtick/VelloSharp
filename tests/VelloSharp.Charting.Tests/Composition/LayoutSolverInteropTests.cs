using System;
using VelloSharp.Composition;
using Xunit;

namespace VelloSharp.Charting.Tests.Composition;

public sealed class LayoutSolverInteropTests
{
    [Fact]
    public void SolveStackLayout_VerticalSpacingMatchesNative()
    {
        var children = new[]
        {
            new StackLayoutChild(
                new LayoutConstraints(
                    new ScalarConstraint(0, 50, 50),
                    new ScalarConstraint(0, 20, 20))),
            new StackLayoutChild(
                new LayoutConstraints(
                    new ScalarConstraint(0, 50, double.PositiveInfinity),
                    new ScalarConstraint(0, 20, 20))),
        };

        var available = new LayoutSize(100, 200);
        var options = new StackLayoutOptions(
            LayoutOrientation.Vertical,
            4,
            new LayoutThickness(5, 5, 5, 5),
            LayoutAlignment.Stretch);

        Span<LayoutRect> results = stackalloc LayoutRect[children.Length];
        int produced = CompositionInterop.SolveStackLayout(children, options, available, results);

        Assert.Equal(children.Length, produced);
        Assert.True(Math.Abs(results[0].Y - 5.0) < 1e-6, "First child respects top padding.");

        double expectedSecondY = 5.0 + results[0].PrimaryLength + 4.0;
        Assert.True(Math.Abs(results[1].Y - expectedSecondY) < 1e-6, "Spacing should match configured value.");
    }

    [Fact]
    public void SolveWrapLayout_BreaksLines()
    {
        var constraints = new LayoutConstraints(
            new ScalarConstraint(0, 60, 60),
            new ScalarConstraint(0, 20, 20));
        var child = new WrapLayoutChild(constraints, LayoutThickness.Zero);
        var children = new[] { child, child, child, child };

        var options = new WrapLayoutOptions(
            LayoutOrientation.Horizontal,
            0,
            0,
            LayoutThickness.Zero,
            LayoutAlignment.Start,
            LayoutAlignment.Stretch);

        Span<LayoutRect> rects = stackalloc LayoutRect[children.Length];
        Span<WrapLayoutLine> lines = stackalloc WrapLayoutLine[children.Length];

        var result = CompositionInterop.SolveWrapLayout(
            children,
            options,
            new LayoutSize(120, 200),
            rects,
            lines);

        Assert.Equal(children.Length, result.LayoutCount);
        Assert.Equal(2, result.LineCount);
        Assert.Equal<uint>(2, lines[0].Count);
        Assert.Equal<uint>(2, lines[1].Count);
    }

    [Fact]
    public void SolveGridLayout_PositionsChildren()
    {
        var columns = new[]
        {
            new GridTrack(GridTrackKind.Fixed, 50, 0, double.PositiveInfinity),
            new GridTrack(GridTrackKind.Star, 1, 0, double.PositiveInfinity),
        };
        var rows = new[]
        {
            new GridTrack(GridTrackKind.Auto, 0, 0, double.PositiveInfinity),
            new GridTrack(GridTrackKind.Star, 1, 0, double.PositiveInfinity),
        };

        var childConstraints = new LayoutConstraints(
            new ScalarConstraint(0, 50, 50),
            new ScalarConstraint(0, 30, 30));

        var children = new[]
        {
            new GridLayoutChild(
                childConstraints,
                0,
                0,
                1,
                1,
                LayoutThickness.Zero,
                LayoutAlignment.Stretch,
                LayoutAlignment.Stretch),
            new GridLayoutChild(
                childConstraints,
                1,
                1,
                1,
                1,
                LayoutThickness.Zero,
                LayoutAlignment.Stretch,
                LayoutAlignment.Stretch),
        };

        Span<LayoutRect> rects = stackalloc LayoutRect[children.Length];
        int produced = CompositionInterop.SolveGridLayout(
            columns,
            rows,
            children,
            new GridLayoutOptions(LayoutThickness.Zero, 0, 0),
            new LayoutSize(200, 200),
            rects);

        Assert.Equal(children.Length, produced);
        Assert.True(rects[1].X > rects[0].X);
        Assert.True(rects[1].Y > rects[0].Y);
    }

    [Fact]
    public void SolveDockLayout_RespectsSides()
    {
        var childConstraints = new LayoutConstraints(
            new ScalarConstraint(0, 40, 40),
            new ScalarConstraint(0, 200, 200));

        var children = new[]
        {
            new DockLayoutChild(
                childConstraints,
                LayoutThickness.Zero,
                DockSide.Left,
                LayoutAlignment.Stretch,
                LayoutAlignment.Stretch),
            new DockLayoutChild(
                childConstraints,
                LayoutThickness.Zero,
                DockSide.Right,
                LayoutAlignment.Stretch,
                LayoutAlignment.Stretch),
            new DockLayoutChild(
                new LayoutConstraints(
                    new ScalarConstraint(0, 100, 100),
                    new ScalarConstraint(0, 100, 100)),
                LayoutThickness.Zero,
                DockSide.Fill,
                LayoutAlignment.Stretch,
                LayoutAlignment.Stretch),
        };

        Span<LayoutRect> rects = stackalloc LayoutRect[children.Length];
        int produced = CompositionInterop.SolveDockLayout(
            children,
            new DockLayoutOptions(LayoutThickness.Zero, 0, true),
            new LayoutSize(300, 200),
            rects);

        Assert.Equal(children.Length, produced);
        Assert.True(rects[0].X < rects[2].X);
        Assert.True(rects[1].X > rects[2].X);
    }
}
