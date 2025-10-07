using System;
using VelloSharp.Charting.Layout;
using VelloSharp.Charting.Scales;
using VelloSharp.Charting.Styling;
using VelloSharp.Charting.Ticks;

namespace VelloSharp.Charting.Axis;

/// <summary>
/// Base axis definition containing layout and styling metadata.
/// </summary>
public abstract class AxisDefinition
{
    private readonly AxisLayoutRequest _layoutRequest;

    protected AxisDefinition(
        string id,
        AxisOrientation orientation,
        double thickness,
        AxisStyle? style = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Axis id is required.", nameof(id));
        }

        if (!double.IsFinite(thickness) || thickness < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(thickness), thickness, "Axis thickness must be non-negative and finite.");
        }

        Id = id;
        Orientation = orientation;
        Thickness = thickness;
        Style = style ?? AxisStyle.Default;
        _layoutRequest = new AxisLayoutRequest(orientation, thickness);
    }

    public string Id { get; }

    public AxisOrientation Orientation { get; }

    public double Thickness { get; }

    public AxisStyle Style { get; }

    public AxisLayoutRequest LayoutRequest => _layoutRequest;

    public abstract ScaleKind ScaleKind { get; }

    public abstract IScale Scale { get; }

    internal abstract AxisRenderModel Build(AxisLayout layout, AxisTickGeneratorRegistry registry);
}
