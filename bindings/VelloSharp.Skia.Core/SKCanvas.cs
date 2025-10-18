using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using VelloSharp;

namespace SkiaSharp;

public sealed class SKCanvas : IDisposable
{
    private static readonly LayerBlend s_clipLayerBlend = new(LayerMix.Clip, LayerCompose.SrcOver);
    private static readonly LayerBlend s_saveLayerBlend = new(LayerMix.Normal, LayerCompose.SrcOver);
    private const float CircleControlPoint = 0.552284749831f;

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

    public int SaveCount => _saveStack.Count + 1;

    public void Reset()
    {
        _scene.Reset();
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

    public void RestoreToCount(int saveCount)
    {
        var target = Math.Min(saveCount, SaveCount);
        if (target < 1)
        {
            target = 1;
        }

        while (SaveCount > target)
        {
            Restore();
        }
    }

    public void SaveLayer() => SaveLayerCore(null, null, record: true);

    public void SaveLayer(SKPaint? paint) => SaveLayerCore(null, paint, record: true);

    public void SaveLayer(SKRect rect) => SaveLayer(rect, null);

    public void SaveLayer(SKRect rect, SKPaint? paint) => SaveLayerCore(rect, paint, record: true);

    internal void SaveLayerReplay(SKRect? rect, SKPaint? paint) => SaveLayerCore(rect, paint, record: false);

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

    public void SetMatrix(SKMatrix44 matrix) => SetMatrix(SKMatrix.FromMatrix3x2(matrix.ToMatrix3x2()));

    public SKMatrix TotalMatrix => SKMatrix.FromMatrix3x2(_currentState.Transform);

    public SKMatrix44 TotalMatrix44 => SKMatrix44.FromMatrix3x2(_currentState.Transform);

    public void RotateDegrees(float degrees) => RotateDegrees(degrees, 0f, 0f);

    public void RotateDegrees(float degrees, float px, float py)
    {
        var rotation = Matrix3x2.CreateRotation(DegreesToRadians(degrees), new Vector2(px, py));
        _currentState = _currentState with { Transform = _currentState.Transform * rotation };
        _commandLog?.Add(new RotateCommand(degrees, px, py));
    }

    public bool QuickReject(SKRect rect)
    {
        if (rect.IsEmpty)
        {
            return true;
        }

        var transform = _currentState.Transform;
        var p1 = Vector2.Transform(new Vector2(rect.Left, rect.Top), transform);
        var p2 = Vector2.Transform(new Vector2(rect.Right, rect.Top), transform);
        var p3 = Vector2.Transform(new Vector2(rect.Right, rect.Bottom), transform);
        var p4 = Vector2.Transform(new Vector2(rect.Left, rect.Bottom), transform);

        var minX = MathF.Min(MathF.Min(p1.X, p2.X), MathF.Min(p3.X, p4.X));
        var maxX = MathF.Max(MathF.Max(p1.X, p2.X), MathF.Max(p3.X, p4.X));
        var minY = MathF.Min(MathF.Min(p1.Y, p2.Y), MathF.Min(p3.Y, p4.Y));
        var maxY = MathF.Max(MathF.Max(p1.Y, p2.Y), MathF.Max(p3.Y, p4.Y));

        if (maxX <= 0 || maxY <= 0)
        {
            return true;
        }

        if (minX >= _width || minY >= _height)
        {
            return true;
        }

        return false;
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
        _commandLog?.Add(new ClipRectCommand(rect));
    }

    public void ClipRect(SKRect rect, SKClipOperation operation) =>
        ClipRect(rect, operation, antialias: false);

    public void ClipRect(SKRect rect, SKClipOperation operation, bool antialias)
    {
        if (operation == SKClipOperation.Intersect)
        {
            ClipRect(rect);
            return;
        }

        using var path = new SKPath();
        path.AddRect(rect);
        ClipPath(path, operation, antialias);
    }

    public void ClipRoundRect(SKRoundRect roundRect, bool antialias = false) =>
        ClipRoundRect(roundRect, SKClipOperation.Intersect, antialias);

    public void ClipRoundRect(SKRoundRect roundRect, SKClipOperation operation, bool antialias = false)
    {
        ArgumentNullException.ThrowIfNull(roundRect);
        using var path = CreateRoundRectPath(roundRect);
        ClipPath(path, operation, antialias);
    }

    public void ClipRegion(SKRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        using var path = new SKPath
        {
            FillType = SKPathFillType.Winding,
        };

        using var iterator = region.CreateRectIterator();
        var hasRect = false;
        while (iterator.Next(out var rect))
        {
            var normalized = NormalizeRect(rect);
            if (normalized.Right <= normalized.Left || normalized.Bottom <= normalized.Top)
            {
                continue;
            }

            hasRect = true;
            path.AddRect(new SKRect(normalized.Left, normalized.Top, normalized.Right, normalized.Bottom));
        }

        PathBuilder clipBuilder;
        if (hasRect)
        {
            clipBuilder = path.ToPathBuilder();
        }
        else
        {
            clipBuilder = CreateEmptyClipBuilder();
        }

        _scene.PushLayer(clipBuilder, s_clipLayerBlend, _currentState.Transform, alpha: 1f);
        _activeLayerDepth++;
        _currentState = _currentState with { LayerDepth = _activeLayerDepth };
        _commandLog?.Add(new ClipRegionCommand(region));
    }

    public void ClipPath(SKPath path, SKClipOperation operation, bool doAntialias)
    {
        ArgumentNullException.ThrowIfNull(path);

        SKPath? ownedPath = null;
        var effectiveOperation = operation;

        if (operation == SKClipOperation.Difference)
        {
            ownedPath = new SKPath
            {
                FillType = SKPathFillType.EvenOdd,
            };
            ownedPath.AddRect(SKRect.Create(0, 0, _width, _height));
            ownedPath.AddPath(path);
            path = ownedPath;
            effectiveOperation = SKClipOperation.Intersect;
        }
        else if (operation != SKClipOperation.Intersect)
        {
            ShimNotImplemented.Throw($"{nameof(SKCanvas)}.{nameof(ClipPath)}", operation.ToString());
            return;
        }

        var builder = path.ToPathBuilder();
        _scene.PushLayer(builder, s_clipLayerBlend, _currentState.Transform, alpha: 1f);
        _activeLayerDepth++;
        _currentState = _currentState with { LayerDepth = _activeLayerDepth };
        _commandLog?.Add(new ClipPathCommand(path, effectiveOperation, doAntialias));
        ownedPath?.Dispose();
    }

    public void Clear() => Clear(SKColors.Transparent);

    public void Clear(SKColor color)
    {
        var canResetScene =
            _saveStack.Count == 0 &&
            _activeLayerDepth == 0 &&
            _currentState.Transform == Matrix3x2.Identity;

        if (canResetScene)
        {
            _scene.Reset();
            ResetState();

            if (color == SKColors.Transparent)
            {
                _commandLog?.Add(new ClearCommand(color));
                return;
            }
        }

        _commandLog?.Add(new ClearCommand(color));

        var rect = SKRect.Create(0, 0, _width, _height);

        if (color == SKColors.Transparent)
        {
            var clipBlend = new LayerBlend(LayerMix.Normal, LayerCompose.Copy);
            var clipBuilder = rect.ToPathBuilder();
            var clearBrush = new SolidColorBrush(RgbaColor.FromBytes(0, 0, 0, 0));

            _scene.PushLayer(clipBuilder, clipBlend, Matrix3x2.Identity, 1f);
            var transparentFill = rect.ToPathBuilder();
            _scene.FillPath(transparentFill, FillRule.NonZero, Matrix3x2.Identity, clearBrush);
            _scene.PopLayer();
            return;
        }

        var colorFill = rect.ToPathBuilder();
        var brush = new SolidColorBrush(color.ToRgbaColor());
        _scene.FillPath(colorFill, FillRule.NonZero, Matrix3x2.Identity, brush);
    }

    public void DrawRect(SKRect rect, SKPaint paint)
    {
        using var path = new SKPath();
        path.MoveTo(rect.Left, rect.Top);
        path.LineTo(rect.Right, rect.Top);
        path.LineTo(rect.Right, rect.Bottom);
        path.LineTo(rect.Left, rect.Bottom);
        path.Close();
        DrawPath(path, paint);
    }

    public void DrawLine(float x0, float y0, float x1, float y1, SKPaint paint)
    {
        ArgumentNullException.ThrowIfNull(paint);
        paint.ThrowIfDisposed();

        using var path = new SKPath();
        path.MoveTo(x0, y0);
        path.LineTo(x1, y1);
        DrawPath(path, paint);
    }

    public void DrawLine(SKPoint p0, SKPoint p1, SKPaint paint) =>
        DrawLine(p0.X, p0.Y, p1.X, p1.Y, paint);

    public void DrawRoundRect(SKRect rect, float rx, float ry, SKPaint paint)
    {
        using var path = CreateRoundRectPath(rect, rx, ry);
        DrawPath(path, paint);
    }

    public void DrawRoundRect(SKRoundRect roundRect, SKPaint paint)
    {
        ArgumentNullException.ThrowIfNull(roundRect);
        using var path = CreateRoundRectPath(roundRect);
        DrawPath(path, paint);
    }

    public void DrawRoundRectDifference(SKRoundRect outer, SKRoundRect inner, SKPaint paint)
    {
        ArgumentNullException.ThrowIfNull(outer);
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(paint);
        paint.ThrowIfDisposed();

        using var path = new SKPath
        {
            FillType = SKPathFillType.EvenOdd,
        };
        using var outerPath = CreateRoundRectPath(outer);
        using var innerPath = CreateRoundRectPath(inner);
        path.AddPath(outerPath);
        path.AddPath(innerPath);
        DrawPath(path, paint);
    }

    public void DrawCircle(float cx, float cy, float radius, SKPaint paint)
    {
        var rect = SKRect.Create(cx - radius, cy - radius, radius * 2, radius * 2);
        DrawOval(rect, paint);
    }

    public void DrawCircle(SKPoint center, float radius, SKPaint paint) =>
        DrawCircle(center.X, center.Y, radius, paint);

    public void DrawOval(SKRect rect, SKPaint paint)
    {
        using var path = CreateOvalPath(rect);
        DrawPath(path, paint);
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

    public void DrawPaint(SKPaint paint)
    {
        ArgumentNullException.ThrowIfNull(paint);
        paint.ThrowIfDisposed();
        var rect = SKRect.Create(0, 0, _width, _height);
        DrawRect(rect, paint);
    }

    public void DrawRegion(SKRegion region, SKPaint paint)
    {
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(paint);
        paint.ThrowIfDisposed();
        ShimNotImplemented.Throw($"{nameof(SKCanvas)}.{nameof(DrawRegion)}", "region rendering");
    }

    public void DrawImage(SKImage image, float x, float y) =>
        DrawImage(image, x, y, SKSamplingOptions.Default, paint: null);

    public void DrawImage(SKImage image, float x, float y, SKSamplingOptions sampling) =>
        DrawImage(image, x, y, sampling, paint: null);

    public void DrawImage(SKImage image, float x, float y, SKPaint? paint) =>
        DrawImage(image, x, y, SKSamplingOptions.Default, paint);

    public void DrawImage(SKImage image, float x, float y, SKSamplingOptions sampling, SKPaint? paint)
    {
        ArgumentNullException.ThrowIfNull(image);
        var dest = SKRect.Create(x, y, image.Width, image.Height);
        DrawImage(image, dest, sampling, paint);
    }

    public void DrawImage(SKImage image, SKPoint point) =>
        DrawImage(image, point.X, point.Y, SKSamplingOptions.Default, paint: null);

    public void DrawImage(SKImage image, SKPoint point, SKPaint? paint) =>
        DrawImage(image, point.X, point.Y, SKSamplingOptions.Default, paint);

    public void DrawImage(SKImage image, SKPoint point, SKSamplingOptions sampling, SKPaint? paint = null) =>
        DrawImage(image, point.X, point.Y, sampling, paint);

    public void DrawImage(SKImage image, SKRect destRect) =>
        DrawImage(image, destRect, SKSamplingOptions.Default, paint: null);

    public void DrawImage(SKImage image, SKRect destRect, SKPaint? paint) =>
        DrawImage(image, destRect, SKSamplingOptions.Default, paint);

    public void DrawImage(SKImage image, SKRect destRect, SKSamplingOptions sampling) =>
        DrawImage(image, destRect, sampling, paint: null);

    public void DrawImage(SKImage image, SKRect destRect, SKSamplingOptions sampling, SKPaint? paint) =>
        DrawImageCore(image, destRect, sampling, paint, sourceRect: null);

    public void DrawImage(SKImage image, SKRect destRect, SKRect sourceRect, SKSamplingOptions sampling, SKPaint? paint = null) =>
        DrawImageCore(image, destRect, sampling, paint, sourceRect);

    private void DrawImageCore(SKImage image, SKRect destRect, SKSamplingOptions sampling, SKPaint? paint, SKRect? sourceRect)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (destRect.IsEmpty)
        {
            return;
        }

        paint?.ThrowIfDisposed();

        if (paint is not null)
        {
            if (paint.Shader is not null ||
                paint.PathEffect is not null ||
                paint.ImageFilter is not null ||
                paint.BlendMode != SKBlendMode.SrcOver)
            {
                ShimNotImplemented.Throw($"{nameof(SKCanvas)}.{nameof(DrawImage)}", "Advanced paint parameters");
            }
        }

        if (sourceRect is SKRect rect && rect.IsEmpty)
        {
            sourceRect = null;
        }

        var effectiveSource = sourceRect ?? new SKRect(0, 0, image.Width, image.Height);
        var effectiveDest = destRect;

        if (!NormalizeSourceAndDestination(image, ref effectiveSource, ref effectiveDest))
        {
            return;
        }

        var translateToOrigin = Matrix3x2.CreateTranslation(-effectiveSource.Left, -effectiveSource.Top);
        var scale = Matrix3x2.CreateScale(
            effectiveDest.Width / Math.Max(effectiveSource.Width, float.Epsilon),
            effectiveDest.Height / Math.Max(effectiveSource.Height, float.Epsilon));
        var translation = Matrix3x2.CreateTranslation(effectiveDest.Left, effectiveDest.Top);
        var transform = translateToOrigin * scale * translation * _currentState.Transform;

        var alpha = 1f;
        if (paint is not null)
        {
            alpha = Math.Clamp(paint.Opacity * (paint.Color.Alpha / 255f), 0f, 1f);
        }

        var brush = new ImageBrush(image.Image)
        {
            Quality = sampling.ToBrushQuality(),
            Alpha = alpha,
        };

        PathBuilder? clip = null;
        if (sourceRect.HasValue)
        {
            clip = effectiveDest.ToPathBuilder();
            _scene.PushLayer(clip, s_clipLayerBlend, _currentState.Transform, 1f);
        }

        try
        {
            _scene.DrawImage(brush, transform);
        }
        finally
        {
            if (clip is not null)
            {
                _scene.PopLayer();
            }
        }

        _commandLog?.Add(new DrawImageCommand(image, effectiveDest, sampling, paint, sourceRect.HasValue ? effectiveSource : null));
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

    public void DrawPicture(SKPicture picture, SKMatrix44 matrix)
    {
        ArgumentNullException.ThrowIfNull(picture);
        var transform = new Matrix3x2(
            matrix.M00, matrix.M10,
            matrix.M01, matrix.M11,
            matrix.M03, matrix.M13);
        DrawPicture(picture, SKMatrix.FromMatrix3x2(transform));
    }

    public void Flush()
    {
        // No batching semantics yet; scene is built incrementally.
    }

    internal Matrix3x2 CurrentTransform => _currentState.Transform;

    public void Dispose()
    {
        while (_activeLayerDepth > 0)
        {
            _scene.PopLayer();
            _activeLayerDepth--;
        }

        _saveStack.Clear();
        _commandLog?.Clear();
        _scene.Reset();
        _currentState = new CanvasState(Matrix3x2.Identity, 0);
    }

    private void ResetState()
    {
        _saveStack.Clear();
        _activeLayerDepth = 0;
        _currentState = new CanvasState(Matrix3x2.Identity, 0);
    }

    private void SaveLayerCore(SKRect? rect, SKPaint? paint, bool record)
    {
        _saveStack.Push(_currentState);

        var layerRect = rect ?? SKRect.Create(0, 0, _width, _height);
        if (layerRect.IsEmpty)
        {
            layerRect = SKRect.Create(0, 0, _width, _height);
        }

        if (paint is not null)
        {
            paint.ThrowIfDisposed();
        }

        var builder = layerRect.ToPathBuilder();
        var alpha = ComputeLayerAlpha(paint);
        _scene.PushLayer(builder, s_saveLayerBlend, _currentState.Transform, alpha);
        _activeLayerDepth++;
        _currentState = _currentState with { LayerDepth = _activeLayerDepth };

        if (record)
        {
            _commandLog?.Add(new SaveLayerCommand(rect, paint));
        }
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

    private static float DegreesToRadians(float degrees) => degrees * (float)(Math.PI / 180.0);

    private static float ComputeLayerAlpha(SKPaint? paint)
    {
        if (paint is null)
        {
            return 1f;
        }

        var opacity = Math.Clamp(paint.Opacity, 0f, 1f);
        var colorAlpha = paint.Color.Alpha / 255f;
        return Math.Clamp(opacity * colorAlpha, 0f, 1f);
    }

    private static bool NormalizeSourceAndDestination(SKImage image, ref SKRect source, ref SKRect destination)
    {
        var imageRect = new SKRect(0, 0, image.Width, image.Height);

        var srcWidth = source.Width;
        var srcHeight = source.Height;
        if (srcWidth <= 0 || srcHeight <= 0)
        {
            return false;
        }

        var left = Math.Clamp(source.Left, imageRect.Left, imageRect.Right);
        var top = Math.Clamp(source.Top, imageRect.Top, imageRect.Bottom);
        var right = Math.Clamp(source.Right, imageRect.Left, imageRect.Right);
        var bottom = Math.Clamp(source.Bottom, imageRect.Top, imageRect.Bottom);

        if (right <= left || bottom <= top)
        {
            return false;
        }

        if (left != source.Left || top != source.Top || right != source.Right || bottom != source.Bottom)
        {
            var scaleX = destination.Width / srcWidth;
            var scaleY = destination.Height / srcHeight;

            var destLeft = destination.Left + (left - source.Left) * scaleX;
            var destTop = destination.Top + (top - source.Top) * scaleY;
            var destRight = destination.Right - (source.Right - right) * scaleX;
            var destBottom = destination.Bottom - (source.Bottom - bottom) * scaleY;

            destination = new SKRect(destLeft, destTop, destRight, destBottom);
            if (destination.IsEmpty)
            {
                return false;
            }
        }

        source = new SKRect(left, top, right, bottom);
        return true;
    }

    private static SKPath CreateOvalPath(SKRect rect)
    {
        var cx = (rect.Left + rect.Right) * 0.5f;
        var cy = (rect.Top + rect.Bottom) * 0.5f;
        var rx = rect.Width * 0.5f;
        var ry = rect.Height * 0.5f;
        var kx = CircleControlPoint * rx;
        var ky = CircleControlPoint * ry;

        var path = new SKPath();
        path.MoveTo(cx, rect.Top);
        path.CubicTo(new SKPoint(cx + kx, rect.Top), new SKPoint(rect.Right, cy - ky), new SKPoint(rect.Right, cy));
        path.CubicTo(new SKPoint(rect.Right, cy + ky), new SKPoint(cx + kx, rect.Bottom), new SKPoint(cx, rect.Bottom));
        path.CubicTo(new SKPoint(cx - kx, rect.Bottom), new SKPoint(rect.Left, cy + ky), new SKPoint(rect.Left, cy));
        path.CubicTo(new SKPoint(rect.Left, cy - ky), new SKPoint(cx - kx, rect.Top), new SKPoint(cx, rect.Top));
        path.Close();
        return path;
    }

    private static SKPath CreateRoundRectPath(SKRect rect, float rx, float ry)
    {
        rx = MathF.Min(rx, rect.Width * 0.5f);
        ry = MathF.Min(ry, rect.Height * 0.5f);

        var kx = CircleControlPoint * rx;
        var ky = CircleControlPoint * ry;

        var path = new SKPath();
        var left = rect.Left;
        var top = rect.Top;
        var right = rect.Right;
        var bottom = rect.Bottom;

        path.MoveTo(left + rx, top);
        path.LineTo(right - rx, top);
        path.CubicTo(
            new SKPoint(right - kx, top),
            new SKPoint(right, top + ky),
            new SKPoint(right, top + ry));
        path.LineTo(right, bottom - ry);
        path.CubicTo(
            new SKPoint(right, bottom - ky),
            new SKPoint(right - kx, bottom),
            new SKPoint(right - rx, bottom));
        path.LineTo(left + rx, bottom);
        path.CubicTo(
            new SKPoint(left + kx, bottom),
            new SKPoint(left, bottom - ky),
            new SKPoint(left, bottom - ry));
        path.LineTo(left, top + ry);
        path.CubicTo(
            new SKPoint(left, top + ky),
            new SKPoint(left + kx, top),
            new SKPoint(left + rx, top));
        path.Close();
        return path;
    }

    private static SKPath CreateRoundRectPath(SKRoundRect roundRect)
    {
        var rect = roundRect.Rect;
        if (rect.IsEmpty)
        {
            var empty = new SKPath();
            empty.AddRect(rect);
            return empty;
        }

        var radii = roundRect.Radii;
        var tl = ClampRadius(radii[0]);
        var tr = ClampRadius(radii[1]);
        var br = ClampRadius(radii[2]);
        var bl = ClampRadius(radii[3]);

        NormalizeCornerRadii(rect, ref tl, ref tr, ref br, ref bl);

        var left = rect.Left;
        var top = rect.Top;
        var right = rect.Right;
        var bottom = rect.Bottom;

        var path = new SKPath
        {
            FillType = SKPathFillType.Winding,
        };

        path.MoveTo(left + tl.X, top);
        path.LineTo(right - tr.X, top);

        if (tr.X > 0f && tr.Y > 0f)
        {
            var kx = CircleControlPoint * tr.X;
            var ky = CircleControlPoint * tr.Y;
            path.CubicTo(
                new SKPoint(right - tr.X + kx, top),
                new SKPoint(right, top + tr.Y - ky),
                new SKPoint(right, top + tr.Y));
        }
        else
        {
            path.LineTo(right, top);
        }

        path.LineTo(right, bottom - br.Y);

        if (br.X > 0f && br.Y > 0f)
        {
            var kx = CircleControlPoint * br.X;
            var ky = CircleControlPoint * br.Y;
            path.CubicTo(
                new SKPoint(right, bottom - br.Y + ky),
                new SKPoint(right - br.X + kx, bottom),
                new SKPoint(right - br.X, bottom));
        }
        else
        {
            path.LineTo(right, bottom);
        }

        path.LineTo(left + bl.X, bottom);

        if (bl.X > 0f && bl.Y > 0f)
        {
            var kx = CircleControlPoint * bl.X;
            var ky = CircleControlPoint * bl.Y;
            path.CubicTo(
                new SKPoint(left + bl.X - kx, bottom),
                new SKPoint(left, bottom - bl.Y + ky),
                new SKPoint(left, bottom - bl.Y));
        }
        else
        {
            path.LineTo(left, bottom);
        }

        path.LineTo(left, top + tl.Y);

        if (tl.X > 0f && tl.Y > 0f)
        {
            var kx = CircleControlPoint * tl.X;
            var ky = CircleControlPoint * tl.Y;
            path.CubicTo(
                new SKPoint(left, top + tl.Y - ky),
                new SKPoint(left + tl.X - kx, top),
                new SKPoint(left + tl.X, top));
        }
        else
        {
            path.LineTo(left, top);
        }

        path.Close();
        return path;
    }

    private static SKPoint ClampRadius(SKPoint radius)
    {
        var x = float.IsFinite(radius.X) ? MathF.Max(radius.X, 0f) : 0f;
        var y = float.IsFinite(radius.Y) ? MathF.Max(radius.Y, 0f) : 0f;
        return new SKPoint(x, y);
    }

    private static void NormalizeCornerRadii(SKRect rect, ref SKPoint tl, ref SKPoint tr, ref SKPoint br, ref SKPoint bl)
    {
        var width = rect.Width;
        var height = rect.Height;

        if (width <= 0 || height <= 0)
        {
            tl = default;
            tr = default;
            br = default;
            bl = default;
            return;
        }

        var scaleX = 1f;
        var topSum = tl.X + tr.X;
        if (topSum > width && topSum > 0f)
        {
            scaleX = MathF.Min(scaleX, width / topSum);
        }

        var bottomSum = bl.X + br.X;
        if (bottomSum > width && bottomSum > 0f)
        {
            scaleX = MathF.Min(scaleX, width / bottomSum);
        }

        if (scaleX < 1f)
        {
            tl.X *= scaleX;
            tr.X *= scaleX;
            br.X *= scaleX;
            bl.X *= scaleX;
        }

        var scaleY = 1f;
        var leftSum = tl.Y + bl.Y;
        if (leftSum > height && leftSum > 0f)
        {
            scaleY = MathF.Min(scaleY, height / leftSum);
        }

        var rightSum = tr.Y + br.Y;
        if (rightSum > height && rightSum > 0f)
        {
            scaleY = MathF.Min(scaleY, height / rightSum);
        }

        if (scaleY < 1f)
        {
            tl.Y *= scaleY;
            tr.Y *= scaleY;
            br.Y *= scaleY;
            bl.Y *= scaleY;
        }
    }

    private readonly record struct CanvasState(Matrix3x2 Transform, int LayerDepth);

    private static SKRectI NormalizeRect(SKRectI rect)
    {
        var left = Math.Min(rect.Left, rect.Right);
        var right = Math.Max(rect.Left, rect.Right);
        var top = Math.Min(rect.Top, rect.Bottom);
        var bottom = Math.Max(rect.Top, rect.Bottom);
        return new SKRectI(left, top, right, bottom);
    }

    private static PathBuilder CreateEmptyClipBuilder()
    {
        var builder = new PathBuilder();
        builder.MoveTo(0, 0)
               .LineTo(0, 0)
               .LineTo(0, 0)
               .Close();
        return builder;
    }
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
