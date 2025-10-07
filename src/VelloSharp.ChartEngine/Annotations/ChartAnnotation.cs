namespace VelloSharp.ChartEngine.Annotations;

public enum AnnotationKind
{
    HorizontalLine,
    VerticalLine,
}

public sealed record ChartAnnotation(AnnotationKind Kind, double Value, string? Label = null);
