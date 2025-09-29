using System;
using VelloSharp;

namespace SkiaSharp;

public enum SKPaintStyle
{
    Fill,
    Stroke,
    StrokeAndFill,
}

public enum SKStrokeCap
{
    Butt,
    Round,
    Square,
}

public enum SKStrokeJoin
{
    Miter,
    Round,
    Bevel,
}

public sealed class SKPaint : IDisposable
{
    private bool _disposed;

    public SKPaintStyle Style { get; set; } = SKPaintStyle.Fill;
    public SKStrokeCap StrokeCap { get; set; } = SKStrokeCap.Butt;
    public SKStrokeJoin StrokeJoin { get; set; } = SKStrokeJoin.Miter;
    public float StrokeWidth { get; set; } = 1f;
    public float StrokeMiter { get; set; } = 4f;
    public bool IsAntialias { get; set; } = true;
    public float TextSize { get; set; } = 16f;
    public SKColor Color { get; set; } = new(0, 0, 0, 255);
    public SKTypeface? Typeface { get; set; }
    public float Opacity { get; set; } = 1f;

    public void Dispose() => _disposed = true;

    internal void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SKPaint));
        }
    }

    internal SolidColorBrush CreateSolidColorBrush()
    {
        ThrowIfDisposed();
        var alpha = Math.Clamp(Opacity, 0f, 1f);
        var color = Color.ToRgbaColor();
        return new SolidColorBrush(new RgbaColor(color.R, color.G, color.B, color.A * alpha));
    }

    internal StrokeStyle CreateStrokeStyle()
    {
        ThrowIfDisposed();
        return new StrokeStyle
        {
            Width = Math.Max(0.1, StrokeWidth),
            MiterLimit = Math.Max(1.0, StrokeMiter),
            StartCap = ToLineCap(StrokeCap),
            EndCap = ToLineCap(StrokeCap),
            LineJoin = ToLineJoin(StrokeJoin),
        };
    }

    private static LineCap ToLineCap(SKStrokeCap cap) => cap switch
    {
        SKStrokeCap.Butt => LineCap.Butt,
        SKStrokeCap.Round => LineCap.Round,
        SKStrokeCap.Square => LineCap.Square,
        _ => LineCap.Butt,
    };

    private static LineJoin ToLineJoin(SKStrokeJoin join) => join switch
    {
        SKStrokeJoin.Miter => LineJoin.Miter,
        SKStrokeJoin.Round => LineJoin.Round,
        SKStrokeJoin.Bevel => LineJoin.Bevel,
        _ => LineJoin.Miter,
    };
}
