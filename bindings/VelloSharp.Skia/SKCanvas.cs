using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using VelloSharp;

namespace SkiaSharp;

public sealed class SKCanvas
{
    private static readonly LayerBlend s_clipLayerBlend = new(LayerMix.Clip, LayerCompose.SrcOver);

    private readonly Scene _scene;
    private readonly float _width;
    private readonly float _height;
    private readonly Stack<CanvasState> _saveStack = new();
    private readonly List<ICanvasCommand>? _commandLog;

    private CanvasState _currentState;
    private int _activeLayerDepth;

    internal SKCanvas(Scene scene, float width, float height, List<ICanvasCommand>? commandLog = null)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _width = width;
        _height = height;
        _commandLog = commandLog;
        ResetState();
    }

    public void Save()
    {
        _saveStack.Push(_currentState);
        _commandLog?.Add(SaveCommand.Instance);
    }

    public void Restore()
    {
        _commandLog?.Add(RestoreCommand.Instance);
        if (_saveStack.Count == 0)
        {
            ResetState();
            return;
        }

        var targetState = _saveStack.Pop();
        while (_activeLayerDepth > targetState.LayerDepth)
        {
            _scene.PopLayer();
            _activeLayerDepth--;
        }

        _currentState = targetState;
    }

    public void Translate(float dx, float dy)
    {
        var translation = Matrix3x2.CreateTranslation(dx, dy);
        _currentState = _currentState with { Transform = _currentState.Transform * translation };
        _commandLog?.Add(new TranslateCommand(dx, dy));
    }

    public void Scale(float scale) => Scale(scale, scale);

    public void Scale(float sx, float sy)
    {
        var scaleMatrix = Matrix3x2.CreateScale(sx, sy);
        _currentState = _currentState with { Transform = _currentState.Transform * scaleMatrix };
        _commandLog?.Add(new ScaleCommand(sx, sy));
    }

    public void ResetMatrix()
    {
        _currentState = _currentState with { Transform = Matrix3x2.Identity };
        _commandLog?.Add(ResetMatrixCommand.Instance);
    }

    public void SetMatrix(SKMatrix matrix)
    {
        _currentState = _currentState with { Transform = matrix.ToMatrix3x2() };
        _commandLog?.Add(new SetMatrixCommand(matrix));
    }

    public SKMatrix TotalMatrix => SKMatrix.FromMatrix3x2(_currentState.Transform);

    public SKMatrix44 TotalMatrix44 => SKMatrix44.FromMatrix3x2(_currentState.Transform);

    public void ClipRect(SKRect rect)
    {
        if (rect.IsEmpty)
        {
            return;
        }

        var builder = rect.ToPathBuilder();
        _scene.PushLayer(builder, s_clipLayerBlend, _currentState.Transform, alpha: 1f);
        _activeLayerDepth++;
        _currentState = _currentState with { LayerDepth = _activeLayerDepth };
        _commandLog?.Add(new ClipRectCommand(rect));
    }

    public void Clear(SKColor color)
    {
        _scene.Reset();
        ResetState();
        _commandLog?.Add(new ClearCommand(color));

        if (color == SKColors.Transparent)
        {
            return;
        }

        var rect = SKRect.Create(0, 0, _width, _height);
        var builder = rect.ToPathBuilder();
        var brush = new SolidColorBrush(color.ToRgbaColor());
        _scene.FillPath(builder, FillRule.NonZero, Matrix3x2.Identity, brush);
    }

    public void DrawPath(SKPath path, SKPaint paint)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(paint);
        paint.ThrowIfDisposed();

        var builder = path.ToPathBuilder();
        var brushInfo = paint.CreateBrush();
        var (fillBrush, fillTransform) = brushInfo;

        if (paint.Style is SKPaintStyle.Fill or SKPaintStyle.StrokeAndFill)
        {
            _scene.FillPath(builder, FillRule.NonZero, _currentState.Transform, fillBrush, fillTransform);
        }

        if (paint.Style is SKPaintStyle.Stroke or SKPaintStyle.StrokeAndFill)
        {
            var stroke = paint.CreateStrokeStyle();
            _scene.StrokePath(builder, stroke, _currentState.Transform, fillBrush, fillTransform);
        }

        _commandLog?.Add(new DrawPathCommand(path, paint));
    }

    public void DrawImage(SKImage image, float x, float y)
    {
        ArgumentNullException.ThrowIfNull(image);
        var dest = SKRect.Create(x, y, image.Width, image.Height);
        DrawImage(image, dest);
    }

    public void DrawImage(SKImage image, SKRect destRect)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (destRect.IsEmpty)
        {
            return;
        }

        var scale = Matrix3x2.CreateScale(
            destRect.Width / Math.Max(image.Width, 1),
            destRect.Height / Math.Max(image.Height, 1));
        var translation = Matrix3x2.CreateTranslation(destRect.Left, destRect.Top);
        var transform = scale * translation * _currentState.Transform;

        var brush = new ImageBrush(image.Image);
        _scene.DrawImage(brush, transform);
        _commandLog?.Add(new DrawImageCommand(image, destRect));
    }

    public void DrawText(string? text, float x, float y, SKPaint paint)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(paint);
        paint.ThrowIfDisposed();

        var typeface = paint.Typeface ?? SKTypeface.Default;
        var font = typeface.Font;
        var fontSize = paint.TextSize <= 0 ? 16f : paint.TextSize;

        var glyphs = ListPool<Glyph>.Shared.Rent(text.Length);
        var rendered = false;
        try
        {
            var penX = 0f;
            foreach (var rune in text.EnumerateRunes())
            {
                if (!font.TryGetGlyphIndex((uint)rune.Value, out var glyphIndex))
                {
                    continue;
                }

                float advance;
                if (font.TryGetGlyphMetrics(glyphIndex, fontSize, out var metrics))
                {
                    advance = metrics.Advance;
                }
                else
                {
                    advance = fontSize * 0.6f;
                }

                glyphs.Add(new Glyph(glyphIndex, penX, 0f));
                penX += advance;
            }

            if (glyphs.Count == 0)
            {
                return;
            }

            var snapshot = new SKFont.FontSnapshot(
                typeface,
                fontSize,
                1f,
                0f,
                true,
                false,
                paint.IsAntialias,
                paint.IsAntialias ? SKFontHinting.Normal : SKFontHinting.None,
                paint.IsAntialias ? SKFontEdging.SubpixelAntialias : SKFontEdging.Alias);

            var transform = Matrix3x2.CreateTranslation(x, y) * _currentState.Transform;
            RenderGlyphRun(snapshot, CollectionsMarshal.AsSpan(glyphs), paint, transform);
            rendered = true;
        }
        finally
        {
            ListPool<Glyph>.Shared.Return(glyphs);
        }

        if (rendered)
        {
            _commandLog?.Add(new DrawTextCommand(text, x, y, paint));
        }
    }

    public void DrawText(SKTextBlob textBlob, float x, float y, SKPaint paint)
    {
        ArgumentNullException.ThrowIfNull(textBlob);
        ArgumentNullException.ThrowIfNull(paint);

        var transform = Matrix3x2.CreateTranslation(x, y) * _currentState.Transform;
        var rendered = false;

        foreach (var run in textBlob.Runs)
        {
            var glyphs = run.Glyphs;
            var positions = run.Positions;
            var glyphList = ListPool<Glyph>.Shared.Rent(glyphs.Length);
            try
            {
                for (var i = 0; i < glyphs.Length; i++)
                {
                    var pos = positions[i];
                    glyphList.Add(new Glyph(glyphs[i], pos.X, pos.Y));
                }

                RenderGlyphRun(run.FontSnapshot, CollectionsMarshal.AsSpan(glyphList), paint, transform);
                rendered = true;
            }
            finally
            {
                ListPool<Glyph>.Shared.Return(glyphList);
            }
        }

        if (rendered)
        {
            _commandLog?.Add(new DrawTextBlobCommand(textBlob, x, y, paint));
        }
    }

    public void DrawPicture(SKPicture picture)
    {
        ArgumentNullException.ThrowIfNull(picture);
        picture.Playback(this);
    }

    public void DrawPicture(SKPicture picture, SKMatrix matrix)
    {
        ArgumentNullException.ThrowIfNull(picture);
        Save();
        SetMatrix(matrix);
        picture.Playback(this);
        Restore();
    }

    public void Flush()
    {
        // No batching semantics yet; scene is built incrementally.
    }

    internal Matrix3x2 CurrentTransform => _currentState.Transform;

    private void ResetState()
    {
        _saveStack.Clear();
        _activeLayerDepth = 0;
        _currentState = new CanvasState(Matrix3x2.Identity, 0);
    }

    private void RenderGlyphRun(SKFont.FontSnapshot snapshot, ReadOnlySpan<Glyph> glyphs, SKPaint paint, Matrix3x2 transform)
    {
        var font = snapshot.Typeface?.Font ?? SKTypeface.Default.Font;

        Matrix3x2? glyphTransform = null;
        if (Math.Abs(snapshot.ScaleX - 1f) > float.Epsilon)
        {
            var scaleTransform = Matrix3x2.CreateScale(snapshot.ScaleX, 1f);
            glyphTransform = glyphTransform.HasValue ? glyphTransform.Value * scaleTransform : scaleTransform;
        }

        if (Math.Abs(snapshot.SkewX) > float.Epsilon)
        {
            var skewTransform = Matrix3x2.CreateSkew(snapshot.SkewX, 0f);
            glyphTransform = glyphTransform.HasValue ? glyphTransform.Value * skewTransform : skewTransform;
        }

        EmitGlyphRun(font, glyphs, snapshot.Size, paint, transform, glyphTransform, snapshot.Hinting != SKFontHinting.None);
    }

    private void EmitGlyphRun(Font font, ReadOnlySpan<Glyph> glyphs, float fontSize, SKPaint paint, Matrix3x2 transform, Matrix3x2? glyphTransform, bool hint)
    {
        if (glyphs.IsEmpty)
        {
            return;
        }

        var brushInfo = paint.CreateBrush();
        var options = new GlyphRunOptions
        {
            Brush = brushInfo.Brush,
            FontSize = fontSize <= 0 ? 16f : fontSize,
            Hint = hint,
            Style = GlyphRunStyle.Fill,
            BrushAlpha = 1f,
            Transform = transform,
            GlyphTransform = glyphTransform,
        };

        _scene.DrawGlyphRun(font, glyphs, options);
    }

    private readonly record struct CanvasState(Matrix3x2 Transform, int LayerDepth);
}

internal sealed class ListPool<T>
{
    public static ListPool<T> Shared { get; } = new();

    private readonly Stack<List<T>> _pool = new();

    public List<T> Rent(int capacity)
    {
        lock (_pool)
        {
            if (_pool.Count > 0)
            {
                var list = _pool.Pop();
                list.Clear();
                return list;
            }
        }

        return new List<T>(capacity);
    }

    public void Return(List<T> list)
    {
        if (list is null)
        {
            return;
        }

        lock (_pool)
        {
            _pool.Push(list);
        }
    }
}
