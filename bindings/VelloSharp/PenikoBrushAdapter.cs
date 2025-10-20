using System;
using System.Buffers;

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
        var data = Serialize();
        return data.Brush.Kind switch
        {
            PenikoBrushKind.Solid => CreateSolidBrush(data.Brush.Solid),
            PenikoBrushKind.Gradient => CreateGradientBrush(data.Brush, data.Stops.AsSpan()),
            PenikoBrushKind.Image => CreateImageBrush(data.Brush.Image),
            _ => throw new InvalidOperationException($"Unsupported Peniko brush kind: {data.Brush.Kind}.")
        };
    }

    private SerializedBrushData Serialize()
    {
        unsafe
        {
            PenikoSerializedBrush brush;
            nuint length;
            var status = PenikoNativeMethods.peniko_brush_serialize(_brush.DangerousGetHandle(), out brush, (PenikoColorStop*)0, 0, out length);
            NativeHelpers.ThrowOnError(status, "peniko_brush_serialize");

            PenikoColorStop[] stops;
            if (length == 0)
            {
                stops = Array.Empty<PenikoColorStop>();
            }
            else
            {
                stops = new PenikoColorStop[(int)length];
                fixed (PenikoColorStop* ptr = stops)
                {
                    status = PenikoNativeMethods.peniko_brush_serialize(_brush.DangerousGetHandle(), out brush, ptr, (nuint)stops.Length, out var written);
                    NativeHelpers.ThrowOnError(status, "peniko_brush_serialize");
                    if (written != (nuint)stops.Length)
                    {
                        Array.Resize(ref stops, (int)written);
                    }
                }
            }

            return new SerializedBrushData(brush, stops);
        }
    }

    private BrushNativeData CreateSolidBrush(PenikoColor color)
    {
        var native = new VelloBrush
        {
            Kind = VelloBrushKind.Solid,
            Solid = new VelloColor { R = color.R, G = color.G, B = color.B, A = color.A },
        };
        return new BrushNativeData(native, null, 0, pooled: false);
    }

    private BrushNativeData CreateGradientBrush(PenikoSerializedBrush brush, ReadOnlySpan<PenikoColorStop> stops)
    {
        var gradientStops = RentStops(stops, out var stopCount, out var pooled);
        VelloBrush native;
        switch (brush.GradientKind)
        {
            case PenikoGradientKind.Linear:
                native = new VelloBrush
                {
                    Kind = VelloBrushKind.LinearGradient,
                    Linear = new VelloLinearGradient
                    {
                        Start = ToVelloPoint(brush.Linear.Start),
                        End = ToVelloPoint(brush.Linear.End),
                        Extend = (VelloExtendMode)brush.Extend,
                        StopCount = (nuint)stopCount,
                    },
                };
                break;
            case PenikoGradientKind.Radial:
                native = new VelloBrush
                {
                    Kind = VelloBrushKind.RadialGradient,
                    Radial = new VelloRadialGradient
                    {
                        StartCenter = ToVelloPoint(brush.Radial.StartCenter),
                        StartRadius = brush.Radial.StartRadius,
                        EndCenter = ToVelloPoint(brush.Radial.EndCenter),
                        EndRadius = brush.Radial.EndRadius,
                        Extend = (VelloExtendMode)brush.Extend,
                        StopCount = (nuint)stopCount,
                    },
                };
                break;
            case PenikoGradientKind.Sweep:
                native = new VelloBrush
                {
                    Kind = VelloBrushKind.SweepGradient,
                    Sweep = new VelloSweepGradient
                    {
                        Center = ToVelloPoint(brush.Sweep.Center),
                        StartAngle = brush.Sweep.StartAngle,
                        EndAngle = brush.Sweep.EndAngle,
                        Extend = (VelloExtendMode)brush.Extend,
                        StopCount = (nuint)stopCount,
                    },
                };
                break;
            default:
                gradientStops?.AsSpan().Clear();
                throw new InvalidOperationException($"Unsupported Peniko gradient kind: {brush.GradientKind}.");
        }

        return new BrushNativeData(native, gradientStops, stopCount, pooled);
    }

    private BrushNativeData CreateImageBrush(PenikoImageBrushParams parameters)
    {
        if (parameters.Image == IntPtr.Zero)
        {
            throw new InvalidOperationException("Serialized image brush is missing image data.");
        }

        using var imageData = PenikoImageData.FromNativeHandle(parameters.Image);
        var info = imageData.GetInfo();
        var bytesRequired = checked(info.Stride * info.Height);
        if (bytesRequired <= 0)
        {
            throw new InvalidOperationException("Serialized image brush has invalid dimensions.");
        }

        var buffer = ArrayPool<byte>.Shared.Rent(bytesRequired);
        try
        {
            var span = buffer.AsSpan(0, bytesRequired);
            imageData.CopyPixels(span);

            var renderFormat = info.Format switch
            {
                PenikoImageFormat.Rgba8 => RenderFormat.Rgba8,
                PenikoImageFormat.Bgra8 => RenderFormat.Bgra8,
                _ => RenderFormat.Rgba8,
            };

            var alphaMode = info.Alpha switch
            {
                PenikoImageAlphaType.Alpha => ImageAlphaMode.Straight,
                PenikoImageAlphaType.AlphaPremultiplied => ImageAlphaMode.Premultiplied,
                _ => ImageAlphaMode.Straight,
            };

            var image = Image.FromPixels(
                span,
                info.Width,
                info.Height,
                renderFormat,
                alphaMode,
                info.Stride);

            var native = new VelloBrush
            {
                Kind = VelloBrushKind.Image,
                Image = new VelloImageBrushParams
                {
                    Image = image.Handle,
                    XExtend = (VelloExtendMode)parameters.XExtend,
                    YExtend = (VelloExtendMode)parameters.YExtend,
                    Quality = (VelloImageQualityMode)parameters.Quality,
                    Alpha = parameters.Alpha,
                },
            };

            return new BrushNativeData(native, null, 0, pooled: false, image: image);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static GradientStop[]? RentStops(ReadOnlySpan<PenikoColorStop> stops, out int count, out bool pooled)
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
        for (var i = 0; i < count; i++)
        {
            var stop = stops[i];
            var color = stop.Color;
            span[i] = new GradientStop(stop.Offset, new RgbaColor(color.R, color.G, color.B, color.A));
        }

        pooled = true;
        return buffer;
    }

    private readonly struct SerializedBrushData
    {
        public SerializedBrushData(PenikoSerializedBrush brush, PenikoColorStop[] stops)
        {
            Brush = brush;
            Stops = stops;
        }

        public PenikoSerializedBrush Brush { get; }
        public PenikoColorStop[] Stops { get; }
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
