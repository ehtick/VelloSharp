using System;

namespace VelloSharp.ChartEngine.Annotations;

/// <summary>
/// Identifies the visual presentation of an annotation.
/// </summary>
public enum AnnotationKind
{
    HorizontalLine,
    VerticalLine,
    ValueZone,
    GradientZone,
    TimeRange,
    Callout,
}

/// <summary>
/// Determines how annotation coordinates snap to chart data or ticks.
/// </summary>
[Flags]
public enum AnnotationSnapMode
{
    None = 0,
    TimeToTicks = 1 << 0,
    ValueToTicks = 1 << 1,
}

/// <summary>
/// Preferred placement for annotation callouts relative to the anchor point.
/// </summary>
public enum AnnotationCalloutPlacement
{
    Auto,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

/// <summary>
/// Base type for chart annotations rendered on top of chart panes.
/// </summary>
public abstract record ChartAnnotation
{
    protected ChartAnnotation(AnnotationKind kind, string? label = null)
    {
        Kind = kind;
        Label = label;
    }

    /// <summary>
    /// Gets the annotation kind.
    /// </summary>
    public AnnotationKind Kind { get; }

    /// <summary>
    /// Gets an optional label rendered alongside the annotation.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// Gets or sets the primary color for the annotation (line, callout border, etc.).
    /// </summary>
    public ChartColor? Color { get; init; }

    /// <summary>
    /// Gets or sets the pane identifier this annotation targets. When null, the annotation applies to all panes.
    /// </summary>
    public string? TargetPaneId { get; init; }
}

/// <summary>
/// Horizontal reference line rendered across the pane.
/// </summary>
public sealed record HorizontalLineAnnotation(double Value, string? Label = null) : ChartAnnotation(AnnotationKind.HorizontalLine, Label)
{
    public double Value { get; } = Value;

    public double Thickness { get; init; } = 1.5;

    public AnnotationSnapMode SnapMode { get; init; } = AnnotationSnapMode.ValueToTicks;
}

/// <summary>
/// Vertical reference line rendered across the plot width.
/// </summary>
public sealed record VerticalLineAnnotation(double TimestampSeconds, string? Label = null) : ChartAnnotation(AnnotationKind.VerticalLine, Label)
{
    public double TimestampSeconds { get; } = TimestampSeconds;

    public double Thickness { get; init; } = 1.5;

    public AnnotationSnapMode SnapMode { get; init; } = AnnotationSnapMode.TimeToTicks;
}

/// <summary>
/// Shaded value band spanning the pane height.
/// </summary>
public sealed record ValueZoneAnnotation(double MinValue, double MaxValue, string? Label = null) : ChartAnnotation(AnnotationKind.ValueZone, Label)
{
    public double MinValue { get; } = MinValue;

    public double MaxValue { get; } = MaxValue;

    public AnnotationSnapMode SnapMode { get; init; } = AnnotationSnapMode.ValueToTicks;

    public ChartColor? Fill { get; init; }

    public ChartColor? Border { get; init; }

    public double BorderThickness { get; init; } = 1.0;
}

/// <summary>
/// Shaded gradient band spanning the pane height.
/// </summary>
public sealed record GradientZoneAnnotation(
    double MinValue,
    double MaxValue,
    ChartColor StartColor,
    ChartColor EndColor,
    string? Label = null) : ChartAnnotation(AnnotationKind.GradientZone, Label)
{
    public AnnotationSnapMode SnapMode { get; init; } = AnnotationSnapMode.ValueToTicks;

    public double FillOpacity { get; init; } = 0.35;

    public double BorderThickness { get; init; } = 1.0;
}

/// <summary>
/// Shaded time range rendered across the plot width.
/// </summary>
public sealed record TimeRangeAnnotation(double StartSeconds, double EndSeconds, string? Label = null) : ChartAnnotation(AnnotationKind.TimeRange, Label)
{
    public double StartSeconds { get; } = StartSeconds;

    public double EndSeconds { get; } = EndSeconds;

    public AnnotationSnapMode SnapMode { get; init; } = AnnotationSnapMode.TimeToTicks;

    public ChartColor? Fill { get; init; }

    public ChartColor? Border { get; init; }

    public double BorderThickness { get; init; } = 1.0;
}

/// <summary>
/// Textual callout anchored to a specific time/value coordinate.
/// </summary>
public sealed record CalloutAnnotation(double TimestampSeconds, double Value, string Label) : ChartAnnotation(AnnotationKind.Callout, Label)
{
    public double TimestampSeconds { get; } = TimestampSeconds;

    public double Value { get; } = Value;

    public AnnotationSnapMode SnapMode { get; init; } = AnnotationSnapMode.TimeToTicks | AnnotationSnapMode.ValueToTicks;

    public ChartColor? Background { get; init; }

    public ChartColor? Border { get; init; }

    public ChartColor? TextColor { get; init; }

    public double Padding { get; init; } = 6.0;

    public double PointerLength { get; init; } = 12.0;

    public AnnotationCalloutPlacement Placement { get; init; } = AnnotationCalloutPlacement.Auto;
}


