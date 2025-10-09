namespace VelloSharp.Composition.Controls;

public class GeometryPresenter : CompositionElement
{
    public string? Data { get; set; }

    public CompositionColor Fill { get; set; }

    public CompositionColor Stroke { get; set; }

    public double StrokeThickness { get; set; } = 1.0;
}
