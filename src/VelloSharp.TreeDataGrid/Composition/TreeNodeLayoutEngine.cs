using System;
using System.Collections.Generic;
using VelloSharp.Composition;

namespace VelloSharp.TreeDataGrid.Composition;

/// <summary>
/// Resolves column slots for TreeDataGrid panes via the shared composition linear layout solver.
/// </summary>
public sealed class TreeNodeLayoutEngine
{
    private const int StackThreshold = 8;

    public IReadOnlyList<TreeColumnSlot> ArrangeColumns(
        ReadOnlySpan<TreeColumnDefinition> columns,
        double availableWidth,
        double spacing)
    {
        if (columns.IsEmpty || availableWidth <= 0.0)
        {
            return Array.Empty<TreeColumnSlot>();
        }

        Span<CompositionInterop.LinearLayoutChild> nativeItemsSpan = columns.Length <= StackThreshold
            ? stackalloc CompositionInterop.LinearLayoutChild[StackThreshold]
            : new CompositionInterop.LinearLayoutChild[columns.Length];
        var nativeItems = nativeItemsSpan[..columns.Length];

        for (var i = 0; i < columns.Length; i++)
        {
            nativeItems[i] = columns[i].ToLinearLayoutChild();
        }

        Span<CompositionInterop.LinearLayoutResult> slotBufferSpan = columns.Length <= StackThreshold
            ? stackalloc CompositionInterop.LinearLayoutResult[StackThreshold]
            : new CompositionInterop.LinearLayoutResult[columns.Length];
        var slotBuffer = slotBufferSpan[..columns.Length];

        var solved = CompositionInterop.SolveLinearLayout(
            nativeItems,
            availableWidth,
            spacing,
            slotBuffer);
        if (solved == 0)
        {
            return Array.Empty<TreeColumnSlot>();
        }

        var slots = new TreeColumnSlot[solved];
        for (var i = 0; i < solved; i++)
        {
            var slot = slotBuffer[i];
            slots[i] = new TreeColumnSlot(slot.Offset, slot.Length);
        }

        return slots;
    }
}

public readonly record struct TreeColumnSlot(double Offset, double Width);
