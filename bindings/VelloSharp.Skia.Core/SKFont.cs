using System;
using System.Collections.Generic;
using System.Text;
using VelloSharp;

namespace SkiaSharp;

public enum SKFontHinting
{
    None,
    Slight,
    Normal,
    Full,
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

    public void Dispose()
    {
        _disposed = true;
    }

    public float MeasureText(string text, SKPaint? paint = null) =>
        MeasureText(text.AsSpan(), paint);

    public float MeasureText(ReadOnlySpan<char> text, SKPaint? paint = null)
    {
        var glyphs = GetGlyphArray(text);
        return MeasureText(glyphs, paint);
    }

    public float MeasureText(string text, out SKRect bounds, SKPaint? paint = null) =>
        MeasureText(text.AsSpan(), out bounds, paint);

    public float MeasureText(ReadOnlySpan<char> text, out SKRect bounds, SKPaint? paint = null)
    {
        var glyphs = GetGlyphArray(text);
        return MeasureText(glyphs, out bounds, paint);
    }

    public float MeasureText(ReadOnlySpan<ushort> glyphs, SKPaint? paint = null)
    {
        paint?.ThrowIfDisposed();
        if (paint is not null)
        {
            ShimNotImplemented.Throw($"{nameof(SKFont)}.{nameof(MeasureText)}", "Paint parameter");
        }

        return ProcessGlyphRun(glyphs, Span<float>.Empty, Span<SKRect>.Empty, Span<SKPoint>.Empty, out _);
    }

    public float MeasureText(ReadOnlySpan<ushort> glyphs, out SKRect bounds, SKPaint? paint = null)
    {
        paint?.ThrowIfDisposed();
        if (paint is not null)
        {
            ShimNotImplemented.Throw($"{nameof(SKFont)}.{nameof(MeasureText)}", "Paint parameter");
        }

        return ProcessGlyphRun(glyphs, Span<float>.Empty, Span<SKRect>.Empty, Span<SKPoint>.Empty, out bounds);
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
        if (paint is not null)
        {
            ShimNotImplemented.Throw($"{nameof(SKFont)}.{nameof(BreakText)}", "Paint parameter");
        }

        measuredWidth = 0f;
        if (maxWidth <= 0f || text.IsEmpty)
        {
            return 0;
        }

        var consumed = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (!TryGetGlyphIndex((uint)rune.Value, out var glyphId))
            {
                continue;
            }

            var advance = GetAdvance(glyphId);
            if (measuredWidth + advance > maxWidth)
            {
                break;
            }

            measuredWidth += advance;
            consumed += rune.Utf16SequenceLength;
        }

        return consumed;
    }

    public SKPoint[] GetGlyphPositions(string text, SKPoint origin = default) =>
        GetGlyphPositions(text.AsSpan(), origin);

    public SKPoint[] GetGlyphPositions(ReadOnlySpan<char> text, SKPoint origin = default)
    {
        var glyphs = GetGlyphArray(text);
        return GetGlyphPositions(glyphs, origin);
    }

    public void GetGlyphPositions(string text, Span<SKPoint> offsets, SKPoint origin = default) =>
        GetGlyphPositions(text.AsSpan(), offsets, origin);

    public void GetGlyphPositions(ReadOnlySpan<char> text, Span<SKPoint> offsets, SKPoint origin = default)
    {
        var glyphs = GetGlyphArray(text);
        GetGlyphPositions(glyphs, offsets, origin);
    }

    public SKPoint[] GetGlyphPositions(ReadOnlySpan<ushort> glyphs, SKPoint origin = default)
    {
        if (glyphs.IsEmpty)
        {
            return Array.Empty<SKPoint>();
        }

        var positions = new SKPoint[glyphs.Length];
        var span = positions.AsSpan();
        ProcessGlyphRun(glyphs, Span<float>.Empty, Span<SKRect>.Empty, span, out _);
        OffsetPositions(span, origin);
        return positions;
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
        ProcessGlyphRun(glyphs, Span<float>.Empty, Span<SKRect>.Empty, slice, out _);
        OffsetPositions(slice, origin);
    }

    public float[] GetGlyphWidths(string text, SKPaint? paint = null) =>
        GetGlyphWidths(text.AsSpan(), paint);

    public float[] GetGlyphWidths(ReadOnlySpan<char> text, SKPaint? paint = null)
    {
        var glyphs = GetGlyphArray(text);
        return GetGlyphWidths(glyphs, paint);
    }

    public float[] GetGlyphWidths(string text, out SKRect[] bounds, SKPaint? paint = null) =>
        GetGlyphWidths(text.AsSpan(), out bounds, paint);

    public float[] GetGlyphWidths(ReadOnlySpan<char> text, out SKRect[] bounds, SKPaint? paint = null)
    {
        var glyphs = GetGlyphArray(text);
        return GetGlyphWidths(glyphs, out bounds, paint);
    }

    public float[] GetGlyphWidths(ReadOnlySpan<ushort> glyphs, SKPaint? paint = null)
    {
        paint?.ThrowIfDisposed();
        if (paint is not null)
        {
            ShimNotImplemented.Throw($"{nameof(SKFont)}.{nameof(GetGlyphWidths)}", "Paint parameter");
        }

        if (glyphs.IsEmpty)
        {
            return Array.Empty<float>();
        }

        var widths = new float[glyphs.Length];
        ProcessGlyphRun(glyphs, widths.AsSpan(), Span<SKRect>.Empty, Span<SKPoint>.Empty, out _);
        return widths;
    }

    public float[] GetGlyphWidths(ReadOnlySpan<ushort> glyphs, out SKRect[] bounds, SKPaint? paint = null)
    {
        paint?.ThrowIfDisposed();
        if (paint is not null)
        {
            ShimNotImplemented.Throw($"{nameof(SKFont)}.{nameof(GetGlyphWidths)}", "Paint parameter");
        }

        if (glyphs.IsEmpty)
        {
            bounds = Array.Empty<SKRect>();
            return Array.Empty<float>();
        }

        var widths = new float[glyphs.Length];
        bounds = new SKRect[glyphs.Length];
        ProcessGlyphRun(glyphs, widths.AsSpan(), bounds.AsSpan(), Span<SKPoint>.Empty, out _);
        return widths;
    }

    public void GetGlyphWidths(ReadOnlySpan<ushort> glyphs, Span<float> widths, Span<SKRect> bounds, SKPaint? paint = null)
    {
        paint?.ThrowIfDisposed();
        if (paint is not null)
        {
            ShimNotImplemented.Throw($"{nameof(SKFont)}.{nameof(GetGlyphWidths)}", "Paint parameter");
        }

        if (glyphs.Length > widths.Length)
        {
            throw new ArgumentException("Widths span is too small for the glyph collection.", nameof(widths));
        }

        if (glyphs.Length > bounds.Length)
        {
            throw new ArgumentException("Bounds span is too small for the glyph collection.", nameof(bounds));
        }

        if (glyphs.IsEmpty)
        {
            widths[..0].Clear();
            bounds[..0].Clear();
            return;
        }

        var widthSlice = widths.Slice(0, glyphs.Length);
        var boundsSlice = bounds.Slice(0, glyphs.Length);
        ProcessGlyphRun(glyphs, widthSlice, boundsSlice, Span<SKPoint>.Empty, out _);
    }

    public void GetGlyphWidths(string text, Span<float> widths, Span<SKRect> bounds, SKPaint? paint = null) =>
        GetGlyphWidths(text.AsSpan(), widths, bounds, paint);

    public void GetGlyphWidths(ReadOnlySpan<char> text, Span<float> widths, Span<SKRect> bounds, SKPaint? paint = null)
    {
        var glyphs = GetGlyphArray(text);
        GetGlyphWidths(glyphs, widths, bounds, paint);
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

        if (glyphs.IsEmpty)
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

            if (!widths.IsEmpty)
            {
                widths[i] = advance;
            }

            if (!positions.IsEmpty)
            {
                positions[i] = new SKPoint(penX, 0f);
            }

            var rect = GetGlyphRect(metrics);
            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;
            var left = rect.Left + penX;
            var top = rect.Top;

            if (!bounds.IsEmpty)
            {
                bounds[i] = SKRect.Create(left, top, width, height);
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

        return penX;
    }

    private ushort[] GetGlyphArray(ReadOnlySpan<char> text)
    {
        ThrowIfDisposed();
        if (text.IsEmpty)
        {
            return Array.Empty<ushort>();
        }

        var glyphs = new List<ushort>(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            if (TryGetGlyphIndex((uint)rune.Value, out var glyphId))
            {
                glyphs.Add(glyphId);
            }
        }

        return glyphs.Count == 0 ? Array.Empty<ushort>() : glyphs.ToArray();
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
