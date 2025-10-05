using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Numerics;
using VelloSharp;
using GdiLineCap = System.Drawing.Drawing2D.LineCap;
using GdiLineJoin = System.Drawing.Drawing2D.LineJoin;

namespace VelloSharp.WinForms;

public sealed class VelloPen : IDisposable
{
    private bool _disposed;
    private Color _color;
    private VelloBrush? _brush;

    public VelloPen(Color color, float width = 1f)
    {
        if (width <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Pen width must be greater than zero.");
        }

        _color = color;
        Width = width;
    }

    public VelloPen(VelloBrush brush, float width = 1f)
    {
        ArgumentNullException.ThrowIfNull(brush);
        if (width <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Pen width must be greater than zero.");
        }

        _brush = brush;
        _color = Color.Transparent;
        Width = width;
    }

    public float Width { get; set; }

    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            _brush = null;
        }
    }

    public VelloBrush? Brush
    {
        get => _brush;
        set
        {
            _brush = value;
            if (value is not null)
            {
                _color = Color.Transparent;
            }
        }
    }

    public GdiLineCap StartCap { get; set; } = GdiLineCap.Flat;

    public GdiLineCap EndCap { get; set; } = GdiLineCap.Flat;

    public GdiLineJoin LineJoin { get; set; } = GdiLineJoin.Miter;

    public float MiterLimit { get; set; } = 4f;

    public float DashOffset { get; set; }

    public float[]? DashPattern { get; set; }

    public PenAlignment Alignment { get; set; } = PenAlignment.Center;

    public Matrix3x2? BrushTransform
    {
        get => _brush?.Transform;
        set
        {
            if (_brush is not null && value.HasValue)
            {
                _brush.Transform = value.Value;
            }
        }
    }

    internal void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(VelloPen));
        }
    }

    internal StrokeStyle CreateStrokeStyle()
    {
        ThrowIfDisposed();

        double[]? dashArray = null;
        if (DashPattern is { Length: > 0 } pattern)
        {
            dashArray = new double[pattern.Length];
            for (var i = 0; i < pattern.Length; i++)
            {
                dashArray[i] = pattern[i];
            }
        }

        return new StrokeStyle
        {
            Width = Width,
            MiterLimit = MiterLimit,
            StartCap = ConvertLineCap(StartCap),
            EndCap = ConvertLineCap(EndCap),
            LineJoin = ConvertLineJoin(LineJoin),
            DashPhase = DashOffset,
            DashPattern = dashArray,
        };
    }

    internal bool TryGetStrokeBrush(out Brush brush, out Matrix3x2? transform)
    {
        ThrowIfDisposed();
        if (_brush is null)
        {
            brush = null!;
            transform = null;
            return false;
        }

        brush = _brush.CreateCoreBrush(out transform);
        return true;
    }

    internal RgbaColor GetStrokeColor()
    {
        ThrowIfDisposed();
        if (_brush is not null)
        {
            if (_brush is VelloSolidBrush solid)
            {
                return VelloColorHelpers.ToRgba(solid.Color);
            }

            throw new InvalidOperationException("Pen is configured with a brush-based fill; use TryGetStrokeBrush instead.");
        }

        return VelloColorHelpers.ToRgba(_color);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DashPattern = null;
    }

    private static VelloSharp.LineCap ConvertLineCap(GdiLineCap cap) => cap switch
    {
        GdiLineCap.Flat or GdiLineCap.NoAnchor => VelloSharp.LineCap.Butt,
        GdiLineCap.Square or GdiLineCap.SquareAnchor => VelloSharp.LineCap.Square,
        GdiLineCap.Round or GdiLineCap.RoundAnchor => VelloSharp.LineCap.Round,
        GdiLineCap.Triangle => VelloSharp.LineCap.Round,
        _ => VelloSharp.LineCap.Butt,
    };

    private static VelloSharp.LineJoin ConvertLineJoin(GdiLineJoin join) => join switch
    {
        GdiLineJoin.Bevel => VelloSharp.LineJoin.Bevel,
        GdiLineJoin.Round => VelloSharp.LineJoin.Round,
        GdiLineJoin.Miter or GdiLineJoin.MiterClipped => VelloSharp.LineJoin.Miter,
        _ => VelloSharp.LineJoin.Miter,
    };
}
