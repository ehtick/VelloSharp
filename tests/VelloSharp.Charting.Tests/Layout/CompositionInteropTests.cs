using System;
using VelloSharp.Composition;
using Xunit;

namespace VelloSharp.Charting.Tests.Layout;

public sealed class CompositionInteropTests
{
    [Fact]
    public void SolveLinearLayout_DistributesWeight()
    {
        Span<CompositionInterop.LinearLayoutChild> children = stackalloc CompositionInterop.LinearLayoutChild[2];
        children[0] = new CompositionInterop.LinearLayoutChild(10.0, 20.0, 80.0, 1.0, 0.0, 0.0);
        children[1] = new CompositionInterop.LinearLayoutChild(10.0, 20.0, 80.0, 2.0, 0.0, 0.0);

        Span<CompositionInterop.LinearLayoutResult> results = stackalloc CompositionInterop.LinearLayoutResult[2];
        int solved = CompositionInterop.SolveLinearLayout(children, 90.0, 0.0, results);

        Assert.Equal(2, solved);
        Assert.Equal(0.0, results[0].Offset, 6);
        Assert.InRange(results[0].Length, 36.6, 36.8);
        Assert.Equal(results[0].Length, results[1].Offset, 3);
        Assert.InRange(results[1].Length, 53.3, 53.4);
    }

    [Fact]
    public void SceneCache_TracksDirtyRegion()
    {
        using var cache = new SceneCache();
        uint root = cache.CreateNode();
        uint child = cache.CreateNode(root);

        cache.MarkDirty(child, 1.0, 2.0);
        cache.MarkDirty(child, 4.0, -1.0);

        Assert.True(cache.TakeDirty(root, out var region));
        Assert.Equal(1.0, region.MinX, 6);
        Assert.Equal(4.0, region.MaxX, 6);
        Assert.Equal(-1.0, region.MinY, 6);
        Assert.Equal(2.0, region.MaxY, 6);

        Assert.False(cache.TakeDirty(root, out _));
    }

    [Fact]
    public void SceneCache_MarkDirtyBounds_ExpandsRegion()
    {
        using var cache = new SceneCache();
        uint root = cache.CreateNode();

        cache.MarkDirtyBounds(root, 5.0, 15.0, -3.0, 9.0);
        cache.MarkDirtyBounds(root, 2.0, 4.0, -6.0, -1.0);

        Assert.True(cache.TakeDirty(root, out var region));
        Assert.InRange(region.MinX, 2.0, 2.1);
        Assert.InRange(region.MaxX, 15.0, 15.1);
        Assert.InRange(region.MinY, -6.0, -5.9);
        Assert.InRange(region.MaxY, 9.0, 9.1);
    }
}
