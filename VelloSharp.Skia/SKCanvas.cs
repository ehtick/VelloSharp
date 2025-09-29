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

    private CanvasState _currentState;
    private int _activeLayerDepth;

    internal SKCanvas(Scene scene, float width, float height)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _width = width;
        _height = height;
        ResetState();
    }

    public void Save()
    {
        _saveStack.Push(_currentState);
    }

    public void Restore()
    {
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
    }

    public void Scale(float scale) => Scale(scale, scale);

    public void Scale(float sx, float sy)
    {
        var scaleMatrix = Matrix3x2.CreateScale(sx, sy);
        _currentState = _currentState with { Transform = _currentState.Transform * scaleMatrix };
    }

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
    }

    public void Clear(SKColor color)
    {
        _scene.Reset();
        ResetState();

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
        var brush = paint.CreateSolidColorBrush();

        if (paint.Style is SKPaintStyle.Fill or SKPaintStyle.StrokeAndFill)
        {
            _scene.FillPath(builder, FillRule.NonZero, _currentState.Transform, brush);
        }

        if (paint.Style is SKPaintStyle.Stroke or SKPaintStyle.StrokeAndFill)
        {
            var stroke = paint.CreateStrokeStyle();
            _scene.StrokePath(builder, stroke, _currentState.Transform, brush);
        }
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

            var brush = paint.CreateSolidColorBrush();
            var options = new GlyphRunOptions
            {
                Brush = brush,
                FontSize = fontSize,
                Hint = paint.IsAntialias,
                Style = GlyphRunStyle.Fill,
                BrushAlpha = 1f,
                Transform = Matrix3x2.CreateTranslation(x, y) * _currentState.Transform,
            };

            var span = CollectionsMarshal.AsSpan(glyphs);
            _scene.DrawGlyphRun(font, span, options);
        }
        finally
        {
            ListPool<Glyph>.Shared.Return(glyphs);
        }
    }

    internal Matrix3x2 CurrentTransform => _currentState.Transform;

    private void ResetState()
    {
        _saveStack.Clear();
        _activeLayerDepth = 0;
        _currentState = new CanvasState(Matrix3x2.Identity, 0);
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
