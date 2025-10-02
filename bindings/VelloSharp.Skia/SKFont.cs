using System;
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

    public float GetGlyphWidths(ReadOnlySpan<ushort> glyphs, float[]? widths, Span<SKRect> bounds)
    {
        ThrowIfDisposed();
        if (glyphs.Length > bounds.Length)
        {
            throw new ArgumentException("Bounds span is too small for the glyph collection.", nameof(bounds));
        }

        float totalAdvance = 0f;
        for (var i = 0; i < glyphs.Length; i++)
        {
            var glyphId = glyphs[i];
            if (!TryGetGlyphMetrics(glyphId, out var metrics))
            {
                metrics = new GlyphMetrics(Size * 0.5f, 0f, Size * 0.8f, Size * 0.6f, Size * 0.8f);
            }

            var advance = metrics.Advance * ScaleX;
            if (widths is not null)
            {
                widths[i] = advance;
            }

            var left = metrics.XBearing * ScaleX;
            var top = -metrics.YBearing;
            var right = left + metrics.Width * ScaleX;
            var bottom = top + metrics.Height;
            bounds[i] = SKRect.Create(left, top, right - left, bottom - top);

            totalAdvance += advance;
        }

        return totalAdvance;
    }

    internal bool TryGetGlyphMetrics(uint glyphId, out GlyphMetrics metrics)
    {
        ThrowIfDisposed();
        return _typeface.Font.TryGetGlyphMetrics((ushort)glyphId, _size, out metrics);
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
}
