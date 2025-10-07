using VelloSharp.Composition;

namespace VelloSharp.TreeDataGrid.Composition;

/// <summary>
/// Describes the sizing behaviour for a TreeDataGrid column.
/// </summary>
public readonly record struct TreeColumnDefinition(
    double MinWidth,
    double PreferredWidth,
    double MaxWidth,
    double Weight = 1.0,
    double LeadingMargin = 0.0,
    double TrailingMargin = 0.0)
{
    public CompositionInterop.LinearLayoutChild ToLinearLayoutChild()
        => new(
            Math.Max(0.0, MinWidth),
            Math.Max(0.0, PreferredWidth),
            Math.Max(0.0, MaxWidth),
            Weight <= 0.0 ? 1.0 : Weight,
            Math.Max(0.0, LeadingMargin),
            Math.Max(0.0, TrailingMargin));
}
