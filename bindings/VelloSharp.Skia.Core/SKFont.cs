using System;
using System.Buffers;
using System.Numerics;
using System.Text;
using SkiaSharpShim;
using VelloSharp;

namespace SkiaSharp;

public enum SKFontHinting
{
    None,
    Slight,
    Normal,
    Full,
}

public readonly struct SKFontMetrics
{
    public SKFontMetrics(
        float top,
        float ascent,
        float descent,
        float bottom,
        float leading,
        float averageCharacterWidth,
        float maxCharacterWidth,
        float xMin,
        float xMax,
        float xHeight,
        float capHeight,
        float? underlinePosition,
        float? underlineThickness,
        float? strikeoutPosition,
        float? strikeoutThickness)
    {
        Top = top;
        Ascent = ascent;
        Descent = descent;
        Bottom = bottom;
        Leading = leading;
        AverageCharacterWidth = averageCharacterWidth;
        MaxCharacterWidth = maxCharacterWidth;
        XMin = xMin;
        XMax = xMax;
        XHeight = xHeight;
        CapHeight = capHeight;
        UnderlinePosition = underlinePosition;
        UnderlineThickness = underlineThickness;
        StrikeoutPosition = strikeoutPosition;
        StrikeoutThickness = strikeoutThickness;
    }

    public float Top { get; }
    public float Ascent { get; }
    public float Descent { get; }
    public float Bottom { get; }
    public float Leading { get; }
    public float AverageCharacterWidth { get; }
    public float MaxCharacterWidth { get; }
    public float XMin { get; }
    public float XMax { get; }
    public float XHeight { get; }
    public float CapHeight { get; }
    public float? UnderlinePosition { get; }
    public float? UnderlineThickness { get; }
    public float? StrikeoutPosition { get; }
    public float? StrikeoutThickness { get; }

    public static SKFontMetrics Empty { get; } = new(
        0f,
        0f,
        0f,
        0f,
        0f,
        0f,
        0f,
        0f,
        0f,
        0f,
        0f,
        null,
        null,
        null,
        null);
}

public readonly struct SKFontVerticalMetrics
{
    public SKFontVerticalMetrics(float originX, float originY, float advance, float topSideBearing)
    {
        OriginX = originX;
        OriginY = originY;
        Advance = advance;
        TopSideBearing = topSideBearing;
    }

    public float OriginX { get; }
    public float OriginY { get; }
    public float Advance { get; }
    public float TopSideBearing { get; }
}

public enum SKFontEdging
{
    Alias,
    Antialias,
    SubpixelAntialias,
}

public sealed class SKFont : IDisposable
{
    private bool _disposed;
    private SKTypeface _typeface;
    private float _size;
    private float _scaleX;
    private float _skewX;

    public SKFont()
        : this(SKTypeface.Default, 12f)
    {
    }

    public SKFont(SKTypeface typeface, float size, float scaleX = 1f, float skewX = 0f)
    {
        _typeface = typeface ?? throw new ArgumentNullException(nameof(typeface));
        _size = size > 0 ? size : 12f;
        _scaleX = scaleX == 0f ? 1f : scaleX;
        _skewX = skewX;
    }

    public SKTypeface Typeface
    {
        get => _typeface;
        set => _typeface = value ?? throw new ArgumentNullException(nameof(value));
    }

    public float Size
    {
        get => _size;
        set => _size = value > 0 ? value : _size;
    }

    public float ScaleX
    {
        get => _scaleX;
        set => _scaleX = value == 0 ? 1f : value;
    }

    public float SkewX
    {
        get => _skewX;
        set => _skewX = value;
    }

    public bool LinearMetrics { get; set; }
    public bool Embolden { get; set; }
    public bool Subpixel { get; set; }
    public SKFontHinting Hinting { get; set; }
    public SKFontEdging Edging { get; set; }

    public float Spacing => GetMetrics(out _);

    public SKFontMetrics Metrics
    {
        get
        {
            GetMetrics(out var metrics);
            return metrics;
        }
    }

    public float GetMetrics(out SKFontMetrics metrics)
    {
        ThrowIfDisposed();

        var fontHandle = _typeface.Font.Handle;
        var size = _size > 0f ? _size : 0f;

        var hbStatus = NativeMethods.vello_hb_font_get_extents(fontHandle, size, out var hbExtents);
        var fontStatus = NativeMethods.vello_font_get_metrics(fontHandle, size, out var fontExtents);

        if (hbStatus != VelloStatus.Success || fontStatus != VelloStatus.Success)
        {
            metrics = SKFontMetrics.Empty;
            return 0f;
        }

        var ascentUp = hbExtents.Ascender;
        var descentDown = hbExtents.Descender;
        var leading = hbExtents.LineGap;

        var top = -ascentUp;
        var ascent = top;
        var descent = descentDown >= 0f ? descentDown : -descentDown;
        var bottom = descent;

        if (!LinearMetrics)
        {
            top = MathF.Round(top);
            ascent = top;
            descent = MathF.Round(descent);
            bottom = descent;
            leading = MathF.Round(leading);
        }

        var underlineThickness = fontExtents.UnderlineThickness;
        var underlinePosition = fontExtents.UnderlinePosition;
        var strikeoutThickness = fontExtents.StrikeoutThickness;
        var strikeoutPosition = fontExtents.StrikeoutPosition;

        metrics = new SKFontMetrics(
            top,
            ascent,
            descent,
            bottom,
            leading,
            averageCharacterWidth: 0f,
            maxCharacterWidth: 0f,
            xMin: 0f,
            xMax: 0f,
            xHeight: 0f,
            capHeight: 0f,
            underlinePosition,
            underlineThickness,
            strikeoutPosition,
            strikeoutThickness);

        var spacing = (-top) + bottom + leading;
        return spacing;
    }

    public bool GetVerticalMetrics(ushort glyphId, out SKFontVerticalMetrics metrics, SKPaint? paint = null)
    {
        ThrowIfDisposed();
        paint?.ThrowIfDisposed();

        unsafe
        {
            var size = _size > 0f ? _size : 0f;
            VelloHbGlyphVerticalMetricsNative native;
            VelloVariationAxisValueNative* variationPtr = null;
            var status = NativeMethods.vello_hb_font_get_glyph_vertical_metrics(
                _typeface.Font.Handle,
                glyphId,
                size,
                variationPtr,
                0,
                out native);

            if (status != VelloStatus.Success)
            {
                metrics = default;
                return false;
            }

            const float epsilon = 1e-5f;

            var baseScaleX = ScaleX;
            var paintScaleX = paint?.TextScaleX ?? 1f;
            var horizontalScale = baseScaleX * paintScaleX;

            var originX = native.OriginX * horizontalScale;
            var originY = native.OriginY;
            var advance = native.Advance;
            var topSideBearing = native.TopSideBearing * horizontalScale;

            var totalSkew = SkewX + (paint?.TextSkewX ?? 0f);
            if (MathF.Abs(totalSkew) > epsilon)
            {
                originX += totalSkew * originY;
            }

            var isFakeBold = Embolden || (paint?.IsFakeBoldText ?? false);
            if (isFakeBold)
            {
                var pad = MathF.Max(0.5f, _size * 0.02f * horizontalScale);
                originX -= pad * 0.5f;
                topSideBearing += pad;
            }

            metrics = new SKFontVerticalMetrics(originX, originY, advance, topSideBearing);
            return true;
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }

    public float MeasureText(string text, SKPaint? paint = null) =>
        MeasureText(text.AsSpan(), paint);

    public float MeasureText(ReadOnlySpan<char> text, SKPaint? paint = null)
    {
        var run = GetShapedRun(text, paint);
        return ComputeMetricsFromRun(run, paint, Span<float>.Empty, Span<SKRect>.Empty, Span<SKPoint>.Empty, out _);
    }

    public float MeasureText(string text, out SKRect bounds, SKPaint? paint = null) =>
        MeasureText(text.AsSpan(), out bounds, paint);

    public float MeasureText(ReadOnlySpan<char> text, out SKRect bounds, SKPaint? paint = null)
    {
        var run = GetShapedRun(text, paint);
        return ComputeMetricsFromRun(run, paint, Span<float>.Empty, Span<SKRect>.Empty, Span<SKPoint>.Empty, out bounds);
    }

    public float MeasureText(ReadOnlySpan<ushort> glyphs, SKPaint? paint = null)
    {
        paint?.ThrowIfDisposed();
        return ProcessGlyphRun(glyphs, paint, Span<float>.Empty, Span<SKRect>.Empty, Span<SKPoint>.Empty, out _);
    }

    public float MeasureText(ReadOnlySpan<ushort> glyphs, out SKRect bounds, SKPaint? paint = null)
    {
        paint?.ThrowIfDisposed();
        return ProcessGlyphRun(glyphs, paint, Span<float>.Empty, Span<SKRect>.Empty, Span<SKPoint>.Empty, out bounds);
    }

    public int BreakText(string text, float maxWidth, SKPaint? paint = null) =>
        BreakText(text.AsSpan(), maxWidth, out _, paint);

    public int BreakText(string text, float maxWidth, out float measuredWidth, SKPaint? paint = null) =>
        BreakText(text.AsSpan(), maxWidth, out measuredWidth, paint);

    public int BreakText(ReadOnlySpan<char> text, float maxWidth, SKPaint? paint = null) =>
        BreakText(text, maxWidth, out _, paint);

    public int BreakText(ReadOnlySpan<char> text, float maxWidth, out float measuredWidth, SKPaint? paint = null)
    {
        ThrowIfDisposed();
        paint?.ThrowIfDisposed();

        measuredWidth = 0f;
        if (maxWidth <= 0f || text.IsEmpty)
        {
            return 0;
        }

        var run = GetShapedRun(text, paint);
        if (run.IsDisposed || run.GlyphCount == 0)
        {
            return 0;
        }

        var glyphs = run.GetGlyphSpan();
        var glyphCount = glyphs.Length;
        if (glyphCount == 0)
        {
            return 0;
        }

        var pool = ArrayPool<float>.Shared;
        var rented = pool.Rent(glyphCount);
        try
        {
            var widthSlice = rented.AsSpan(0, glyphCount);
            _ = ComputeMetricsFromRun(run, paint, widthSlice, Span<SKRect>.Empty, Span<SKPoint>.Empty, out _);

            var accumulated = 0f;
            var consumedChars = 0;
            var processedGlyphs = 0;

            for (var i = 0; i < glyphCount; i++)
            {
                var glyphAdvance = widthSlice[i];
                if (accumulated + glyphAdvance > maxWidth)
                {
                    break;
                }

                accumulated += glyphAdvance;
                processedGlyphs++;

                var currentCluster = (int)glyphs[i].Cluster;
                var nextCluster = i + 1 < glyphCount ? (int)glyphs[i + 1].Cluster : text.Length;
                if (nextCluster < currentCluster)
                {
                    nextCluster = currentCluster;
                }

                consumedChars = Math.Max(consumedChars, nextCluster);
            }

            if (consumedChars == 0 && accumulated > 0f)
            {
                consumedChars = text.Length;
            }
            else if (processedGlyphs == glyphCount)
            {
                consumedChars = text.Length;
            }

            measuredWidth = accumulated;
            return consumedChars;
        }
        finally
        {
            pool.Return(rented);
        }
    }

    public SKPoint[] GetGlyphPositions(string text, SKPoint origin = default) =>
        GetGlyphPositions(text.AsSpan(), origin);

    public SKPoint[] GetGlyphPositions(ReadOnlySpan<char> text, SKPoint origin = default)
    {
        var run = GetShapedRun(text, null);
        if (run.IsDisposed || run.GlyphCount == 0)
        {
            return Array.Empty<SKPoint>();
        }

        var positions = new SKPoint[run.GlyphCount];
        var span = positions.AsSpan();
        ComputeMetricsFromRun(run, null, Span<float>.Empty, Span<SKRect>.Empty, span, out _);
        OffsetPositions(span, origin);
        return positions;
    }

    public ushort[] GetGlyphs(string text) => GetGlyphArray(text.AsSpan());

    public ushort[] GetGlyphs(ReadOnlySpan<char> text) => GetGlyphArray(text);

    public void GetGlyphPositions(string text, Span<SKPoint> offsets, SKPoint origin = default) =>
        GetGlyphPositions(text.AsSpan(), offsets, origin);

    public void GetGlyphPositions(ReadOnlySpan<char> text, Span<SKPoint> offsets, SKPoint origin = default)
    {
        var run = GetShapedRun(text, null);
        if (run.IsDisposed || run.GlyphCount == 0)
        {
            offsets[..0].Clear();
            return;
        }

        if (offsets.Length < run.GlyphCount)
        {
            throw new ArgumentException("Offsets span is too small for the glyph collection.", nameof(offsets));
        }

        var slice = offsets.IsEmpty ? Span<SKPoint>.Empty : offsets.Slice(0, run.GlyphCount);
        ComputeMetricsFromRun(run, null, Span<float>.Empty, Span<SKRect>.Empty, slice, out _);
        OffsetPositions(slice, origin);
    }

    public SKPoint[] GetGlyphPositions(ReadOnlySpan<ushort> glyphs, SKPoint origin = default)
    {
        if (glyphs.IsEmpty)
        {
            return Array.Empty<SKPoint>();
        }

        var positions = new SKPoint[glyphs.Length];
        var span = positions.AsSpan();
        ProcessGlyphRun(glyphs, null, Span<float>.Empty, Span<SKRect>.Empty, span, out _);
        OffsetPositions(span, origin);
        return positions;
    }

    public SKPath? GetGlyphPath(ushort glyph)
    {
        ThrowIfDisposed();
        if (!_typeface.Font.TryGetGlyphOutline(glyph, Size, out var commands, out _))
        {
            return null;
        }

        if (commands.Length == 0)
        {
            return null;
        }

        using var path = new SKPath();
        foreach (var command in commands)
        {
            switch (command.Verb)
            {
                case VelloPathVerb.MoveTo:
                    path.MoveTo((float)command.X0, (float)command.Y0);
                    break;
                case VelloPathVerb.LineTo:
                    path.LineTo((float)command.X0, (float)command.Y0);
                    break;
                case VelloPathVerb.QuadTo:
                    path.QuadTo(
                        new SKPoint((float)command.X0, (float)command.Y0),
                        new SKPoint((float)command.X1, (float)command.Y1));
                    break;
                case VelloPathVerb.CubicTo:
                    path.CubicTo(
                        new SKPoint((float)command.X0, (float)command.Y0),
                        new SKPoint((float)command.X1, (float)command.Y1),
                        new SKPoint((float)command.X2, (float)command.Y2));
                    break;
                case VelloPathVerb.Close:
                    path.Close();
                    break;
            }
        }

        if (path.IsEmpty)
        {
            return null;
        }

        path.FillType = SKPathFillType.Winding;

        var transform = Matrix3x2.Identity;

        if (Math.Abs(ScaleX - 1f) > float.Epsilon)
        {
            transform *= Matrix3x2.CreateScale(ScaleX, 1f);
        }

        if (Math.Abs(SkewX) > float.Epsilon)
        {
            transform *= Matrix3x2.CreateSkew(SkewX, 0f);
        }

        if (transform != Matrix3x2.Identity)
        {
            path.Transform(SKMatrix.FromMatrix3x2(transform));
        }

        return path.Clone();
    }

    public void GetGlyphPositions(ReadOnlySpan<ushort> glyphs, Span<SKPoint> offsets, SKPoint origin = default)
    {
        if (offsets.Length < glyphs.Length)
        {
            throw new ArgumentException("Offsets span is too small for the glyph collection.", nameof(offsets));
        }

        if (glyphs.IsEmpty)
        {
            offsets[..0].Clear();
            return;
        }

        var slice = offsets.Slice(0, glyphs.Length);
        ProcessGlyphRun(glyphs, null, Span<float>.Empty, Span<SKRect>.Empty, slice, out _);
        OffsetPositions(slice, origin);
    }

    public float[] GetGlyphWidths(string text, SKPaint? paint = null) =>
        GetGlyphWidths(text.AsSpan(), paint);

    public float[] GetGlyphWidths(ReadOnlySpan<char> text, SKPaint? paint = null)
    {
        var run = GetShapedRun(text, paint);
        if (run.IsDisposed || run.GlyphCount == 0)
        {
            return Array.Empty<float>();
        }

        var widths = new float[run.GlyphCount];
        ComputeMetricsFromRun(run, paint, widths.AsSpan(), Span<SKRect>.Empty, Span<SKPoint>.Empty, out _);
        return widths;
    }

    public float[] GetGlyphWidths(string text, out SKRect[] bounds, SKPaint? paint = null) =>
        GetGlyphWidths(text.AsSpan(), out bounds, paint);

    public float[] GetGlyphWidths(ReadOnlySpan<char> text, out SKRect[] bounds, SKPaint? paint = null)
    {
        var run = GetShapedRun(text, paint);
        if (run.IsDisposed || run.GlyphCount == 0)
        {
            bounds = Array.Empty<SKRect>();
            return Array.Empty<float>();
        }

        var widths = new float[run.GlyphCount];
        bounds = new SKRect[run.GlyphCount];
        ComputeMetricsFromRun(run, paint, widths.AsSpan(), bounds.AsSpan(), Span<SKPoint>.Empty, out _);
        return widths;
    }

    public float[] GetGlyphWidths(ReadOnlySpan<ushort> glyphs, SKPaint? paint = null)
    {
        paint?.ThrowIfDisposed();

        if (glyphs.IsEmpty)
        {
            return Array.Empty<float>();
        }

        var widths = new float[glyphs.Length];
        ProcessGlyphRun(glyphs, paint, widths.AsSpan(), Span<SKRect>.Empty, Span<SKPoint>.Empty, out _);
        return widths;
    }

    public float[] GetGlyphWidths(ReadOnlySpan<ushort> glyphs, out SKRect[] bounds, SKPaint? paint = null)
    {
        paint?.ThrowIfDisposed();

        if (glyphs.IsEmpty)
        {
            bounds = Array.Empty<SKRect>();
            return Array.Empty<float>();
        }

        var widths = new float[glyphs.Length];
        bounds = new SKRect[glyphs.Length];
        ProcessGlyphRun(glyphs, paint, widths.AsSpan(), bounds.AsSpan(), Span<SKPoint>.Empty, out _);
        return widths;
    }

    public void GetGlyphWidths(ReadOnlySpan<ushort> glyphs, Span<float> widths, Span<SKRect> bounds, SKPaint? paint = null)
    {
        paint?.ThrowIfDisposed();

        if (!widths.IsEmpty && glyphs.Length > widths.Length)
        {
            throw new ArgumentException("Widths span is too small for the glyph collection.", nameof(widths));
        }

        if (!bounds.IsEmpty && glyphs.Length > bounds.Length)
        {
            throw new ArgumentException("Bounds span is too small for the glyph collection.", nameof(bounds));
        }

        if (glyphs.IsEmpty)
        {
            widths[..0].Clear();
            bounds[..0].Clear();
            return;
        }

        var widthSlice = widths.IsEmpty ? Span<float>.Empty : widths.Slice(0, glyphs.Length);
        var boundsSlice = bounds.IsEmpty ? Span<SKRect>.Empty : bounds.Slice(0, glyphs.Length);
        ProcessGlyphRun(glyphs, paint, widthSlice, boundsSlice, Span<SKPoint>.Empty, out _);
    }

    public void GetGlyphWidths(string text, Span<float> widths, Span<SKRect> bounds, SKPaint? paint = null) =>
        GetGlyphWidths(text.AsSpan(), widths, bounds, paint);

    public void GetGlyphWidths(ReadOnlySpan<char> text, Span<float> widths, Span<SKRect> bounds, SKPaint? paint = null)
    {
        var run = GetShapedRun(text, paint);
        if (run.IsDisposed || run.GlyphCount == 0)
        {
            widths[..0].Clear();
            bounds[..0].Clear();
            return;
        }

        if (!widths.IsEmpty && widths.Length < run.GlyphCount)
        {
            throw new ArgumentException("Widths span is too small for the glyph collection.", nameof(widths));
        }

        if (!bounds.IsEmpty && bounds.Length < run.GlyphCount)
        {
            throw new ArgumentException("Bounds span is too small for the glyph collection.", nameof(bounds));
        }

        var widthSlice = widths.IsEmpty ? Span<float>.Empty : widths.Slice(0, run.GlyphCount);
        var boundsSlice = bounds.IsEmpty ? Span<SKRect>.Empty : bounds.Slice(0, run.GlyphCount);
        ComputeMetricsFromRun(run, paint, widthSlice, boundsSlice, Span<SKPoint>.Empty, out _);
    }

    private FontMeasurement.ShapedRunHandle GetShapedRun(ReadOnlySpan<char> text, SKPaint? paint)
    {
        ThrowIfDisposed();
        paint?.ThrowIfDisposed();

        if (text.IsEmpty)
        {
            return FontMeasurement.ShapedRunHandle.Empty;
        }

        return FontMeasurement.Instance.GetOrCreateRun(_typeface, _size, paint, text);
    }

    private float ComputeMetricsFromRun(
        FontMeasurement.ShapedRunHandle run,
        SKPaint? paint,
        Span<float> widths,
        Span<SKRect> bounds,
        Span<SKPoint> positions,
        out SKRect overallBounds)
    {
        if (run.IsDisposed || run.GlyphCount == 0)
        {
            overallBounds = SKRect.Create(0, 0, 0, 0);
            return 0f;
        }

        var glyphCount = run.GlyphCount;

        if (!widths.IsEmpty && widths.Length < glyphCount)
        {
            throw new ArgumentException("Widths span is too small for the glyph collection.", nameof(widths));
        }

        if (!bounds.IsEmpty && bounds.Length < glyphCount)
        {
            throw new ArgumentException("Bounds span is too small for the glyph collection.", nameof(bounds));
        }

        if (!positions.IsEmpty && positions.Length < glyphCount)
        {
            throw new ArgumentException("Positions span is too small for the glyph collection.", nameof(positions));
        }

        var widthSlice = widths.IsEmpty ? Span<float>.Empty : widths.Slice(0, glyphCount);
        var boundsSlice = bounds.IsEmpty ? Span<SKRect>.Empty : bounds.Slice(0, glyphCount);
        var positionSlice = positions.IsEmpty ? Span<SKPoint>.Empty : positions.Slice(0, glyphCount);

        var measuredWidth = FontMeasurement.Instance.ComputeMetrics(this, run, widthSlice, boundsSlice, positionSlice, out overallBounds);
        return ApplyFontAndPaintAdjustments(glyphCount, paint, widthSlice, boundsSlice, positionSlice, ref overallBounds, measuredWidth);
    }

    private void OffsetPositions(Span<SKPoint> positions, SKPoint origin)
    {
        if (positions.IsEmpty || (origin.X == 0f && origin.Y == 0f))
        {
            return;
        }

        for (var i = 0; i < positions.Length; i++)
        {
            var point = positions[i];
            positions[i] = new SKPoint(point.X + origin.X, point.Y + origin.Y);
        }
    }

    private float ProcessGlyphRun(
        ReadOnlySpan<ushort> glyphs,
        SKPaint? paint,
        Span<float> widths,
        Span<SKRect> bounds,
        Span<SKPoint> positions,
        out SKRect overallBounds)
    {
        ThrowIfDisposed();

        if (!widths.IsEmpty && widths.Length < glyphs.Length)
        {
            throw new ArgumentException("Widths span is too small for the glyph collection.", nameof(widths));
        }

        if (!bounds.IsEmpty && bounds.Length < glyphs.Length)
        {
            throw new ArgumentException("Bounds span is too small for the glyph collection.", nameof(bounds));
        }

        if (!positions.IsEmpty && positions.Length < glyphs.Length)
        {
            throw new ArgumentException("Positions span is too small for the glyph collection.", nameof(positions));
        }

        var glyphCount = glyphs.Length;
        var widthSlice = widths.IsEmpty ? Span<float>.Empty : widths.Slice(0, glyphCount);
        var boundsSlice = bounds.IsEmpty ? Span<SKRect>.Empty : bounds.Slice(0, glyphCount);
        var positionSlice = positions.IsEmpty ? Span<SKPoint>.Empty : positions.Slice(0, glyphCount);

        if (glyphCount == 0)
        {
            overallBounds = SKRect.Create(0, 0, 0, 0);
            return 0f;
        }

        var penX = 0f;
        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;
        var hasBounds = false;

        for (var i = 0; i < glyphs.Length; i++)
        {
            var metrics = GetGlyphMetricsOrFallback(glyphs[i]);
            var advance = metrics.Advance * ScaleX;

            if (!widthSlice.IsEmpty)
            {
                widthSlice[i] = advance;
            }

            if (!positionSlice.IsEmpty)
            {
                positionSlice[i] = new SKPoint(penX, 0f);
            }

            var rect = GetGlyphRect(metrics);
            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            var left = rect.Left + penX;
            var top = rect.Top;

            if (!boundsSlice.IsEmpty)
            {
                boundsSlice[i] = SKRect.Create(left, top, width, height);
            }

            if (width > 0f && height > 0f)
            {
                hasBounds = true;
                var right = left + width;
                var bottom = top + height;

                minX = MathF.Min(minX, left);
                minY = MathF.Min(minY, top);
                maxX = MathF.Max(maxX, right);
                maxY = MathF.Max(maxY, bottom);
            }

            penX += advance;
        }

        overallBounds = hasBounds
            ? new SKRect(minX, minY, maxX, maxY)
            : SKRect.Create(0, 0, 0, 0);

        return ApplyFontAndPaintAdjustments(glyphCount, paint, widthSlice, boundsSlice, positionSlice, ref overallBounds, penX);
    }

    private float ApplyFontAndPaintAdjustments(
        int glyphCount,
        SKPaint? paint,
        Span<float> widths,
        Span<SKRect> bounds,
        Span<SKPoint> positions,
        ref SKRect overallBounds,
        float measuredWidth)
    {
        const float epsilon = 1e-5f;

        var paintScaleX = paint?.TextScaleX ?? 1f;
        if (MathF.Abs(paintScaleX - 1f) > epsilon)
        {
            if (!widths.IsEmpty)
            {
                for (var i = 0; i < widths.Length; i++)
                {
                    widths[i] *= paintScaleX;
                }
            }

            if (!positions.IsEmpty)
            {
                for (var i = 0; i < positions.Length; i++)
                {
                    var point = positions[i];
                    positions[i] = new SKPoint(point.X * paintScaleX, point.Y);
                }
            }

            if (!bounds.IsEmpty)
            {
                for (var i = 0; i < bounds.Length; i++)
                {
                    var rect = bounds[i];
                    bounds[i] = new SKRect(rect.Left * paintScaleX, rect.Top, rect.Right * paintScaleX, rect.Bottom);
                }
            }

            overallBounds = new SKRect(overallBounds.Left * paintScaleX, overallBounds.Top, overallBounds.Right * paintScaleX, overallBounds.Bottom);
            measuredWidth *= paintScaleX;
        }

        var totalSkew = SkewX + (paint?.TextSkewX ?? 0f);
        if (MathF.Abs(totalSkew) > epsilon)
        {
            if (!positions.IsEmpty)
            {
                for (var i = 0; i < positions.Length; i++)
                {
                    var point = positions[i];
                    positions[i] = new SKPoint(point.X + totalSkew * point.Y, point.Y);
                }
            }

            if (!bounds.IsEmpty)
            {
                for (var i = 0; i < bounds.Length; i++)
                {
                    bounds[i] = ShearRect(bounds[i], totalSkew);
                }
            }

            overallBounds = ShearRect(overallBounds, totalSkew);
        }

        var isFakeBold = Embolden || (paint?.IsFakeBoldText ?? false);
        if (isFakeBold && glyphCount > 0)
        {
            var totalScaleX = ScaleX * paintScaleX;
            var pad = MathF.Max(0.5f, _size * 0.02f * totalScaleX);
            var halfPad = pad * 0.5f;

            measuredWidth += pad;

            if (!widths.IsEmpty && widths.Length > 0)
            {
                widths[^1] += pad;
            }

            if (!bounds.IsEmpty)
            {
                for (var i = 0; i < bounds.Length; i++)
                {
                    var rect = bounds[i];
                    bounds[i] = new SKRect(rect.Left - halfPad, rect.Top, rect.Right + halfPad, rect.Bottom);
                }
            }

            overallBounds = new SKRect(overallBounds.Left - halfPad, overallBounds.Top, overallBounds.Right + halfPad, overallBounds.Bottom);
        }

        if (paint is not null)
        {
            var shift = paint.TextAlign switch
            {
                SKTextAlign.Center => -measuredWidth * 0.5f,
                SKTextAlign.Right => -measuredWidth,
                _ => 0f,
            };

            if (MathF.Abs(shift) > epsilon)
            {
                if (!positions.IsEmpty)
                {
                    for (var i = 0; i < positions.Length; i++)
                    {
                        var point = positions[i];
                        positions[i] = new SKPoint(point.X + shift, point.Y);
                    }
                }

                if (!bounds.IsEmpty)
                {
                    for (var i = 0; i < bounds.Length; i++)
                    {
                        var rect = bounds[i];
                        bounds[i] = new SKRect(rect.Left + shift, rect.Top, rect.Right + shift, rect.Bottom);
                    }
                }

                overallBounds = new SKRect(overallBounds.Left + shift, overallBounds.Top, overallBounds.Right + shift, overallBounds.Bottom);
            }
        }

        return measuredWidth;
    }

    private static SKRect ShearRect(SKRect rect, float skewX)
    {
        if (rect.IsEmpty)
        {
            return rect;
        }

        var topLeft = rect.Left + skewX * rect.Top;
        var bottomLeft = rect.Left + skewX * rect.Bottom;
        var topRight = rect.Right + skewX * rect.Top;
        var bottomRight = rect.Right + skewX * rect.Bottom;

        var newLeft = MathF.Min(MathF.Min(topLeft, bottomLeft), MathF.Min(topRight, bottomRight));
        var newRight = MathF.Max(MathF.Max(topLeft, bottomLeft), MathF.Max(topRight, bottomRight));

        return new SKRect(newLeft, rect.Top, newRight, rect.Bottom);
    }

    private ushort[] GetGlyphArray(ReadOnlySpan<char> text)
    {
        var run = GetShapedRun(text, null);
        if (run.IsDisposed || run.GlyphCount == 0)
        {
            return Array.Empty<ushort>();
        }

        return run.ToGlyphIdArray();
    }

    internal bool TryGetGlyphMetrics(uint glyphId, out GlyphMetrics metrics)
    {
        ThrowIfDisposed();
        return _typeface.Font.TryGetGlyphMetrics((ushort)glyphId, _size, out metrics);
    }

    internal bool TryGetGlyphIndex(uint codePoint, out ushort glyphId)
    {
        ThrowIfDisposed();
        return _typeface.Font.TryGetGlyphIndex(codePoint, out glyphId);
    }

    internal FontSnapshot CreateSnapshot() => new(
        Typeface,
        Size,
        ScaleX,
        SkewX,
        LinearMetrics,
        Embolden,
        Subpixel,
        Hinting,
        Edging);

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SKFont));
        }
    }

    internal readonly record struct FontSnapshot(
        SKTypeface Typeface,
        float Size,
        float ScaleX,
        float SkewX,
        bool LinearMetrics,
        bool Embolden,
        bool Subpixel,
        SKFontHinting Hinting,
        SKFontEdging Edging);

    private GlyphMetrics GetGlyphMetricsOrFallback(ushort glyphId)
    {
        if (!TryGetGlyphMetrics(glyphId, out var metrics))
        {
            var fallbackAdvance = Size * 0.5f;
            var fallbackYBearing = Size * 0.8f;
            var fallbackWidth = Size * 0.6f;
            var fallbackHeight = Size * 0.8f;
            metrics = new GlyphMetrics(fallbackAdvance, 0f, fallbackYBearing, fallbackWidth, fallbackHeight);
        }

        return metrics;
    }

    private float GetAdvance(ushort glyphId)
    {
        var metrics = GetGlyphMetricsOrFallback(glyphId);
        return metrics.Advance * ScaleX;
    }

    private SKRect GetGlyphRect(GlyphMetrics metrics)
    {
        var left = metrics.XBearing * ScaleX;
        var top = -metrics.YBearing;
        var right = left + metrics.Width * ScaleX;
        var bottom = top + metrics.Height;
        return new SKRect(left, top, right, bottom);
    }
}
