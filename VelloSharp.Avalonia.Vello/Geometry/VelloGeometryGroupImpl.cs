using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Platform;
using VelloSharp.Avalonia.Vello.Geometry;

namespace VelloSharp.Avalonia.Vello;

internal sealed class VelloGeometryGroupImpl : VelloGeometryImplBase
{
    public VelloGeometryGroupImpl(global::Avalonia.Media.FillRule fillRule, IReadOnlyList<IGeometryImpl> children)
        : base(CreateData(children), fillRule)
    {
        Children = children;
    }

    public IReadOnlyList<IGeometryImpl> Children { get; }

    private static VelloPathData CreateData(IReadOnlyList<IGeometryImpl> children)
    {
        var data = new VelloPathData();
        foreach (var child in children)
        {
            if (child is VelloGeometryImplBase vello)
            {
                AppendChild(data, vello.GetCommandsSnapshot());
            }
        }
        return data;
    }

    private static void AppendChild(VelloPathData target, Geometry.VelloPathData.PathCommand[] commands)
    {
        foreach (var command in commands)
        {
            switch (command.Verb)
            {
                case VelloPathVerb.MoveTo:
                    target.MoveTo(command.X0, command.Y0);
                    break;
                case VelloPathVerb.LineTo:
                    target.LineTo(command.X0, command.Y0);
                    break;
                case VelloPathVerb.QuadTo:
                    target.QuadraticTo(command.X0, command.Y0, command.X1, command.Y1);
                    break;
                case VelloPathVerb.CubicTo:
                    target.CubicTo(command.X0, command.Y0, command.X1, command.Y1, command.X2, command.Y2);
                    break;
                case VelloPathVerb.Close:
                    target.Close();
                    break;
            }
        }
    }
}
