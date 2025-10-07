using System;
using System.Collections.Generic;
using VelloSharp.Charting.Coordinates;
using VelloSharp.Charting.Scales;
using VelloSharp.Charting.Units;
using Xunit;

namespace VelloSharp.Charting.Tests.Scales;

public sealed class LinearScaleTests
{
    [Fact]
    public void ProjectsLinearly()
    {
        var scale = new LinearScale(0d, 10d);
        Assert.Equal(0.5d, scale.Project(5d), 3);
        Assert.Equal(10d, scale.Unproject(1d), 3);
    }

    [Fact]
    public void SupportsDescendingDomains()
    {
        var scale = new LinearScale(10d, 0d);
        Assert.Equal(0.25d, scale.Project(7.5d), 3);
        Assert.Equal(5d, scale.Unproject(0.5d), 3);
    }
}

public sealed class LogarithmicScaleTests
{
    [Fact]
    public void ProjectsLogarithmically()
    {
        var scale = new LogarithmicScale(1d, 1000d);
        var mid = Math.Pow(10d, 1.5d);
        Assert.Equal(0.5d, scale.Project(mid), 2);
        Assert.Equal(100d, scale.Unproject(0.6666666d), 1);
    }

    [Fact]
    public void RejectsNonPositiveDomains()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LogarithmicScale(-1d, 10d));
    }
}

public sealed class TimeScaleTests
{
    [Fact]
    public void ProjectsTime()
    {
        var start = DateTimeOffset.FromUnixTimeSeconds(1000);
        var end = DateTimeOffset.FromUnixTimeSeconds(1100);
        var scale = new TimeScale(start, end);

        var mid = DateTimeOffset.FromUnixTimeSeconds(1050);
        Assert.Equal(0.5d, scale.Project(mid), 3);

        var roundTrip = scale.Unproject(0.5d);
        Assert.Equal(mid.ToUnixTimeSeconds(), roundTrip.ToUnixTimeSeconds());
    }
}

public sealed class OrdinalScaleTests
{
    [Fact]
    public void ProjectsCategories()
    {
        var categories = new[] { "A", "B", "C", "D" };
        var scale = new OrdinalScale<string>(categories);

        Assert.True(scale.TryProject("C", out var unit));
        Assert.Equal(2d / 3d, unit, 6);
        Assert.Equal("B", scale.Unproject(1d / 3d));
    }

    [Fact]
    public void ThrowsForUnknownCategory()
    {
        var scale = new OrdinalScale<int>(new[] { 1, 2, 3 });
        Assert.False(scale.TryProject(5, out _));
        Assert.Throws<KeyNotFoundException>(() => scale.Project(5));
    }
}

public sealed class CoordinateTransformerTests
{
    [Fact]
    public void ProjectsToPhysicalCoordinates()
    {
        var xScale = new LinearScale(0d, 100d);
        var yScale = new LinearScale(0d, 50d);
        var transformer = CoordinateTransformer<double, double>.CreateForSize(xScale, yScale, width: 200d, height: 100d);

        var point = transformer.Project(50d, 25d);
        Assert.Equal(new ChartPoint(100d, 50d), point);
    }

    [Fact]
    public void RoundTripsThroughUnproject()
    {
        var xScale = new LinearScale(0d, 1d);
        var yScale = new LinearScale(0d, 1d);
        var transformer = new CoordinateTransformer<double, double>(xScale, yScale, new UnitRange(0d, 400d), new UnitRange(0d, 200d));

        var original = new ChartPoint(200d, 100d);
        var data = transformer.Unproject(original.X, original.Y);
        Assert.Equal(0.5d, data.X, 3);
        Assert.Equal(0.5d, data.Y, 3);
    }
}
