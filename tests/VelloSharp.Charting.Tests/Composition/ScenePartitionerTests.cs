using System;
using VelloSharp.Composition;
using Xunit;

namespace VelloSharp.Charting.Tests.Composition;

public sealed class ScenePartitionerTests
{
    [Fact]
    public void MaterialRegistry_ResolvesTintedColor()
    {
        const uint shaderId = 91;
        const uint materialId = 204;

        CompositionShaderRegistry.Register(
            shaderId,
            new CompositionShaderDescriptor(
                CompositionShaderKind.Solid,
                new CompositionColor(0.25f, 0.5f, 0.75f, 1f)));

        try
        {
            CompositionMaterialRegistry.Register(
                materialId,
                new CompositionMaterialDescriptor(shaderId, 0.4f));

            try
            {
                Assert.True(
                    CompositionMaterialRegistry.TryResolveColor(materialId, out var color),
                    "Material color should resolve.");
                Assert.InRange(color.A, 0.39f, 0.41f);
                Assert.InRange(color.R, 0.24f, 0.26f);
            }
            finally
            {
                CompositionMaterialRegistry.Unregister(materialId);
            }
        }
        finally
        {
            CompositionShaderRegistry.Unregister(shaderId);
        }
    }

    [Fact]
    public void GetOrCreateLayer_IsStablePerName()
    {
        using var cache = new SceneCache();
        var root = cache.CreateNode();
        var partitioner = new ScenePartitioner(cache, root);

        var first = partitioner.GetOrCreateLayer("overlays");
        var second = partitioner.GetOrCreateLayer("overlays");

        Assert.Equal(first.NodeId, second.NodeId);
        Assert.NotEqual(root, first.NodeId);
    }

    [Fact]
    public void GetOrCreateLayer_RespectsParent()
    {
        using var cache = new SceneCache();
        var root = cache.CreateNode();
        var partitioner = new ScenePartitioner(cache, root);

        var parent = partitioner.GetOrCreateLayer("series");
        var child = partitioner.GetOrCreateLayer("annotations", parent.NodeId);

        Assert.NotEqual(parent.NodeId, child.NodeId);
        Assert.NotEqual(root, child.NodeId);
    }

    [Fact]
    public void RemoveLayer_ReleasesNode()
    {
        using var cache = new SceneCache();
        var root = cache.CreateNode();
        var partitioner = new ScenePartitioner(cache, root);

        var layer = partitioner.GetOrCreateLayer("ephemeral");
        Assert.True(partitioner.RemoveLayer("ephemeral"));

        var replacement = partitioner.GetOrCreateLayer("ephemeral");
        Assert.NotEqual(layer.NodeId, replacement.NodeId);
    }
}
