using System;
using System.Collections.Generic;

namespace VelloSharp.TreeDataGrid.Composition;

public sealed class TreeColumnLayoutAnimator
{
    private readonly Dictionary<uint, ColumnState> _states = new();
    private readonly TreeNodeLayoutEngine _engine = new();
    private readonly double _damping;

    public TreeColumnLayoutAnimator(double damping = 0.35)
    {
        _damping = Math.Clamp(damping, 0.05, 1.0);
    }

    public IReadOnlyList<TreeColumnSlot> Animate(
        ReadOnlySpan<TreeColumnDefinition> columns,
        double availableWidth,
        double spacing)
    {
        var targetSlots = _engine.ArrangeColumns(columns, availableWidth, spacing);
        if (targetSlots.Count == 0)
        {
            _states.Clear();
            return targetSlots;
        }

        var animated = new TreeColumnSlot[targetSlots.Count];
        var seen = HashSetPool<uint>.Rent();

        try
        {
            for (var i = 0; i < targetSlots.Count; i++)
            {
                var key = columns.Length > i && columns[i].Key != 0
                    ? columns[i].Key
                    : (uint)(i + 1);

                seen.Add(key);
                var target = targetSlots[i];
                var state = _states.TryGetValue(key, out var existing)
                    ? existing
                    : new ColumnState(target.Offset, target.Width);

                var nextOffset = Interpolate(state.Offset, target.Offset, _damping);
                var nextWidth = Interpolate(state.Width, target.Width, _damping);

                if (Math.Abs(nextOffset - target.Offset) < 0.25)
                {
                    nextOffset = target.Offset;
                }

                if (Math.Abs(nextWidth - target.Width) < 0.25)
                {
                    nextWidth = target.Width;
                }

                _states[key] = new ColumnState(nextOffset, nextWidth);
                animated[i] = new TreeColumnSlot(nextOffset, nextWidth);
            }

            RemoveStaleStates(seen);
            return animated;
        }
        finally
        {
            HashSetPool<uint>.Return(seen);
        }
    }

    public void Reset() => _states.Clear();

    private void RemoveStaleStates(HashSet<uint> activeKeys)
    {
        if (_states.Count == activeKeys.Count)
        {
            return;
        }

        var toRemove = new List<uint>();
        foreach (var key in _states.Keys)
        {
            if (!activeKeys.Contains(key))
            {
                toRemove.Add(key);
            }
        }

        foreach (var key in toRemove)
        {
            _states.Remove(key);
        }
    }

    private static double Interpolate(double current, double target, double factor)
        => current + (target - current) * factor;

    private readonly record struct ColumnState(double Offset, double Width);
}

internal static class HashSetPool<T>
    where T : notnull
{
    private static readonly Stack<HashSet<T>> Pool = new();

    public static HashSet<T> Rent()
    {
        lock (Pool)
        {
            if (Pool.TryPop(out var set))
            {
                return set;
            }
        }

        return new HashSet<T>();
    }

    public static void Return(HashSet<T> set)
    {
        set.Clear();
        lock (Pool)
        {
            Pool.Push(set);
        }
    }
}

