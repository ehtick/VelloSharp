using System;
namespace SkiaSharp;

internal interface ICanvasCommand
{
    void Replay(SKCanvas canvas);
}

internal readonly struct PaintSnapshot
{
    private readonly SKPaintStyle _style;
    private readonly SKStrokeCap _strokeCap;
    private readonly SKStrokeJoin _strokeJoin;
    private readonly float _strokeWidth;
    private readonly float _strokeMiter;
    private readonly bool _isAntialias;
    private readonly float _textSize;
    private readonly SKColor _color;
    private readonly SKTypeface? _typeface;
    private readonly float _opacity;
    private readonly SKShader? _shader;

    public PaintSnapshot(SKPaint paint)
    {
        ArgumentNullException.ThrowIfNull(paint);
        paint.ThrowIfDisposed();

        _style = paint.Style;
        _strokeCap = paint.StrokeCap;
        _strokeJoin = paint.StrokeJoin;
        _strokeWidth = paint.StrokeWidth;
        _strokeMiter = paint.StrokeMiter;
        _isAntialias = paint.IsAntialias;
        _textSize = paint.TextSize;
        _color = paint.Color;
        _typeface = paint.Typeface;
        _opacity = paint.Opacity;
        _shader = paint.Shader;
    }

    public SKPaint CreatePaint()
    {
        var paint = new SKPaint
        {
            Style = _style,
            StrokeCap = _strokeCap,
            StrokeJoin = _strokeJoin,
            StrokeWidth = _strokeWidth,
            StrokeMiter = _strokeMiter,
            IsAntialias = _isAntialias,
            TextSize = _textSize,
            Color = _color,
            Typeface = _typeface,
            Opacity = _opacity,
            Shader = _shader,
        };
        return paint;
    }
}

internal sealed class SaveCommand : ICanvasCommand
{
    public static SaveCommand Instance { get; } = new();
    private SaveCommand() { }
    public void Replay(SKCanvas canvas) => canvas.Save();
}

internal sealed class RestoreCommand : ICanvasCommand
{
    public static RestoreCommand Instance { get; } = new();
    private RestoreCommand() { }
    public void Replay(SKCanvas canvas) => canvas.Restore();
}

internal sealed class TranslateCommand(float dx, float dy) : ICanvasCommand
{
    private readonly float _dx = dx;
    private readonly float _dy = dy;
    public void Replay(SKCanvas canvas) => canvas.Translate(_dx, _dy);
}

internal sealed class ScaleCommand(float sx, float sy) : ICanvasCommand
{
    private readonly float _sx = sx;
    private readonly float _sy = sy;
    public void Replay(SKCanvas canvas) => canvas.Scale(_sx, _sy);
}

internal sealed class RotateCommand(float degrees, float px, float py) : ICanvasCommand
{
    private readonly float _degrees = degrees;
    private readonly float _px = px;
    private readonly float _py = py;
    public void Replay(SKCanvas canvas) => canvas.RotateDegrees(_degrees, _px, _py);
}

internal sealed class ResetMatrixCommand : ICanvasCommand
{
    public static ResetMatrixCommand Instance { get; } = new();
    private ResetMatrixCommand() { }
    public void Replay(SKCanvas canvas) => canvas.ResetMatrix();
}

internal sealed class SetMatrixCommand(SKMatrix matrix) : ICanvasCommand
{
    private readonly SKMatrix _matrix = matrix;
    public void Replay(SKCanvas canvas) => canvas.SetMatrix(_matrix);
}

internal sealed class ClipRectCommand(SKRect rect) : ICanvasCommand
{
    private readonly SKRect _rect = rect;
    public void Replay(SKCanvas canvas) => canvas.ClipRect(_rect);
}

internal sealed class ClearCommand(SKColor color) : ICanvasCommand
{
    private readonly SKColor _color = color;
    public void Replay(SKCanvas canvas) => canvas.Clear(_color);
}

internal sealed class SaveLayerCommand : ICanvasCommand
{
    private readonly SKRect? _rect;
    private readonly bool _hasPaint;
    private readonly PaintSnapshot _paint;

    public SaveLayerCommand(SKRect? rect, SKPaint? paint)
    {
        _rect = rect;
        if (paint is not null)
        {
            _hasPaint = true;
            _paint = new PaintSnapshot(paint);
        }
    }

    public void Replay(SKCanvas canvas)
    {
        SKPaint? paint = null;
        try
        {
            if (_hasPaint)
            {
                paint = _paint.CreatePaint();
            }

            canvas.SaveLayerReplay(_rect, paint);
        }
        finally
        {
            paint?.Dispose();
        }
    }
}

internal sealed class DrawPathCommand : ICanvasCommand
{
    private readonly SKPath _path;
    private readonly PaintSnapshot _paint;

    public DrawPathCommand(SKPath path, SKPaint paint)
    {
        _path = path.Clone();
        _paint = new PaintSnapshot(paint);
    }

    public void Replay(SKCanvas canvas)
    {
        using var paint = _paint.CreatePaint();
        canvas.DrawPath(_path, paint);
    }
}

internal sealed class DrawImageCommand : ICanvasCommand
{
    private readonly SKImage _image;
    private readonly SKRect _destRect;

    public DrawImageCommand(SKImage image, SKRect destRect)
    {
        _image = image ?? throw new ArgumentNullException(nameof(image));
        _destRect = destRect;
    }

    public void Replay(SKCanvas canvas)
    {
        canvas.DrawImage(_image, _destRect);
    }
}

internal sealed class DrawTextCommand : ICanvasCommand
{
    private readonly string _text;
    private readonly float _x;
    private readonly float _y;
    private readonly PaintSnapshot _paint;

    public DrawTextCommand(string text, float x, float y, SKPaint paint)
    {
        _text = text ?? string.Empty;
        _x = x;
        _y = y;
        _paint = new PaintSnapshot(paint);
    }

    public void Replay(SKCanvas canvas)
    {
        using var paint = _paint.CreatePaint();
        canvas.DrawText(_text, _x, _y, paint);
    }
}

internal sealed class DrawTextBlobCommand : ICanvasCommand
{
    private readonly SKTextBlob _blob;
    private readonly float _x;
    private readonly float _y;
    private readonly PaintSnapshot _paint;

    public DrawTextBlobCommand(SKTextBlob blob, float x, float y, SKPaint paint)
    {
        _blob = blob.Clone();
        _x = x;
        _y = y;
        _paint = new PaintSnapshot(paint);
    }

    public void Replay(SKCanvas canvas)
    {
        using var paint = _paint.CreatePaint();
        canvas.DrawText(_blob, _x, _y, paint);
    }
}
