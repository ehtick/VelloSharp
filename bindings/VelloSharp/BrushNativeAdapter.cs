using System;
using System.Buffers;
using System.Numerics;

namespace VelloSharp;

internal readonly struct BrushNativeData : IDisposable
{
    private readonly GradientStop[]? _stops;
    private readonly int _stopCount;
    private readonly bool _pooled;
    private readonly Image? _image;

    public BrushNativeData(VelloBrush brush, GradientStop[]? stops, int stopCount, bool pooled, Image? image = null)
    {
        Brush = brush;
        _stops = stops;
        _stopCount = stopCount;
        _pooled = pooled;
        _image = image;
    }

    public VelloBrush Brush { get; }

    public ReadOnlySpan<GradientStop> Stops =>
        _stops is { Length: > 0 } array && _stopCount > 0
            ? array.AsSpan(0, _stopCount)
            : ReadOnlySpan<GradientStop>.Empty;

    public void Dispose()
    {
        if (_pooled && _stops is { })
        {
            ArrayPool<GradientStop>.Shared.Return(_stops);
        }

        _image?.Dispose();
    }
}

internal static class BrushNativeFactory
{
    public static BrushNativeData Create(Brush brush)
    {
        ArgumentNullException.ThrowIfNull(brush);

        return brush switch
        {
            PenikoBrushAdapter adapter => adapter.CreateNative(),
            ImageBrush image => CreateImageBrush(image),
            SolidColorBrush solid => CreateSolidColorBrush(solid),
            LinearGradientBrush linear => CreateLinearGradientBrush(linear),
            RadialGradientBrush radial => CreateRadialGradientBrush(radial),
            SweepGradientBrush sweep => CreateSweepGradientBrush(sweep),
            _ => throw new InvalidOperationException($"Unsupported brush type: {brush.GetType().FullName}"),
        };
    }

    private static BrushNativeData CreateSolidColorBrush(SolidColorBrush brush)
    {
        var native = new VelloBrush
        {
            Kind = VelloBrushKind.Solid,
            Solid = brush.Color.ToNative(),
            Linear = default,
            Radial = default,
            Sweep = default,
            Image = default,
        };
        return new BrushNativeData(native, null, 0, pooled: false);
    }

    private static BrushNativeData CreateLinearGradientBrush(LinearGradientBrush brush)
    {
        var stops = brush.StopsArray;
        var count = stops.Length;
        var native = new VelloBrush
        {
            Kind = VelloBrushKind.LinearGradient,
            Linear = new VelloLinearGradient
            {
                Start = brush.Start.ToNativePoint(),
                End = brush.End.ToNativePoint(),
                Extend = (VelloExtendMode)brush.Extend,
                StopCount = (nuint)count,
            },
            Radial = default,
            Sweep = default,
            Image = default,
        };
        return new BrushNativeData(native, stops, count, pooled: false);
    }

    private static BrushNativeData CreateRadialGradientBrush(RadialGradientBrush brush)
    {
        var stops = brush.StopsArray;
        var count = stops.Length;
        var native = new VelloBrush
        {
            Kind = VelloBrushKind.RadialGradient,
            Radial = new VelloRadialGradient
            {
                StartCenter = brush.StartCenter.ToNativePoint(),
                StartRadius = brush.StartRadius,
                EndCenter = brush.EndCenter.ToNativePoint(),
                EndRadius = brush.EndRadius,
                Extend = (VelloExtendMode)brush.Extend,
                StopCount = (nuint)count,
            },
            Linear = default,
            Sweep = default,
            Image = default,
        };
        return new BrushNativeData(native, stops, count, pooled: false);
    }

    private static BrushNativeData CreateSweepGradientBrush(SweepGradientBrush brush)
    {
        var stops = brush.StopsArray;
        var count = stops.Length;
        var native = new VelloBrush
        {
            Kind = VelloBrushKind.SweepGradient,
            Sweep = new VelloSweepGradient
            {
                Center = brush.Center.ToNativePoint(),
                StartAngle = brush.StartAngle,
                EndAngle = brush.EndAngle,
                Extend = (VelloExtendMode)brush.Extend,
                StopCount = (nuint)count,
            },
            Linear = default,
            Radial = default,
            Image = default,
        };
        return new BrushNativeData(native, stops, count, pooled: false);
    }

    private static BrushNativeData CreateImageBrush(ImageBrush brush)
    {
        var native = new VelloBrush
        {
            Kind = VelloBrushKind.Image,
            Linear = default,
            Radial = default,
            Sweep = default,
            Image = new VelloImageBrushParams
            {
                Image = brush.Image.Handle,
                XExtend = (VelloExtendMode)brush.XExtend,
                YExtend = (VelloExtendMode)brush.YExtend,
                Quality = (VelloImageQualityMode)brush.Quality,
                Alpha = brush.Alpha,
            },
        };
        return new BrushNativeData(native, null, 0, pooled: false);
    }

    internal static GradientStop[]? RentStops(ReadOnlySpan<GradientStop> stops, out int count, out bool pooled)
    {
        if (stops.IsEmpty)
        {
            count = 0;
            pooled = false;
            return null;
        }

        count = stops.Length;
        var buffer = ArrayPool<GradientStop>.Shared.Rent(count);
        var span = buffer.AsSpan(0, count);
        stops.CopyTo(span);

        pooled = true;
        return buffer;
    }
}
