using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using VelloSharp.Avalonia.Vello.Geometry;

namespace VelloSharp.Avalonia.Vello;

internal sealed class VelloTransformedGeometryImpl : VelloGeometryImplBase, ITransformedGeometryImpl
{
    public VelloTransformedGeometryImpl(VelloGeometryImplBase source, Matrix transform)
        : base(CreateData(source, transform), source.EffectiveFillRule)
    {
        SourceGeometry = source ?? throw new ArgumentNullException(nameof(source));
        Transform = transform;
    }

    public IGeometryImpl SourceGeometry { get; }

    public Matrix Transform { get; }

    private static VelloPathData CreateData(VelloGeometryImplBase source, Matrix transform)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var commands = source.GetCommandsSnapshot();
        if (transform.IsIdentity)
        {
            var clone = new VelloPathData();
            foreach (var command in commands)
            {
                AppendCommand(clone, command);
            }
            return clone;
        }

        var transformed = new VelloPathData();
        foreach (var command in commands)
        {
            AppendTransformedCommand(transformed, command, transform);
        }
        return transformed;
    }

    private static void AppendCommand(VelloPathData target, VelloPathData.PathCommand command)
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

    private static void AppendTransformedCommand(VelloPathData target, VelloPathData.PathCommand command, Matrix transform)
    {
        switch (command.Verb)
        {
            case VelloPathVerb.MoveTo:
            {
                var pt = transform.Transform(command.Point0);
                target.MoveTo(pt.X, pt.Y);
                break;
            }
            case VelloPathVerb.LineTo:
            {
                var pt = transform.Transform(command.Point0);
                target.LineTo(pt.X, pt.Y);
                break;
            }
            case VelloPathVerb.QuadTo:
            {
                var control = transform.Transform(command.Point0);
                var end = transform.Transform(command.Point1);
                target.QuadraticTo(control.X, control.Y, end.X, end.Y);
                break;
            }
            case VelloPathVerb.CubicTo:
            {
                var c1 = transform.Transform(command.Point0);
                var c2 = transform.Transform(command.Point1);
                var end = transform.Transform(command.Point2);
                target.CubicTo(c1.X, c1.Y, c2.X, c2.Y, end.X, end.Y);
                break;
            }
            case VelloPathVerb.Close:
                target.Close();
                break;
        }
    }
}
