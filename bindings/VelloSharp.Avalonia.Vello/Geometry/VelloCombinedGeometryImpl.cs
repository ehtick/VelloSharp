using Avalonia.Media;
using Avalonia.Platform;
using VelloSharp.Avalonia.Vello.Geometry;

namespace VelloSharp.Avalonia.Vello;

internal sealed class VelloCombinedGeometryImpl : VelloGeometryImplBase
{
    public VelloCombinedGeometryImpl(GeometryCombineMode combineMode, IGeometryImpl g1, IGeometryImpl g2)
        : base(CreateData(g1, g2))
    {
        CombineMode = combineMode;
        First = g1;
        Second = g2;
    }

    public GeometryCombineMode CombineMode { get; }

    public IGeometryImpl First { get; }

    public IGeometryImpl Second { get; }

    private static VelloPathData CreateData(IGeometryImpl g1, IGeometryImpl g2)
    {
        var data = new VelloPathData();
        AppendIfCompatible(data, g1);
        AppendIfCompatible(data, g2);
        return data;
    }

    private static void AppendIfCompatible(VelloPathData target, IGeometryImpl geometry)
    {
        if (geometry is VelloGeometryImplBase vello)
        {
            foreach (var command in vello.GetCommandsSnapshot())
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
}
