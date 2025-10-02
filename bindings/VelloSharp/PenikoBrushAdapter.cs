using System;
using System.Buffers;
using System.Collections.Generic;

namespace VelloSharp;

public sealed class PenikoBrushAdapter : Brush
{
    private readonly PenikoBrush _brush;

    internal PenikoBrushAdapter(PenikoBrush brush)
    {
        _brush = brush ?? throw new ArgumentNullException(nameof(brush));
    }

    public PenikoBrush Brush => _brush;

    internal BrushNativeData CreateNative()
    {
        return _brush.Kind switch
        {
            PenikoBrushKind.Solid => CreateSolidBrush(),
            PenikoBrushKind.Gradient => CreateGradientBrush(),
            PenikoBrushKind.Image => throw new NotSupportedException("Image brushes are not supported via Peniko interop yet."),
            _ => throw new InvalidOperationException($"Unsupported Peniko brush kind: {_brush.Kind}.")
        };
    }

    private BrushNativeData CreateSolidBrush()
    {
        var color = _brush.GetSolidColor();
        var native = new VelloBrush
        {
            Kind = VelloBrushKind.Solid,
            Solid = color,
            Linear = default,
            Radial = default,
            Sweep = default,
            Image = default,
        };
        return new BrushNativeData(native, null, 0, pooled: false);
    }

    private BrushNativeData CreateGradientBrush()
    {
        var gradientKind = _brush.GetGradientKind();
        return gradientKind switch
        {
            PenikoGradientKind.Linear => CreateLinearGradientBrush(),
            PenikoGradientKind.Radial => CreateRadialGradientBrush(),
            PenikoGradientKind.Sweep => CreateSweepGradientBrush(),
            _ => throw new InvalidOperationException($"Unsupported Peniko gradient kind: {gradientKind}.")
        };
    }

    private BrushNativeData CreateLinearGradientBrush()
    {
        var info = _brush.GetLinearGradient();
        var stops = RentStops(info.Stops, out var stopCount, out var pooled);
        var native = new VelloBrush
        {
            Kind = VelloBrushKind.LinearGradient,
            Linear = new VelloLinearGradient
            {
                Start = ToVelloPoint(info.Gradient.Start),
                End = ToVelloPoint(info.Gradient.End),
                Extend = (VelloExtendMode)info.Extend,
                StopCount = (nuint)stopCount,
            },
            Radial = default,
            Sweep = default,
            Image = default,
        };
        return new BrushNativeData(native, stops, stopCount, pooled);
    }

    private BrushNativeData CreateRadialGradientBrush()
    {
        var info = _brush.GetRadialGradient();
        var stops = RentStops(info.Stops, out var stopCount, out var pooled);
        var native = new VelloBrush
        {
            Kind = VelloBrushKind.RadialGradient,
            Radial = new VelloRadialGradient
            {
                StartCenter = ToVelloPoint(info.Gradient.StartCenter),
                StartRadius = info.Gradient.StartRadius,
                EndCenter = ToVelloPoint(info.Gradient.EndCenter),
                EndRadius = info.Gradient.EndRadius,
                Extend = (VelloExtendMode)info.Extend,
                StopCount = (nuint)stopCount,
            },
            Linear = default,
            Sweep = default,
            Image = default,
        };
        return new BrushNativeData(native, stops, stopCount, pooled);
    }

    private BrushNativeData CreateSweepGradientBrush()
    {
        var info = _brush.GetSweepGradient();
        var stops = RentStops(info.Stops, out var stopCount, out var pooled);
        var native = new VelloBrush
        {
            Kind = VelloBrushKind.SweepGradient,
            Linear = default,
            Radial = default,
            Sweep = new VelloSweepGradient
            {
                Center = ToVelloPoint(info.Gradient.Center),
                StartAngle = info.Gradient.StartAngle,
                EndAngle = info.Gradient.EndAngle,
                Extend = (VelloExtendMode)info.Extend,
                StopCount = (nuint)stopCount,
            },
            Image = default,
        };
        return new BrushNativeData(native, stops, stopCount, pooled);
    }

    private static VelloGradientStop[]? RentStops(IReadOnlyList<PenikoColorStop> stops, out int count, out bool pooled)
    {
        if (stops is null || stops.Count == 0)
        {
            count = 0;
            pooled = false;
            return null;
        }

        count = stops.Count;
        var buffer = ArrayPool<VelloGradientStop>.Shared.Rent(count);
        var span = buffer.AsSpan(0, count);
        for (var i = 0; i < count; i++)
        {
            var stop = stops[i];
            span[i] = new VelloGradientStop
            {
                Offset = stop.Offset,
                Color = stop.Color,
            };
        }

        pooled = true;
        return buffer;
    }

    private static VelloPoint ToVelloPoint(PenikoPoint point) => new()
    {
        X = point.X,
        Y = point.Y,
    };
}

public static class BrushFactory
{
    public static Brush FromPenikoBrush(PenikoBrush brush)
    {
        ArgumentNullException.ThrowIfNull(brush);
        return new PenikoBrushAdapter(brush);
    }
}
