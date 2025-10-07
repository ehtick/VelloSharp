using System;
using System.Collections.Generic;
using System.Linq;
using VelloSharp.Charting.Layout;
using VelloSharp.Charting.Scales;
using VelloSharp.Charting.Styling;
using VelloSharp.Charting.Ticks;

namespace VelloSharp.Charting.Axis;

/// <summary>
/// Axis definition for a specific domain type.
/// </summary>
public sealed class AxisDefinition<T> : AxisDefinition
{
    private readonly IScale<T> _scale;
    private readonly TickGenerationOptions<T>? _tickOptions;
    private readonly IAxisTickGenerator<T>? _tickGenerator;

    public AxisDefinition(
        string id,
        AxisOrientation orientation,
        double thickness,
        IScale<T> scale,
        AxisStyle? style = null,
        TickGenerationOptions<T>? tickOptions = null,
        IAxisTickGenerator<T>? tickGenerator = null)
        : base(id, orientation, thickness, style)
    {
        _scale = scale ?? throw new ArgumentNullException(nameof(scale));
        _tickOptions = tickOptions;
        _tickGenerator = tickGenerator;
    }

    public override ScaleKind ScaleKind => _scale.Kind;

    public override IScale Scale => _scale;

    public IScale<T> TypedScale => _scale;

    public TickGenerationOptions<T>? TickOptions => _tickOptions;

    internal override AxisRenderModel Build(AxisLayout layout, AxisTickGeneratorRegistry registry)
    {
        var generator = _tickGenerator ?? registry.Get<T>(_scale.Kind);
        var ticks = generator.Generate(_scale, _tickOptions);
        var renderTicks = ticks
            .Select(t => new AxisTickInfo(t.Value, t.UnitPosition, t.Label))
            .ToList();

        return new AxisRenderModel(
            Id,
            Orientation,
            layout,
            _scale,
            Style,
            renderTicks);
    }
}
