using System.Collections.Generic;
using VelloSharp.Charting.Axis;

namespace VelloSharp.Charting.Rendering;

public sealed class AxisVisual
{
    public AxisVisual(AxisRenderModel model, AxisLineVisual axisLine, IReadOnlyList<AxisTickVisual> ticks, IReadOnlyList<AxisLabelVisual> labels)
    {
        Model = model;
        AxisLine = axisLine;
        Ticks = ticks;
        Labels = labels;
    }

    public AxisRenderModel Model { get; }

    public AxisLineVisual AxisLine { get; }

    public IReadOnlyList<AxisTickVisual> Ticks { get; }

    public IReadOnlyList<AxisLabelVisual> Labels { get; }
}
