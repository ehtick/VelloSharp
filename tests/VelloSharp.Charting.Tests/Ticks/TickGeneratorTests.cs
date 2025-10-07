using System;
using System.Linq;
using VelloSharp.Charting.Scales;
using VelloSharp.Charting.Ticks;
using Xunit;

namespace VelloSharp.Charting.Tests.Ticks;

public sealed class LinearTickGeneratorTests
{
    [Fact]
    public void ProducesNiceTicks()
    {
        var scale = new LinearScale(0d, 100d);
        var generator = new LinearTickGenerator();
        var ticks = generator.Generate(scale);

        Assert.Equal(6, ticks.Count);
        Assert.Equal(0d, ticks.First().Value);
        Assert.Equal(1d, ticks.Last().UnitPosition, 3);
    }
}

public sealed class TimeTickGeneratorTests
{
    [Fact]
    public void ProducesTemporalTicks()
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddHours(6);
        var scale = new TimeScale(start, end);
        var generator = new TimeTickGenerator();
        var ticks = generator.Generate(scale);

        Assert.True(ticks.Count >= 3);
        Assert.InRange(ticks[0].Value, start, end);
        Assert.InRange(ticks[^1].Value, start, end);
    }
}

public sealed class OrdinalTickGeneratorTests
{
    [Fact]
    public void EmitsTicksForAllCategories()
    {
        var scale = new OrdinalScale<string>(new[] { "Bid", "Ask", "Last" });
        var generator = new OrdinalTickGenerator<string>();
        var ticks = generator.Generate(scale);

        Assert.Equal(3, ticks.Count);
        Assert.Equal(0d, ticks[0].UnitPosition);
        Assert.Equal(1d, ticks[^1].UnitPosition);
    }

    [Fact]
    public void RegistryProvidesGeneratorForNumericOrdinal()
    {
        var registry = AxisTickGeneratorRegistry.CreateDefault();
        var generator = registry.Get<int>(ScaleKind.Ordinal);
        var scale = new OrdinalScale<int>(new[] { 1, 2, 3, 4 });
        var ticks = generator.Generate(scale);

        Assert.Equal(4, ticks.Count);
    }
}
