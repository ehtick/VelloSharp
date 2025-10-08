using System;
using VelloSharp.TreeDataGrid;
using VelloSharp.TreeDataGrid.Composition;
using Xunit;

namespace VelloSharp.Charting.Tests.TreeDataGrid;

public sealed class TreeColumnStripCacheTests
{
    [Fact]
    public void InitialUpdate_FlagsAllPanesChanged()
    {
        var cache = new TreeColumnStripCache();
        var definitions = CreateDefinitions();
        var slots = CreateSlots();

        var snapshot = cache.Update(definitions, slots);

        Assert.True(snapshot.PaneDiff.LeadingChanged);
        Assert.True(snapshot.PaneDiff.PrimaryChanged);
        Assert.True(snapshot.PaneDiff.TrailingChanged);
        Assert.Equal((uint)slots.Length, (uint)snapshot.Spans.Length);
        Assert.Equal(1u, snapshot.FrozenLeading);
        Assert.Equal(1u, snapshot.FrozenTrailing);
    }

    [Fact]
    public void Update_WithSameData_ClearsChangeFlags()
    {
        var cache = new TreeColumnStripCache();
        var definitions = CreateDefinitions();
        var slots = CreateSlots();

        cache.Update(definitions, slots);
        var snapshot = cache.Update(definitions, slots);

        Assert.False(snapshot.PaneDiff.LeadingChanged);
        Assert.False(snapshot.PaneDiff.PrimaryChanged);
        Assert.False(snapshot.PaneDiff.TrailingChanged);
    }

    [Fact]
    public void Update_LeadingChange_FlagsLeadingPane()
    {
        var cache = new TreeColumnStripCache();
        var definitions = CreateDefinitions();
        var slots = CreateSlots();

        cache.Update(definitions, slots);
        slots[0] = new TreeColumnSlot(slots[0].Offset + 4.0, slots[0].Width);

        var snapshot = cache.Update(definitions, slots);
        Assert.True(snapshot.PaneDiff.LeadingChanged);
        Assert.False(snapshot.PaneDiff.PrimaryChanged);
        Assert.False(snapshot.PaneDiff.TrailingChanged);
    }

    [Fact]
    public void Update_PrimaryChange_FlagsPrimaryPane()
    {
        var cache = new TreeColumnStripCache();
        var definitions = CreateDefinitions();
        var slots = CreateSlots();

        cache.Update(definitions, slots);
        slots[1] = new TreeColumnSlot(slots[1].Offset, slots[1].Width + 12.0);

        var snapshot = cache.Update(definitions, slots);
        Assert.False(snapshot.PaneDiff.LeadingChanged);
        Assert.True(snapshot.PaneDiff.PrimaryChanged);
        Assert.False(snapshot.PaneDiff.TrailingChanged);
    }

    [Fact]
    public void Update_TrailingChange_FlagsTrailingPane()
    {
        var cache = new TreeColumnStripCache();
        var definitions = CreateDefinitions();
        var slots = CreateSlots();

        cache.Update(definitions, slots);
        slots[^1] = new TreeColumnSlot(slots[^1].Offset, slots[^1].Width + 6.0);

        var snapshot = cache.Update(definitions, slots);
        Assert.False(snapshot.PaneDiff.LeadingChanged);
        Assert.False(snapshot.PaneDiff.PrimaryChanged);
        Assert.True(snapshot.PaneDiff.TrailingChanged);
    }

    [Fact]
    public void Update_AllowsDynamicTrailingAssignment()
    {
        var cache = new TreeColumnStripCache();
        var definitions = CreateDefinitions();
        var slots = CreateSlots();

        cache.Update(definitions, slots);
        definitions[1] = definitions[1] with { Frozen = TreeFrozenKind.Trailing };

        var snapshot = cache.Update(definitions, slots);
        Assert.False(snapshot.PaneDiff.LeadingChanged);
        Assert.True(snapshot.PaneDiff.PrimaryChanged);
        Assert.True(snapshot.PaneDiff.TrailingChanged);
        Assert.Equal(1u, snapshot.FrozenLeading);
        Assert.Equal(2u, snapshot.FrozenTrailing);
    }

    [Fact]
    public void PaneSnapshots_AlignWithFreezeBands()
    {
        var cache = new TreeColumnStripCache();
        var definitions = CreateDefinitions();
        var slots = CreateSlots();

        var snapshot = cache.Update(definitions, slots);
        Assert.Equal(snapshot.FrozenLeading, (uint)snapshot.LeadingPane.Count);
        Assert.Equal(snapshot.FrozenTrailing, (uint)snapshot.TrailingPane.Count);
        Assert.Equal(snapshot.Spans.Length - snapshot.LeadingPane.Count - snapshot.TrailingPane.Count, snapshot.PrimaryPane.Count);

        definitions[1] = definitions[1] with { Frozen = TreeFrozenKind.Leading };
        snapshot = cache.Update(definitions, slots);
        Assert.Equal(2u, snapshot.FrozenLeading);
        Assert.Equal(snapshot.FrozenLeading, (uint)snapshot.LeadingPane.Count);
        Assert.Equal(snapshot.Spans.Length - snapshot.LeadingPane.Count - snapshot.TrailingPane.Count, snapshot.PrimaryPane.Count);
    }

    private static TreeColumnDefinition[] CreateDefinitions() => new[]
    {
        new TreeColumnDefinition(100, 140, 260, Sizing: TreeColumnSizingMode.Pixel, PixelWidth: 140, Key: 1, Frozen: TreeFrozenKind.Leading),
        new TreeColumnDefinition(120, 160, 280, Weight: 1.5, Sizing: TreeColumnSizingMode.Star, Key: 2),
        new TreeColumnDefinition(110, 150, 260, Weight: 1.0, Sizing: TreeColumnSizingMode.Auto, Key: 3, Frozen: TreeFrozenKind.Trailing),
    };

    private static TreeColumnSlot[] CreateSlots() => new[]
    {
        new TreeColumnSlot(0.0, 140.0),
        new TreeColumnSlot(140.0, 200.0),
        new TreeColumnSlot(340.0, 240.0),
    };
}
