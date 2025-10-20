using System;
using System.Collections.Generic;

namespace VelloSharp;

public sealed class PenikoBrush : IDisposable
{
    private nint _handle;

    private PenikoBrush(nint handle)
    {
        if (handle == nint.Zero)
        {
            throw new InvalidOperationException("Failed to create Peniko brush.");
        }

        _handle = handle;
    }

    public static PenikoBrush CreateSolid(VelloColor color)
    {
        var handle = PenikoNativeMethods.peniko_brush_create_solid(color);
        return new PenikoBrush(handle);
    }

    public static PenikoBrush CreateLinear(PenikoLinearGradient gradient, PenikoExtend extend, ReadOnlySpan<PenikoColorStop> stops)
    {
        unsafe
        {
            fixed (PenikoColorStop* ptr = stops)
            {
                var handle = PenikoNativeMethods.peniko_brush_create_linear(gradient, extend, ptr, (nuint)stops.Length);
                return new PenikoBrush(handle);
            }
        }
    }

    public static PenikoBrush CreateRadial(PenikoRadialGradient gradient, PenikoExtend extend, ReadOnlySpan<PenikoColorStop> stops)
    {
        unsafe
        {
            fixed (PenikoColorStop* ptr = stops)
            {
                var handle = PenikoNativeMethods.peniko_brush_create_radial(gradient, extend, ptr, (nuint)stops.Length);
                return new PenikoBrush(handle);
            }
        }
    }

    public static PenikoBrush CreateSweep(PenikoSweepGradient gradient, PenikoExtend extend, ReadOnlySpan<PenikoColorStop> stops)
    {
        unsafe
        {
            fixed (PenikoColorStop* ptr = stops)
            {
                var handle = PenikoNativeMethods.peniko_brush_create_sweep(gradient, extend, ptr, (nuint)stops.Length);
                return new PenikoBrush(handle);
            }
        }
    }

    public static PenikoBrush CreateImage(PenikoImageData image, PenikoExtend xExtend = PenikoExtend.Pad, PenikoExtend yExtend = PenikoExtend.Pad, PenikoImageQuality quality = PenikoImageQuality.Medium, float alpha = 1f)
    {
        ArgumentNullException.ThrowIfNull(image);

        var parameters = new PenikoImageBrushParams
        {
            Image = image.DangerousGetHandle(),
            XExtend = xExtend,
            YExtend = yExtend,
            Quality = quality,
            Alpha = alpha,
        };
        var handle = PenikoNativeMethods.peniko_brush_create_image(parameters);
        return new PenikoBrush(handle);
    }

    public static PenikoBrush CreateImage(Image image, PenikoExtend xExtend = PenikoExtend.Pad, PenikoExtend yExtend = PenikoExtend.Pad, PenikoImageQuality quality = PenikoImageQuality.Medium, float alpha = 1f)
    {
        ArgumentNullException.ThrowIfNull(image);

        using var penikoImage = PenikoImageData.FromImage(image);
        return CreateImage(penikoImage, xExtend, yExtend, quality, alpha);
    }

    public PenikoBrush Clone()
    {
        var clone = PenikoNativeMethods.peniko_brush_clone(_handle);
        return new PenikoBrush(clone);
    }

    public PenikoBrushKind Kind
    {
        get
        {
            NativeHelpers.ThrowOnError(PenikoNativeMethods.peniko_brush_get_kind(_handle, out var kind), "peniko_brush_get_kind");
            return kind;
        }
    }

    public VelloColor GetSolidColor()
    {
        NativeHelpers.ThrowOnError(PenikoNativeMethods.peniko_brush_get_solid_color(_handle, out var color), "peniko_brush_get_solid_color");
        return color;
    }

    public PenikoGradientKind? GetGradientKind()
    {
        NativeHelpers.ThrowOnError(PenikoNativeMethods.peniko_brush_get_kind(_handle, out var kind), "peniko_brush_get_kind");
        if (kind != PenikoBrushKind.Gradient)
        {
            return null;
        }

        NativeHelpers.ThrowOnError(PenikoNativeMethods.peniko_brush_get_gradient_kind(_handle, out var gradientKind), "peniko_brush_get_gradient_kind");
        return gradientKind;
    }

    public PenikoLinearGradientInfo GetLinearGradient()
    {
        EnsureGradientKind(PenikoGradientKind.Linear);
        var info = GetStops(QueryLinearGradient);
        return new PenikoLinearGradientInfo(info.Gradient.Linear, info.Extend, info.Stops);
    }

    public PenikoRadialGradientInfo GetRadialGradient()
    {
        EnsureGradientKind(PenikoGradientKind.Radial);
        var info = GetStops(QueryRadialGradient);
        return new PenikoRadialGradientInfo(info.Gradient.Radial, info.Extend, info.Stops);
    }

    public PenikoSweepGradientInfo GetSweepGradient()
    {
        EnsureGradientKind(PenikoGradientKind.Sweep);
        var info = GetStops(QuerySweepGradient);
        return new PenikoSweepGradientInfo(info.Gradient.Sweep, info.Extend, info.Stops);
    }

    public PenikoImageBrushInfo GetImage()
    {
        NativeHelpers.ThrowOnError(PenikoNativeMethods.peniko_brush_get_image(_handle, out var parameters, out var imageHandle), "peniko_brush_get_image");
        if (imageHandle == nint.Zero)
        {
            throw new InvalidOperationException("Failed to retrieve Peniko image brush data.");
        }

        var image = PenikoImageData.FromNativeHandle(imageHandle);
        return new PenikoImageBrushInfo(image, parameters.XExtend, parameters.YExtend, parameters.Quality, parameters.Alpha);
    }

    public void WithAlpha(float alpha)
    {
        NativeHelpers.ThrowOnError(PenikoNativeMethods.peniko_brush_with_alpha(_handle, alpha), "peniko_brush_with_alpha");
    }

    public void MultiplyAlpha(float alpha)
    {
        NativeHelpers.ThrowOnError(PenikoNativeMethods.peniko_brush_multiply_alpha(_handle, alpha), "peniko_brush_multiply_alpha");
    }

    public void Dispose()
    {
        if (_handle != nint.Zero)
        {
            PenikoNativeMethods.peniko_brush_destroy(_handle);
            _handle = nint.Zero;
        }

        GC.SuppressFinalize(this);
    }

    ~PenikoBrush()
    {
        Dispose();
    }

    internal nint DangerousGetHandle() => _handle;

    private void EnsureGradientKind(PenikoGradientKind expected)
    {
        var kind = GetGradientKind();
        if (kind != expected)
        {
            throw new InvalidOperationException($"Brush gradient kind is {kind}, expected {expected}.");
        }
    }

    private unsafe PenikoGradientInfo GetStops(PenikoGradientQuery query)
    {
        nuint length;
        var gradient = new GradientUnion();
        var extend = new ExtendHolder();
        var status = query(_handle, ref gradient, ref extend, nint.Zero, 0, out length);
        if (status != PenikoStatus.Success && status != PenikoStatus.InvalidArgument)
        {
            NativeHelpers.ThrowOnError(status, "peniko gradient query");
        }

        if (length == 0)
        {
            return new PenikoGradientInfo(gradient, extend.Value, Array.Empty<PenikoColorStop>());
        }

        var stops = new PenikoColorStop[(int)length];
        unsafe
        {
            fixed (PenikoColorStop* ptr = stops)
            {
                NativeHelpers.ThrowOnError(query(_handle, ref gradient, ref extend, (nint)ptr, length, out var written), "peniko gradient query");
                if (written != length)
                {
                    Array.Resize(ref stops, (int)written);
                }
            }
        }

        return new PenikoGradientInfo(gradient, extend.Value, stops);
    }

    private unsafe delegate PenikoStatus PenikoGradientQuery(nint brush, ref GradientUnion gradient, ref ExtendHolder extend, nint stops, nuint capacity, out nuint length);

    private static unsafe PenikoStatus QueryLinearGradient(nint brush, ref GradientUnion gradient, ref ExtendHolder extend, nint stops, nuint capacity, out nuint length) =>
        PenikoNativeMethods.peniko_brush_get_linear_gradient(brush, out gradient.Linear, out extend.Value, (PenikoColorStop*)stops, capacity, out length);

    private static unsafe PenikoStatus QueryRadialGradient(nint brush, ref GradientUnion gradient, ref ExtendHolder extend, nint stops, nuint capacity, out nuint length) =>
        PenikoNativeMethods.peniko_brush_get_radial_gradient(brush, out gradient.Radial, out extend.Value, (PenikoColorStop*)stops, capacity, out length);

    private static unsafe PenikoStatus QuerySweepGradient(nint brush, ref GradientUnion gradient, ref ExtendHolder extend, nint stops, nuint capacity, out nuint length) =>
        PenikoNativeMethods.peniko_brush_get_sweep_gradient(brush, out gradient.Sweep, out extend.Value, (PenikoColorStop*)stops, capacity, out length);

    private struct GradientUnion
    {
        public PenikoLinearGradient Linear;
        public PenikoRadialGradient Radial;
        public PenikoSweepGradient Sweep;
    }

    private struct ExtendHolder
    {
        public PenikoExtend Value;
    }

    private readonly struct PenikoGradientInfo
    {
        public PenikoGradientInfo(GradientUnion gradient, PenikoExtend extend, PenikoColorStop[] stops)
        {
            Gradient = gradient;
            Extend = extend;
            Stops = stops;
        }

        public GradientUnion Gradient { get; }
        public PenikoExtend Extend { get; }
        public PenikoColorStop[] Stops { get; }
    }
}

public readonly record struct PenikoLinearGradientInfo(PenikoLinearGradient Gradient, PenikoExtend Extend, IReadOnlyList<PenikoColorStop> Stops);

public readonly record struct PenikoRadialGradientInfo(PenikoRadialGradient Gradient, PenikoExtend Extend, IReadOnlyList<PenikoColorStop> Stops);

public readonly record struct PenikoSweepGradientInfo(PenikoSweepGradient Gradient, PenikoExtend Extend, IReadOnlyList<PenikoColorStop> Stops);

public sealed class PenikoImageBrushInfo : IDisposable
{
    internal PenikoImageBrushInfo(PenikoImageData image, PenikoExtend xExtend, PenikoExtend yExtend, PenikoImageQuality quality, float alpha)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
        XExtend = xExtend;
        YExtend = yExtend;
        Quality = quality;
        Alpha = alpha;
    }

    public PenikoImageData Image { get; }
    public PenikoExtend XExtend { get; }
    public PenikoExtend YExtend { get; }
    public PenikoImageQuality Quality { get; }
    public float Alpha { get; }

    public void Dispose()
    {
        Image.Dispose();
        GC.SuppressFinalize(this);
    }
}
