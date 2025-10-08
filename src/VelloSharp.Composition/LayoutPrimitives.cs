using System;

namespace VelloSharp.Composition;

public readonly record struct ScalarConstraint(double Min, double Preferred, double Max)
{
    public static ScalarConstraint Tight(double value) => new(value, value, value);
}

public readonly record struct LayoutConstraints(
    ScalarConstraint Width,
    ScalarConstraint Height);

public readonly record struct LayoutThickness(double Left, double Top, double Right, double Bottom)
{
    public static LayoutThickness Zero => new(0, 0, 0, 0);

    public double Horizontal => Math.Max(0, Left) + Math.Max(0, Right);
    public double Vertical => Math.Max(0, Top) + Math.Max(0, Bottom);
}

public readonly record struct LayoutSize(double Width, double Height)
{
    public LayoutSize ClampNonNegative() =>
        new(Math.Max(0, Width), Math.Max(0, Height));
}

public enum LayoutOrientation
{
    Horizontal = 0,
    Vertical = 1,
}

public enum LayoutAlignment
{
    Start = 0,
    Center = 1,
    End = 2,
    Stretch = 3,
}

public readonly record struct LayoutRect(
    double X,
    double Y,
    double Width,
    double Height,
    double PrimaryOffset,
    double PrimaryLength,
    uint LineIndex = 0);

public readonly record struct StackLayoutChild(
    LayoutConstraints Constraints,
    double Weight = 1.0,
    LayoutThickness Margin = default,
    LayoutAlignment CrossAlignment = LayoutAlignment.Stretch);

public readonly record struct StackLayoutOptions(
    LayoutOrientation Orientation,
    double Spacing,
    LayoutThickness Padding,
    LayoutAlignment CrossAlignment);

public readonly record struct WrapLayoutChild(
    LayoutConstraints Constraints,
    LayoutThickness Margin,
    bool LineBreak = false);

public readonly record struct WrapLayoutOptions(
    LayoutOrientation Orientation,
    double ItemSpacing,
    double LineSpacing,
    LayoutThickness Padding,
    LayoutAlignment LineAlignment,
    LayoutAlignment CrossAlignment);

public readonly record struct WrapLayoutLine(
    uint LineIndex,
    uint Start,
    uint Count,
    double PrimaryOffset,
    double PrimaryLength);

public readonly record struct WrapLayoutSolveResult(int LayoutCount, int LineCount);

public enum GridTrackKind
{
    Fixed = 0,
    Auto = 1,
    Star = 2,
}

public readonly record struct GridTrack(
    GridTrackKind Kind,
    double Value,
    double Min = 0,
    double Max = double.PositiveInfinity);

public readonly record struct GridLayoutChild(
    LayoutConstraints Constraints,
    ushort Column,
    ushort Row,
    ushort ColumnSpan,
    ushort RowSpan,
    LayoutThickness Margin,
    LayoutAlignment HorizontalAlignment,
    LayoutAlignment VerticalAlignment);

public readonly record struct GridLayoutOptions(
    LayoutThickness Padding,
    double ColumnSpacing,
    double RowSpacing);

public enum DockSide
{
    Left = 0,
    Top = 1,
    Right = 2,
    Bottom = 3,
    Fill = 4,
}

public readonly record struct DockLayoutChild(
    LayoutConstraints Constraints,
    LayoutThickness Margin,
    DockSide Side,
    LayoutAlignment HorizontalAlignment,
    LayoutAlignment VerticalAlignment);

public readonly record struct DockLayoutOptions(
    LayoutThickness Padding,
    double Spacing,
    bool LastChildFill);

public enum FrozenKind
{
    None = 0,
    Leading = 1,
    Trailing = 2,
}

public readonly record struct VirtualRowMetric(uint NodeId, double Height);

public readonly record struct VirtualColumnStrip(double Offset, double Width, FrozenKind Frozen, uint Key);

public readonly record struct RowViewportMetrics(double ScrollOffset, double ViewportExtent, double Overscan);

public readonly record struct ColumnViewportMetrics(double ScrollOffset, double ViewportExtent, double Overscan);

public readonly record struct ColumnSlice(uint PrimaryStart, uint PrimaryCount, uint FrozenLeading, uint FrozenTrailing);

public readonly record struct VirtualizerTelemetry(
    uint RowsTotal,
    uint WindowLength,
    uint Reused,
    uint Adopted,
    uint Allocated,
    uint Recycled,
    uint ActiveBuffers,
    uint FreeBuffers,
    uint Evicted);

public enum RowAction
{
    Reuse = 0,
    Adopt = 1,
    Allocate = 2,
    Recycle = 3,
}

public readonly record struct RowPlanEntry(
    uint NodeId,
    uint BufferId,
    double Top,
    float Height,
    RowAction Action);

public readonly record struct RowWindow(uint StartIndex, uint EndIndex, double TotalHeight);
