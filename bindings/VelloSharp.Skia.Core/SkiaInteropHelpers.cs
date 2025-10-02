using System;
using System.Buffers;
using System.Numerics;

namespace SkiaSharp;

internal static class StrokeInterop
{
    public static VelloSharp.VelloStrokeStyle Create(VelloSharp.StrokeStyle style, IntPtr dashPtr, nuint dashLength) => new()
    {
        Width = style.Width,
        MiterLimit = style.MiterLimit,
        StartCap = (VelloSharp.VelloLineCap)style.StartCap,
        EndCap = (VelloSharp.VelloLineCap)style.EndCap,
        LineJoin = (VelloSharp.VelloLineJoin)style.LineJoin,
        DashPhase = style.DashPhase,
        DashPattern = dashPtr,
        DashLength = dashLength,
    };
}

internal static class BrushInvoker
{
    public static unsafe VelloSharp.VelloBrush Prepare(VelloSharp.VelloBrush brush, ReadOnlySpan<VelloSharp.VelloGradientStop> stops, VelloSharp.VelloGradientStop* stopPtr)
    {
        brush.Linear.Stops = IntPtr.Zero;
        brush.Radial.Stops = IntPtr.Zero;
        brush.Sweep.Stops = IntPtr.Zero;

        if (!stops.IsEmpty && stopPtr is not null)
        {
            var ptr = (IntPtr)stopPtr;
            switch (brush.Kind)
            {
                case VelloSharp.VelloBrushKind.LinearGradient:
                    brush.Linear.Stops = ptr;
                    brush.Linear.StopCount = (nuint)stops.Length;
                    break;
                case VelloSharp.VelloBrushKind.RadialGradient:
                    brush.Radial.Stops = ptr;
                    brush.Radial.StopCount = (nuint)stops.Length;
                    break;
                case VelloSharp.VelloBrushKind.SweepGradient:
                    brush.Sweep.Stops = ptr;
                    brush.Sweep.StopCount = (nuint)stops.Length;
                    break;
            }
        }

        return brush;
    }
}

internal static class BrushNativeFactory
{
    public static BrushNativeData Create(VelloSharp.Brush brush)
    {
        ArgumentNullException.ThrowIfNull(brush);

        return brush switch
        {
            VelloSharp.ImageBrush image => CreateImageBrush(image),
            VelloSharp.SolidColorBrush solid => CreateSolidColorBrush(solid),
            VelloSharp.LinearGradientBrush linear => CreateLinearGradientBrush(linear),
            VelloSharp.RadialGradientBrush radial => CreateRadialGradientBrush(radial),
            VelloSharp.SweepGradientBrush sweep => CreateSweepGradientBrush(sweep),
            _ => throw new InvalidOperationException($"Unsupported brush type: {brush.GetType().FullName}"),
        };
    }

    private static BrushNativeData CreateSolidColorBrush(VelloSharp.SolidColorBrush brush)
    {
        var native = new VelloSharp.VelloBrush
        {
            Kind = VelloSharp.VelloBrushKind.Solid,
            Solid = brush.Color.ToNative(),
        };
        return new BrushNativeData(native, null, 0, pooled: false);
    }

    private static BrushNativeData CreateLinearGradientBrush(VelloSharp.LinearGradientBrush brush)
    {
        var stops = RentStops(brush.Stops, out var count, out var pooled);
        var native = new VelloSharp.VelloBrush
        {
            Kind = VelloSharp.VelloBrushKind.LinearGradient,
            Linear = new VelloSharp.VelloLinearGradient
            {
                Start = brush.Start.ToNativePoint(),
                End = brush.End.ToNativePoint(),
                Extend = (VelloSharp.VelloExtendMode)brush.Extend,
                StopCount = (nuint)count,
            },
        };
        return new BrushNativeData(native, stops, count, pooled);
    }

    private static BrushNativeData CreateRadialGradientBrush(VelloSharp.RadialGradientBrush brush)
    {
        var stops = RentStops(brush.Stops, out var count, out var pooled);
        var native = new VelloSharp.VelloBrush
        {
            Kind = VelloSharp.VelloBrushKind.RadialGradient,
            Radial = new VelloSharp.VelloRadialGradient
            {
                StartCenter = brush.StartCenter.ToNativePoint(),
                StartRadius = brush.StartRadius,
                EndCenter = brush.EndCenter.ToNativePoint(),
                EndRadius = brush.EndRadius,
                Extend = (VelloSharp.VelloExtendMode)brush.Extend,
                StopCount = (nuint)count,
            },
        };
        return new BrushNativeData(native, stops, count, pooled);
    }

    private static BrushNativeData CreateSweepGradientBrush(VelloSharp.SweepGradientBrush brush)
    {
        var stops = RentStops(brush.Stops, out var count, out var pooled);
        var native = new VelloSharp.VelloBrush
        {
            Kind = VelloSharp.VelloBrushKind.SweepGradient,
            Sweep = new VelloSharp.VelloSweepGradient
            {
                Center = brush.Center.ToNativePoint(),
                StartAngle = brush.StartAngle,
                EndAngle = brush.EndAngle,
                Extend = (VelloSharp.VelloExtendMode)brush.Extend,
                StopCount = (nuint)count,
            },
        };
        return new BrushNativeData(native, stops, count, pooled);
    }

    private static BrushNativeData CreateImageBrush(VelloSharp.ImageBrush brush)
    {
        var native = new VelloSharp.VelloBrush
        {
            Kind = VelloSharp.VelloBrushKind.Image,
            Image = brush.ToNative(),
        };
        return new BrushNativeData(native, null, 0, pooled: false);
    }

    private static VelloSharp.VelloGradientStop[]? RentStops(ReadOnlySpan<VelloSharp.GradientStop> stops, out int count, out bool pooled)
    {
        if (stops.IsEmpty)
        {
            count = 0;
            pooled = false;
            return null;
        }

        count = stops.Length;
        var buffer = ArrayPool<VelloSharp.VelloGradientStop>.Shared.Rent(count);
        var span = buffer.AsSpan(0, count);
        for (var i = 0; i < count; i++)
        {
            span[i] = stops[i].ToNative();
        }

        pooled = true;
        return buffer;
    }
}

internal readonly struct BrushNativeData : IDisposable
{
    private readonly VelloSharp.VelloGradientStop[]? _stops;
    private readonly int _count;
    private readonly bool _pooled;

    public BrushNativeData(VelloSharp.VelloBrush brush, VelloSharp.VelloGradientStop[]? stops, int count, bool pooled)
    {
        Brush = brush;
        _stops = stops;
        _count = count;
        _pooled = pooled;
    }

    public VelloSharp.VelloBrush Brush { get; }

    public ReadOnlySpan<VelloSharp.VelloGradientStop> Stops =>
        _stops is { Length: > 0 } array && _count > 0
            ? array.AsSpan(0, _count)
            : ReadOnlySpan<VelloSharp.VelloGradientStop>.Empty;

    public void Dispose()
    {
        if (_pooled && _stops is { })
        {
            ArrayPool<VelloSharp.VelloGradientStop>.Shared.Return(_stops);
        }
    }
}

internal static class NativeConversionExtensions
{
    public static VelloSharp.VelloColor ToNative(this VelloSharp.RgbaColor color) => new()
    {
        R = color.R,
        G = color.G,
        B = color.B,
        A = color.A,
    };

    public static VelloSharp.VelloPoint ToNativePoint(this Vector2 point) => new()
    {
        X = point.X,
        Y = point.Y,
    };

    public static VelloSharp.VelloAffine ToNativeAffine(this Matrix3x2 matrix) => new()
    {
        M11 = matrix.M11,
        M12 = matrix.M12,
        M21 = matrix.M21,
        M22 = matrix.M22,
        Dx = matrix.M31,
        Dy = matrix.M32,
    };

    public static VelloSharp.VelloGradientStop ToNative(this VelloSharp.GradientStop stop) => new()
    {
        Offset = stop.Offset,
        Color = stop.Color.ToNative(),
    };

    public static VelloSharp.VelloGlyph ToNative(this VelloSharp.Glyph glyph) => new()
    {
        Id = glyph.Id,
        X = glyph.X,
        Y = glyph.Y,
    };
}

internal readonly struct NativePathElements : IDisposable
{
    private readonly VelloSharp.VelloPathElement[]? _buffer;
    private readonly int _length;

    private NativePathElements(VelloSharp.VelloPathElement[]? buffer, int length)
    {
        _buffer = buffer;
        _length = length;
    }

    public ReadOnlySpan<VelloSharp.VelloPathElement> Span =>
        _buffer is null ? ReadOnlySpan<VelloSharp.VelloPathElement>.Empty : _buffer.AsSpan(0, _length);

    public static NativePathElements Rent(VelloSharp.PathBuilder path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var source = path.AsSpan();
        if (source.IsEmpty)
        {
            return new NativePathElements(null, 0);
        }

        var buffer = ArrayPool<VelloSharp.VelloPathElement>.Shared.Rent(source.Length);
        var span = buffer.AsSpan(0, source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            var element = source[i];
            span[i] = new VelloSharp.VelloPathElement
            {
                Verb = (VelloSharp.VelloPathVerb)element.Verb,
                X0 = element.X0,
                Y0 = element.Y0,
                X1 = element.X1,
                Y1 = element.Y1,
                X2 = element.X2,
                Y2 = element.Y2,
            };
        }

        return new NativePathElements(buffer, source.Length);
    }

    public void Dispose()
    {
        if (_buffer is { })
        {
            ArrayPool<VelloSharp.VelloPathElement>.Shared.Return(_buffer);
        }
    }
}
