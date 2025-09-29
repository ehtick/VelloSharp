using System;
using System.Buffers;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace VelloSharp;

public enum FillRule
{
    NonZero = VelloFillRule.NonZero,
    EvenOdd = VelloFillRule.EvenOdd,
}

public enum LineCap
{
    Butt = VelloLineCap.Butt,
    Round = VelloLineCap.Round,
    Square = VelloLineCap.Square,
}

public enum LineJoin
{
    Miter = VelloLineJoin.Miter,
    Round = VelloLineJoin.Round,
    Bevel = VelloLineJoin.Bevel,
}

public enum AntialiasingMode
{
    Area = VelloAaMode.Area,
    Msaa8 = VelloAaMode.Msaa8,
    Msaa16 = VelloAaMode.Msaa16,
}

public enum RenderFormat
{
    Rgba8 = VelloRenderFormat.Rgba8,
    Bgra8 = VelloRenderFormat.Bgra8,
}

public enum PresentMode
{
    AutoVsync = VelloPresentMode.AutoVsync,
    AutoNoVsync = VelloPresentMode.AutoNoVsync,
    Fifo = VelloPresentMode.Fifo,
    Immediate = VelloPresentMode.Immediate,
}

public enum ImageAlphaMode
{
    Straight = VelloImageAlphaMode.Straight,
    Premultiplied = VelloImageAlphaMode.Premultiplied,
}

public enum ExtendMode
{
    Pad = VelloExtendMode.Pad,
    Repeat = VelloExtendMode.Repeat,
    Reflect = VelloExtendMode.Reflect,
}

public enum ImageQuality
{
    Low = VelloImageQualityMode.Low,
    Medium = VelloImageQualityMode.Medium,
    High = VelloImageQualityMode.High,
}

public enum LayerMix
{
    Normal = VelloBlendMix.Normal,
    Multiply = VelloBlendMix.Multiply,
    Screen = VelloBlendMix.Screen,
    Overlay = VelloBlendMix.Overlay,
    Darken = VelloBlendMix.Darken,
    Lighten = VelloBlendMix.Lighten,
    ColorDodge = VelloBlendMix.ColorDodge,
    ColorBurn = VelloBlendMix.ColorBurn,
    HardLight = VelloBlendMix.HardLight,
    SoftLight = VelloBlendMix.SoftLight,
    Difference = VelloBlendMix.Difference,
    Exclusion = VelloBlendMix.Exclusion,
    Hue = VelloBlendMix.Hue,
    Saturation = VelloBlendMix.Saturation,
    Color = VelloBlendMix.Color,
    Luminosity = VelloBlendMix.Luminosity,
    Clip = VelloBlendMix.Clip,
}

public enum LayerCompose
{
    Clear = VelloBlendCompose.Clear,
    Copy = VelloBlendCompose.Copy,
    Dest = VelloBlendCompose.Dest,
    SrcOver = VelloBlendCompose.SrcOver,
    DestOver = VelloBlendCompose.DestOver,
    SrcIn = VelloBlendCompose.SrcIn,
    DestIn = VelloBlendCompose.DestIn,
    SrcOut = VelloBlendCompose.SrcOut,
    DestOut = VelloBlendCompose.DestOut,
    SrcAtop = VelloBlendCompose.SrcAtop,
    DestAtop = VelloBlendCompose.DestAtop,
    Xor = VelloBlendCompose.Xor,
    Plus = VelloBlendCompose.Plus,
    PlusLighter = VelloBlendCompose.PlusLighter,
}

public enum GlyphRunStyle
{
    Fill = VelloGlyphRunStyle.Fill,
    Stroke = VelloGlyphRunStyle.Stroke,
}

public readonly record struct RgbaColor(float R, float G, float B, float A)
{
    public static RgbaColor FromBytes(byte r, byte g, byte b, byte a = 255)
    {
        const float Scale = 1f / 255f;
        return new RgbaColor(r * Scale, g * Scale, b * Scale, a * Scale);
    }
}

public readonly record struct GradientStop(float Offset, RgbaColor Color)
{
    public float Offset { get; init; } = Offset;
    public RgbaColor Color { get; init; } = Color;
}

internal readonly struct BrushNativeData : IDisposable
{
    private readonly VelloGradientStop[]? _stops;
    private readonly int _stopCount;
    private readonly bool _pooled;

    public BrushNativeData(VelloBrush brush, VelloGradientStop[]? stops, int stopCount, bool pooled)
    {
        Brush = brush;
        _stops = stops;
        _stopCount = stopCount;
        _pooled = pooled;
    }

    public VelloBrush Brush { get; }

    public ReadOnlySpan<VelloGradientStop> Stops =>
        _stops is { Length: > 0 } array && _stopCount > 0
            ? array.AsSpan(0, _stopCount)
            : ReadOnlySpan<VelloGradientStop>.Empty;

    public void Dispose()
    {
        if (_pooled && _stops is { })
        {
            ArrayPool<VelloGradientStop>.Shared.Return(_stops);
        }
    }
}

public abstract class Brush
{
    internal abstract BrushNativeData CreateNative();

    public static Brush FromPenikoBrush(PenikoBrush brush)
    {
        ArgumentNullException.ThrowIfNull(brush);
        return new PenikoBrushAdapter(brush);
    }
}

public sealed class SolidColorBrush : Brush
{
    public SolidColorBrush(RgbaColor color) => Color = color;

    public RgbaColor Color { get; }

    internal override BrushNativeData CreateNative()
    {
        var native = new VelloBrush
        {
            Kind = VelloBrushKind.Solid,
            Solid = Color.ToNative(),
            Linear = default,
            Radial = default,
            Sweep = default,
            Image = default,
        };
        return new BrushNativeData(native, null, 0, pooled: false);
    }
}

public sealed class LinearGradientBrush : Brush
{
    private readonly VelloGradientStop[] _stops;

    public LinearGradientBrush(Vector2 start, Vector2 end, IReadOnlyList<GradientStop> stops, ExtendMode extend = ExtendMode.Pad)
    {
        ArgumentNullException.ThrowIfNull(stops);
        if (stops.Count == 0)
        {
            throw new ArgumentException("At least one gradient stop is required.", nameof(stops));
        }

        Start = start;
        End = end;
        Extend = extend;
        _stops = new VelloGradientStop[stops.Count];
        for (var i = 0; i < stops.Count; i++)
        {
            var stop = stops[i];
            _stops[i] = new VelloGradientStop
            {
                Offset = stop.Offset,
                Color = stop.Color.ToNative(),
            };
        }
    }

    public Vector2 Start { get; }
    public Vector2 End { get; }
    public ExtendMode Extend { get; }

    internal override BrushNativeData CreateNative()
    {
        var native = new VelloBrush
        {
            Kind = VelloBrushKind.LinearGradient,
            Linear = new VelloLinearGradient
            {
                Start = Start.ToNativePoint(),
                End = End.ToNativePoint(),
                Extend = (VelloExtendMode)Extend,
                StopCount = (nuint)_stops.Length,
            },
            Radial = default,
            Sweep = default,
            Image = default,
        };
        return new BrushNativeData(native, _stops, _stops.Length, pooled: false);
    }
}

public sealed class RadialGradientBrush : Brush
{
    private readonly VelloGradientStop[] _stops;

    public RadialGradientBrush(
        Vector2 startCenter,
        float startRadius,
        Vector2 endCenter,
        float endRadius,
        IReadOnlyList<GradientStop> stops,
        ExtendMode extend = ExtendMode.Pad)
    {
        ArgumentNullException.ThrowIfNull(stops);
        if (stops.Count == 0)
        {
            throw new ArgumentException("At least one gradient stop is required.", nameof(stops));
        }
        if (startRadius < 0f || endRadius < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(startRadius), "Radii must be non-negative.");
        }

        StartCenter = startCenter;
        StartRadius = startRadius;
        EndCenter = endCenter;
        EndRadius = endRadius;
        Extend = extend;
        _stops = new VelloGradientStop[stops.Count];
        for (var i = 0; i < stops.Count; i++)
        {
            var stop = stops[i];
            _stops[i] = new VelloGradientStop
            {
                Offset = stop.Offset,
                Color = stop.Color.ToNative(),
            };
        }
    }

    public Vector2 StartCenter { get; }
    public float StartRadius { get; }
    public Vector2 EndCenter { get; }
    public float EndRadius { get; }
    public ExtendMode Extend { get; }

    internal override BrushNativeData CreateNative()
    {
        var native = new VelloBrush
        {
            Kind = VelloBrushKind.RadialGradient,
            Radial = new VelloRadialGradient
            {
                StartCenter = StartCenter.ToNativePoint(),
                StartRadius = StartRadius,
                EndCenter = EndCenter.ToNativePoint(),
                EndRadius = EndRadius,
                Extend = (VelloExtendMode)Extend,
                StopCount = (nuint)_stops.Length,
            },
            Linear = default,
            Sweep = default,
            Image = default,
        };
        return new BrushNativeData(native, _stops, _stops.Length, pooled: false);
    }
}

public sealed class SweepGradientBrush : Brush
{
    private readonly VelloGradientStop[] _stops;

    public SweepGradientBrush(
        Vector2 center,
        float startAngle,
        float endAngle,
        IReadOnlyList<GradientStop> stops,
        ExtendMode extend = ExtendMode.Pad)
    {
        ArgumentNullException.ThrowIfNull(stops);
        if (stops.Count == 0)
        {
            throw new ArgumentException("At least one gradient stop is required.", nameof(stops));
        }
        if (!float.IsFinite(startAngle) || !float.IsFinite(endAngle))
        {
            throw new ArgumentException("Sweep gradient angles must be finite values.");
        }

        Center = center;
        StartAngle = startAngle;
        EndAngle = endAngle;
        Extend = extend;
        _stops = new VelloGradientStop[stops.Count];
        for (var i = 0; i < stops.Count; i++)
        {
            var stop = stops[i];
            _stops[i] = new VelloGradientStop
            {
                Offset = stop.Offset,
                Color = stop.Color.ToNative(),
            };
        }
    }

    public Vector2 Center { get; }
    public float StartAngle { get; }
    public float EndAngle { get; }
    public ExtendMode Extend { get; }

    internal override BrushNativeData CreateNative()
    {
        var native = new VelloBrush
        {
            Kind = VelloBrushKind.SweepGradient,
            Linear = default,
            Radial = default,
            Sweep = new VelloSweepGradient
            {
                Center = Center.ToNativePoint(),
                StartAngle = StartAngle,
                EndAngle = EndAngle,
                Extend = (VelloExtendMode)Extend,
                StopCount = (nuint)_stops.Length,
            },
            Image = default,
        };
        return new BrushNativeData(native, _stops, _stops.Length, pooled: false);
    }
}

public sealed class ImageBrush : Brush
{
    public ImageBrush(Image image)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
    }

    public Image Image { get; }
    public ExtendMode XExtend { get; set; } = ExtendMode.Pad;
    public ExtendMode YExtend { get; set; } = ExtendMode.Pad;
    public ImageQuality Quality { get; set; } = ImageQuality.Medium;
    public float Alpha { get; set; } = 1f;

    internal override BrushNativeData CreateNative()
    {
        var native = new VelloBrush
        {
            Kind = VelloBrushKind.Image,
            Linear = default,
            Radial = default,
            Sweep = default,
            Image = new VelloImageBrushParams
            {
                Image = Image.Handle,
                XExtend = (VelloExtendMode)XExtend,
                YExtend = (VelloExtendMode)YExtend,
                Quality = (VelloImageQualityMode)Quality,
                Alpha = Alpha,
            },
        };
        return new BrushNativeData(native, null, 0, pooled: false);
    }

    internal VelloImageBrushParams ToNative() => new()
    {
        Image = Image.Handle,
        XExtend = (VelloExtendMode)XExtend,
        YExtend = (VelloExtendMode)YExtend,
        Quality = (VelloImageQualityMode)Quality,
        Alpha = Alpha,
    };
}

public sealed class PenikoBrushAdapter : Brush
{
    private readonly PenikoBrush _brush;

    internal PenikoBrushAdapter(PenikoBrush brush)
    {
        _brush = brush ?? throw new ArgumentNullException(nameof(brush));
    }

    public PenikoBrush Brush => _brush;

    internal override BrushNativeData CreateNative()
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
        };
        return new BrushNativeData(native, null, 0, pooled: false);
    }

    private BrushNativeData CreateGradientBrush()
    {
        var kind = _brush.GetGradientKind() ?? throw new InvalidOperationException("Peniko gradient kind was not available.");
        return kind switch
        {
            PenikoGradientKind.Linear => CreateLinearGradient(),
            PenikoGradientKind.Radial => CreateRadialGradient(),
            PenikoGradientKind.Sweep => CreateSweepGradient(),
            _ => throw new InvalidOperationException($"Unsupported Peniko gradient kind: {kind}.")
        };
    }

    private BrushNativeData CreateLinearGradient()
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

    private BrushNativeData CreateRadialGradient()
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

    private BrushNativeData CreateSweepGradient()
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

internal static class NativeConversionExtensions
{
    public static VelloColor ToNative(this RgbaColor color) => new()
    {
        R = color.R,
        G = color.G,
        B = color.B,
        A = color.A,
    };

    public static VelloPoint ToNativePoint(this Vector2 point) => new()
    {
        X = point.X,
        Y = point.Y,
    };

    public static VelloAffine ToNativeAffine(this Matrix3x2 matrix) => new()
    {
        M11 = matrix.M11,
        M12 = matrix.M12,
        M21 = matrix.M21,
        M22 = matrix.M22,
        Dx = matrix.M31,
        Dy = matrix.M32,
    };
}

public readonly struct RendererOptions
{
    public RendererOptions(
        bool useCpu = false,
        bool supportArea = true,
        bool supportMsaa8 = true,
        bool supportMsaa16 = true,
        int? initThreads = null,
        WgpuPipelineCache? pipelineCache = null)
    {
        UseCpu = useCpu;
        SupportArea = supportArea;
        SupportMsaa8 = supportMsaa8;
        SupportMsaa16 = supportMsaa16;
        InitThreads = initThreads;
        PipelineCache = pipelineCache;
    }

    public bool UseCpu { get; }
    public bool SupportArea { get; }
    public bool SupportMsaa8 { get; }
    public bool SupportMsaa16 { get; }
    public int? InitThreads { get; }
    public WgpuPipelineCache? PipelineCache { get; }

    internal VelloRendererOptions ToNative() => new()
    {
        UseCpu = UseCpu,
        SupportArea = SupportArea,
        SupportMsaa8 = SupportMsaa8,
        SupportMsaa16 = SupportMsaa16,
        InitThreads = InitThreads ?? 0,
        PipelineCache = PipelineCache?.Handle ?? IntPtr.Zero,
    };
}

public readonly struct LayerBlend
{
    public LayerBlend(LayerMix mix, LayerCompose compose)
    {
        Mix = mix;
        Compose = compose;
    }

    public LayerMix Mix { get; }
    public LayerCompose Compose { get; }
}

public readonly record struct GlyphMetrics(float Advance, float XBearing, float YBearing, float Width, float Height)
{
    public float Advance { get; init; } = Advance;
    public float XBearing { get; init; } = XBearing;
    public float YBearing { get; init; } = YBearing;
    public float Width { get; init; } = Width;
    public float Height { get; init; } = Height;
}

public readonly struct Glyph
{
    public Glyph(uint id, float x, float y)
    {
        Id = id;
        X = x;
        Y = y;
    }

    public uint Id { get; }
    public float X { get; }
    public float Y { get; }

    internal VelloGlyph ToNative() => new()
    {
        Id = Id,
        X = X,
        Y = Y,
    };
}

public sealed class GlyphRunOptions
{
    public Brush Brush { get; set; } = new SolidColorBrush(RgbaColor.FromBytes(0, 0, 0));
    public float FontSize { get; set; } = 16f;
    public bool Hint { get; set; }
    public GlyphRunStyle Style { get; set; } = GlyphRunStyle.Fill;
    public StrokeStyle? Stroke { get; set; }
    public float BrushAlpha { get; set; } = 1f;
    public Matrix3x2 Transform { get; set; } = Matrix3x2.Identity;
    public Matrix3x2? GlyphTransform { get; set; }
}

public sealed class Image : IDisposable
{
    private IntPtr _handle;

    private Image(IntPtr handle)
    {
        _handle = handle;
    }

    public IntPtr Handle
    {
        get
        {
            if (_handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(Image));
            }
            return _handle;
        }
    }

    public static Image FromPixels(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        RenderFormat format = RenderFormat.Rgba8,
        ImageAlphaMode alphaMode = ImageAlphaMode.Straight,
        int stride = 0)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }
        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }
        var bytesPerRow = checked(width * 4);
        if (stride == 0)
        {
            stride = bytesPerRow;
        }
        if (stride < bytesPerRow)
        {
            throw new ArgumentOutOfRangeException(nameof(stride));
        }
        var required = checked(stride * height);
        if (pixels.Length < required)
        {
            throw new ArgumentException("Pixel data is smaller than expected for the provided stride and dimensions.", nameof(pixels));
        }

        var slice = pixels[..required];
        unsafe
        {
            fixed (byte* ptr = slice)
            {
                var native = NativeMethods.vello_image_create(
                    (VelloRenderFormat)format,
                    (VelloImageAlphaMode)alphaMode,
                    (uint)width,
                    (uint)height,
                    (IntPtr)ptr,
                    (nuint)stride);
                if (native == IntPtr.Zero)
                {
                    throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create image.");
                }
                return new Image(native);
            }
        }
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_image_destroy(_handle);
            _handle = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }
    }

    ~Image()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_image_destroy(_handle);
        }
    }
}

public sealed class Font : IDisposable
{
    private IntPtr _handle;

    private Font(IntPtr handle)
    {
        _handle = handle;
    }

    public IntPtr Handle
    {
        get
        {
            if (_handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(Font));
            }
            return _handle;
        }
    }

    public static Font Load(ReadOnlySpan<byte> fontData, uint index = 0)
    {
        unsafe
        {
            fixed (byte* ptr = fontData)
            {
                var native = NativeMethods.vello_font_create((IntPtr)ptr, (nuint)fontData.Length, index);
                if (native == IntPtr.Zero)
                {
                    throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to load font.");
                }
                return new Font(native);
            }
        }
    }

    public bool TryGetGlyphIndex(uint codePoint, out ushort glyphId)
    {
        var status = NativeMethods.vello_font_get_glyph_index(Handle, codePoint, out glyphId);
        return status == VelloStatus.Success;
    }

    public bool TryGetGlyphMetrics(ushort glyphId, float fontSize, out GlyphMetrics metrics)
    {
        var status = NativeMethods.vello_font_get_glyph_metrics(Handle, glyphId, fontSize, out var native);
        if (status != VelloStatus.Success)
        {
            metrics = default;
            return false;
        }

        metrics = new GlyphMetrics(native.Advance, native.XBearing, native.YBearing, native.Width, native.Height);
        return true;
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_font_destroy(_handle);
            _handle = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }
    }

    ~Font()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_font_destroy(_handle);
        }
    }
}

public sealed class StrokeStyle
{
    public double Width { get; set; } = 1.0;
    public double MiterLimit { get; set; } = 4.0;
    public LineCap StartCap { get; set; } = LineCap.Butt;
    public LineCap EndCap { get; set; } = LineCap.Butt;
    public LineJoin LineJoin { get; set; } = LineJoin.Miter;
    public double DashPhase { get; set; }
    public double[]? DashPattern { get; set; }
}

public readonly record struct RenderParams(
    uint Width,
    uint Height,
    RgbaColor BaseColor,
    AntialiasingMode Antialiasing = AntialiasingMode.Area,
    RenderFormat Format = RenderFormat.Rgba8)
{
    public uint Width { get; init; } = Width;
    public uint Height { get; init; } = Height;
    public RgbaColor BaseColor { get; init; } = BaseColor;
    public AntialiasingMode Antialiasing { get; init; } = Antialiasing;
    public RenderFormat Format { get; init; } = Format;
}
