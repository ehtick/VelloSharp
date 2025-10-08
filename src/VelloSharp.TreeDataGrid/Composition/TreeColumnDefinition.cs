using System;
using VelloSharp.Composition;
using VelloSharp.TreeDataGrid;

namespace VelloSharp.TreeDataGrid.Composition;

public enum TreeColumnSizingMode
{
    Auto,
    Pixel,
    Star,
}

/// <summary>
/// Describes the sizing behaviour for a TreeDataGrid column.
/// </summary>
public readonly record struct TreeColumnDefinition(
    double MinWidth,
    double PreferredWidth,
    double MaxWidth,
    double Weight = 1.0,
    double LeadingMargin = 0.0,
    double TrailingMargin = 0.0,
    TreeColumnSizingMode Sizing = TreeColumnSizingMode.Star,
    double PixelWidth = 0.0,
    uint Key = 0,
    TreeFrozenKind Frozen = TreeFrozenKind.None)
{
    public CompositionInterop.LinearLayoutChild ToLinearLayoutChild()
    {
        var min = Math.Max(0.0, MinWidth);
        var preferred = Math.Max(0.0, PreferredWidth);
        var max = Math.Max(min, Math.Max(0.0, MaxWidth));
        var marginLeading = Math.Max(0.0, LeadingMargin);
        var marginTrailing = Math.Max(0.0, TrailingMargin);

        return Sizing switch
        {
            TreeColumnSizingMode.Pixel =>
                CreatePixelLayoutChild(PixelWidth, min, max, marginLeading, marginTrailing),
            TreeColumnSizingMode.Auto =>
                new CompositionInterop.LinearLayoutChild(
                    min,
                    preferred > 0.0 ? preferred : min,
                    max,
                    1.0,
                    marginLeading,
                    marginTrailing),
            _ =>
                new CompositionInterop.LinearLayoutChild(
                    min,
                    preferred > 0.0 ? preferred : min,
                    max,
                    Weight <= 0.0 ? 1.0 : Weight,
                    marginLeading,
                    marginTrailing),
        };
    }

    private static CompositionInterop.LinearLayoutChild CreatePixelLayoutChild(
        double requestedWidth,
        double min,
        double max,
        double leading,
        double trailing)
    {
        var width = requestedWidth > 0.0 ? requestedWidth : Math.Max(min, max);
        width = Math.Clamp(width, min <= 0.0 ? width : min, max <= 0.0 ? width : max);
        return new CompositionInterop.LinearLayoutChild(width, width, width, 1.0, leading, trailing);
    }
}
