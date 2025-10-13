using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using Windows.Foundation;

namespace VelloSharp.Windows.Accessibility;

/// <summary>
/// Represents a parsed AccessKit tree update suitable for WinUI automation peers.
/// </summary>
internal sealed class AccessKitTreeSnapshot
{
    private readonly Dictionary<ulong, AccessKitNode> _nodes;

    private AccessKitTreeSnapshot(
        ulong rootId,
        ulong focusId,
        Dictionary<ulong, AccessKitNode> nodes)
    {
        RootId = rootId;
        FocusId = focusId;
        _nodes = nodes;
    }

    public ulong RootId { get; }

    public ulong FocusId { get; }

    public bool TryGetNode(ulong id, out AccessKitNode node)
        => _nodes.TryGetValue(id, out node!);

    public ImmutableArray<AccessKitNode> GetRootChildren()
    {
        if (!_nodes.TryGetValue(RootId, out var root))
        {
            return ImmutableArray<AccessKitNode>.Empty;
        }

        return root.Children;
    }

    public static AccessKitTreeSnapshot FromUpdate(JsonDocument document)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        var rootElement = document.RootElement;

        var nodes = new Dictionary<ulong, AccessKitNode>();

        if (rootElement.TryGetProperty("nodes", out var nodesArray) &&
            nodesArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in nodesArray.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Array || entry.GetArrayLength() != 2)
                {
                    continue;
                }

                if (!TryReadNode(entry, out var node))
                {
                    continue;
                }

                nodes[node.Id] = node;
            }
        }

        var tree = rootElement.GetProperty("tree");
        var rootId = tree.GetProperty("root").GetUInt64();
        var focusId = rootElement.TryGetProperty("focus", out var focusProp)
            ? focusProp.GetUInt64()
            : rootId;

        // Ensure parent-child relationships are connected.
        foreach (var kvp in nodes)
        {
            var node = kvp.Value;
            if (node.ChildrenIds.IsDefaultOrEmpty || node.ChildrenIds.Length == 0)
            {
                continue;
            }

            var builder = ImmutableArray.CreateBuilder<AccessKitNode>(node.ChildrenIds.Length);
            foreach (var childId in node.ChildrenIds)
            {
                if (nodes.TryGetValue(childId, out var child))
                {
                    builder.Add(child);
                }
            }

            nodes[kvp.Key] = node.WithChildren(builder.ToImmutable());
        }

        if (!nodes.TryGetValue(rootId, out var rootNode))
        {
            rootNode = new AccessKitNode(
                rootId,
                "Window",
                string.Empty,
                null,
                ImmutableArray<ulong>.Empty,
                ImmutableArray<string>.Empty,
                isDisabled: false);
            nodes[rootId] = rootNode;
        }

        return new AccessKitTreeSnapshot(rootId, focusId, nodes);
    }

    private static bool TryReadNode(JsonElement entry, out AccessKitNode node)
    {
        node = default;

        try
        {
            var id = entry[0].GetUInt64();
            var payload = entry[1];

            var role = payload.TryGetProperty("role", out var roleProp)
                ? roleProp.GetString() ?? "GenericContainer"
                : "GenericContainer";

            var name = payload.TryGetProperty("name", out var nameProp)
                ? nameProp.GetString()
                : null;

            Rect? bounds = null;
            if (payload.TryGetProperty("bounds", out var boundsProp) &&
                boundsProp.ValueKind == JsonValueKind.Object &&
                boundsProp.TryGetProperty("x0", out var x0Prop) &&
                boundsProp.TryGetProperty("y0", out var y0Prop) &&
                boundsProp.TryGetProperty("x1", out var x1Prop) &&
                boundsProp.TryGetProperty("y1", out var y1Prop))
            {
                var x0 = x0Prop.GetDouble();
                var y0 = y0Prop.GetDouble();
                var x1 = x1Prop.GetDouble();
                var y1 = y1Prop.GetDouble();
                bounds = new Rect(x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
            }

            var isDisabled = payload.TryGetProperty("isDisabled", out var disabledProp) &&
                             disabledProp.ValueKind == JsonValueKind.True;

            ImmutableArray<string> actions = ImmutableArray<string>.Empty;
            if (payload.TryGetProperty("actions", out var actionsProp) &&
                actionsProp.ValueKind == JsonValueKind.Array)
            {
                var builder = ImmutableArray.CreateBuilder<string>();
                foreach (var action in actionsProp.EnumerateArray())
                {
                    if (action.ValueKind == JsonValueKind.String && action.GetString() is { } actionValue)
                    {
                        builder.Add(actionValue);
                    }
                }

                actions = builder.ToImmutable();
            }

            var childIds = ImmutableArray<ulong>.Empty;
            if (payload.TryGetProperty("children", out var childrenProp) &&
                childrenProp.ValueKind == JsonValueKind.Array)
            {
                var builder = ImmutableArray.CreateBuilder<ulong>();
                foreach (var child in childrenProp.EnumerateArray())
                {
                    if (child.ValueKind == JsonValueKind.Number && child.TryGetUInt64(out var childId))
                    {
                        builder.Add(childId);
                    }
                }

                childIds = builder.ToImmutable();
            }

            node = new AccessKitNode(id, role, name, bounds, childIds, actions, isDisabled);
            return true;
        }
        catch
        {
            node = default;
            return false;
        }
    }
}

internal readonly struct AccessKitNode
{
    public AccessKitNode(
        ulong id,
        string role,
        string? name,
        Rect? bounds,
        ImmutableArray<ulong> childrenIds,
        ImmutableArray<string> actions,
        bool isDisabled)
    {
        Id = id;
        Role = role;
        Name = name;
        Bounds = bounds;
        ChildrenIds = childrenIds;
        Children = ImmutableArray<AccessKitNode>.Empty;
        Actions = actions;
        IsDisabled = isDisabled;
    }

    private AccessKitNode(
        ulong id,
        string role,
        string? name,
        Rect? bounds,
        ImmutableArray<ulong> childrenIds,
        ImmutableArray<AccessKitNode> children,
        ImmutableArray<string> actions,
        bool isDisabled)
    {
        Id = id;
        Role = role;
        Name = name;
        Bounds = bounds;
        ChildrenIds = childrenIds;
        Children = children;
        Actions = actions;
        IsDisabled = isDisabled;
    }

    public ulong Id { get; }

    public string Role { get; }

    public string? Name { get; }

    public Rect? Bounds { get; }

    public ImmutableArray<ulong> ChildrenIds { get; }

    public ImmutableArray<AccessKitNode> Children { get; }

    public ImmutableArray<string> Actions { get; }

    public bool IsDisabled { get; }

    public AccessKitNode WithChildren(ImmutableArray<AccessKitNode> children)
        => new(Id, Role, Name, Bounds, ChildrenIds, children, Actions, IsDisabled);
}
