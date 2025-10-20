using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using SkiaSharp;
using VelloSharp;

namespace SkiaSharpShim;

internal sealed class FontMeasurement
{
    private const ulong FnvOffset = 1469598103934665603;
    private const ulong FnvPrime = 1099511628211;

    private readonly ConcurrentDictionary<ShapingKey, ShapedRunHandle> _handles = new();

    public static FontMeasurement Instance { get; } = new();

    private FontMeasurement()
    {
    }

    public ShapedRunHandle GetOrCreateRun(SKTypeface typeface, SKPaint paint, ReadOnlySpan<char> text)
    {
        ArgumentNullException.ThrowIfNull(typeface);
        ArgumentNullException.ThrowIfNull(paint);

        var fontSize = paint.TextSize > 0 ? paint.TextSize : 16f;
        var paintFlags = ComputePaintFlags(paint, fontSize);
        return GetOrCreateRunCore(typeface, fontSize, paintFlags, text);
    }

    public ShapedRunHandle GetOrCreateRun(SKTypeface typeface, float fontSize, SKPaint? paint, ReadOnlySpan<char> text)
    {
        ArgumentNullException.ThrowIfNull(typeface);

        fontSize = fontSize > 0 ? fontSize : 16f;
        var paintFlags = ComputePaintFlags(paint, fontSize);
        return GetOrCreateRunCore(typeface, fontSize, paintFlags, text);
    }

    public float ComputeMetrics(
        SKFont font,
        ShapedRunHandle run,
        Span<float> widths,
        Span<SKRect> bounds,
        Span<SKPoint> positions,
        out SKRect overallBounds)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(run);

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

        if (glyphCount == 0)
        {
            overallBounds = SKRect.Create(0, 0, 0, 0);
            return 0f;
        }

        var glyphSpan = run.GetGlyphSpan();
        var scaleX = font.ScaleX == 0f ? 1f : font.ScaleX;

        var penX = 0f;
        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;
        var hasBounds = false;

        for (var i = 0; i < glyphSpan.Length; i++)
        {
            ref readonly var glyph = ref glyphSpan[i];
            var glyphId = (ushort)glyph.GlyphId;

            var metrics = GetGlyphMetricsOrFallback(font, glyphId);

            var advance = glyph.XAdvance * scaleX;
            var offsetX = glyph.XOffset * scaleX;
            var offsetY = glyph.YOffset;

            if (!widths.IsEmpty)
            {
                widths[i] = advance;
            }

            var glyphX = penX + offsetX;
            if (!positions.IsEmpty)
            {
                positions[i] = new SKPoint(glyphX, offsetY);
            }

            if (!bounds.IsEmpty || !hasBounds)
            {
                var rect = ComputeGlyphBounds(metrics, scaleX, glyphX, offsetY);
                if (!bounds.IsEmpty)
                {
                    bounds[i] = rect;
                }

                if (rect.Width > 0f && rect.Height > 0f)
                {
                    hasBounds = true;
                    minX = MathF.Min(minX, rect.Left);
                    minY = MathF.Min(minY, rect.Top);
                    maxX = MathF.Max(maxX, rect.Right);
                    maxY = MathF.Max(maxY, rect.Bottom);
                }
            }

            penX += advance;
        }

        overallBounds = hasBounds
            ? new SKRect(minX, minY, maxX, maxY)
            : SKRect.Create(0, 0, 0, 0);

        return penX;
    }

    public void Clear()
    {
        foreach (var entry in _handles)
        {
            entry.Value.Dispose();
        }

        _handles.Clear();
    }

    private ShapedRunHandle GetOrCreateRunCore(SKTypeface typeface, float fontSize, uint paintFlags, ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return ShapedRunHandle.Empty;
        }

        var font = typeface.Font;
        var fontHandle = font.Handle;
        if (fontHandle == IntPtr.Zero)
        {
            return ShapedRunHandle.Empty;
        }

        var textHash = ComputeTextHash(text);
        var key = new ShapingKey(ComputeFontId(fontHandle), paintFlags, textHash);

        if (_handles.TryGetValue(key, out var existing))
        {
            if (!existing.IsDisposed)
            {
                existing.Touch();
                return existing;
            }

            _handles.TryRemove(key, out _);
            existing.Dispose();
        }

        var created = Shape(fontHandle, text, paintFlags, textHash, fontSize);
        if (created.IsDisposed)
        {
            return created;
        }

        if (_handles.TryAdd(key, created))
        {
            return created;
        }

        created.Dispose();

        if (_handles.TryGetValue(key, out var cached) && !cached.IsDisposed)
        {
            cached.Touch();
            return cached;
        }

        return ShapedRunHandle.Empty;
    }

    private static ShapedRunHandle Shape(
        IntPtr fontHandle,
        ReadOnlySpan<char> text,
        uint paintFlags,
        ulong textHash,
        float fontSize)
    {
        if (fontHandle == IntPtr.Zero || text.IsEmpty)
        {
            return ShapedRunHandle.Empty;
        }

        unsafe
        {
            fixed (char* textPtr = text)
            {
                var options = new VelloTextShapeOptionsNative
                {
                    FontSize = fontSize,
                    Direction = 0,
                    ScriptTag = 0,
                    Language = null,
                    LanguageLength = 0,
                    Features = null,
                    FeatureCount = 0,
                    VariationAxes = null,
                    VariationAxisCount = 0,
                };

                var status = NativeMethods.vello_text_shape_utf16(
                    fontHandle,
                    (ushort*)textPtr,
                    (nuint)text.Length,
                    &options,
                    out var run,
                    out var handle);

                if (status != VelloStatus.Success || handle == IntPtr.Zero || run.Glyphs == IntPtr.Zero)
                {
                    if (handle != IntPtr.Zero)
                    {
                        NativeMethods.vello_text_shape_destroy(handle);
                    }

                    return ShapedRunHandle.Empty;
                }

                return new ShapedRunHandle(
                    handle,
                    run,
                    ComputeFontId(fontHandle),
                    paintFlags,
                    textHash,
                    fontSize);
            }
        }
    }

    private static GlyphMetrics GetGlyphMetricsOrFallback(SKFont font, ushort glyphId)
    {
        if (font.TryGetGlyphMetrics(glyphId, out var metrics))
        {
            return metrics;
        }

        var size = font.Size > 0 ? font.Size : 12f;
        var fallbackAdvance = size * 0.5f;
        var fallbackYBearing = size * 0.8f;
        var fallbackWidth = size * 0.6f;
        var fallbackHeight = size * 0.8f;

        return new GlyphMetrics(fallbackAdvance, 0f, fallbackYBearing, fallbackWidth, fallbackHeight);
    }

    private static SKRect ComputeGlyphBounds(GlyphMetrics metrics, float scaleX, float originX, float originY)
    {
        var left = originX + metrics.XBearing * scaleX;
        var top = originY - metrics.YBearing;
        var right = left + metrics.Width * scaleX;
        var bottom = top + metrics.Height;
        return new SKRect(left, top, right, bottom);
    }

    private static uint ComputePaintFlags(SKPaint? paint, float fontSize)
    {
        uint flags = 0;

        if (paint is not null)
        {
            if (paint.IsAntialias)
            {
                flags |= 1u << 0;
            }

            if (paint.IsStroke)
            {
                flags |= 1u << 1;
            }

            if (paint.Style == SKPaintStyle.StrokeAndFill)
            {
                flags |= 1u << 2;
            }

            if (paint.Shader is not null)
            {
                flags |= 1u << 3;
            }

            if (paint.MaskFilter is not null)
            {
                flags |= 1u << 4;
            }

            if (paint.PathEffect is not null)
            {
                flags |= 1u << 5;
            }

            if (paint.ColorFilter is not null)
            {
                flags |= 1u << 6;
            }

            if (paint.ImageFilter is not null)
            {
                flags |= 1u << 7;
            }

            if (paint.Blender is not null)
            {
                flags |= 1u << 8;
            }

            if (paint.FilterQuality != SKFilterQuality.None)
            {
                flags |= 1u << 9;
            }

            if (paint.BlendMode != SKBlendMode.SrcOver)
            {
                flags |= 1u << 10;
            }

            flags |= ((uint)paint.StrokeCap & 0x3u) << 11;
            flags |= ((uint)paint.StrokeJoin & 0x3u) << 13;
            if (paint.StrokeWidth > 1f)
            {
                flags |= 1u << 15;
            }
        }

        var sizeBits = unchecked((uint)BitConverter.SingleToInt32Bits(fontSize));
        return flags ^ sizeBits;
    }

    private static ulong ComputeFontId(IntPtr handle)
    {
        return unchecked((ulong)handle.ToInt64());
    }

    private static ulong ComputeTextHash(ReadOnlySpan<char> text)
    {
        ulong hash = FnvOffset;
        foreach (var ch in text)
        {
            hash ^= ch;
            hash *= FnvPrime;
        }

        return hash;
    }

    private readonly record struct ShapingKey(ulong FontId, uint PaintFlags, ulong TextHash);

    internal sealed class ShapedRunHandle : IDisposable
    {
        public static ShapedRunHandle Empty { get; } = new(IntPtr.Zero, default, 0, 0, 0, 0f);

        private IntPtr _handle;
        private readonly IntPtr _glyphs;
        private readonly int _glyphCount;
        private readonly float _advance;

        internal ShapedRunHandle(
            IntPtr handle,
            VelloShapedRunNative run,
            ulong fontId,
            uint paintFlags,
            ulong textHash,
            float fontSize)
        {
            _handle = handle;
            _glyphs = run.Glyphs;
            _glyphCount = checked((int)run.GlyphCount);
            _advance = run.Advance;
            FontId = fontId;
            PaintFlags = paintFlags;
            TextHash = textHash;
            FontSize = fontSize;
            CreatedUtc = DateTime.UtcNow;
        }

        public bool IsDisposed => _handle == IntPtr.Zero;
        public int GlyphCount => _glyphCount;
        public ulong FontId { get; }
        public uint PaintFlags { get; }
        public ulong TextHash { get; }
        public float FontSize { get; }
        public float Advance => _advance;
        public DateTime CreatedUtc { get; private set; }
        public DateTime LastAccessUtc { get; private set; } = DateTime.UtcNow;

        public ReadOnlySpan<VelloShapedGlyphNative> GetGlyphSpan()
        {
            if (_glyphs == IntPtr.Zero || _glyphCount == 0)
            {
                return ReadOnlySpan<VelloShapedGlyphNative>.Empty;
            }

            unsafe
            {
                return new ReadOnlySpan<VelloShapedGlyphNative>(
                    (void*)_glyphs,
                    _glyphCount);
            }
        }

        public Glyph[] ToGlyphArray()
        {
            if (_glyphCount == 0)
            {
                return Array.Empty<Glyph>();
            }

            var span = GetGlyphSpan();
            if (span.IsEmpty)
            {
                return Array.Empty<Glyph>();
            }

            var glyphs = new Glyph[span.Length];
            var penX = 0f;

            for (var i = 0; i < span.Length; i++)
            {
                ref readonly var glyph = ref span[i];
                var x = penX + glyph.XOffset;
                var y = glyph.YOffset;
                glyphs[i] = new Glyph(glyph.GlyphId, x, y);
                penX += glyph.XAdvance;
            }

            return glyphs;
        }

        public ushort[] ToGlyphIdArray()
        {
            if (_glyphCount == 0)
            {
                return Array.Empty<ushort>();
            }

            var span = GetGlyphSpan();
            if (span.IsEmpty)
            {
                return Array.Empty<ushort>();
            }

            var ids = new ushort[span.Length];
            for (var i = 0; i < span.Length; i++)
            {
                ids[i] = (ushort)span[i].GlyphId;
            }

            return ids;
        }

        public void Dispose()
        {
            var handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
            if (handle != IntPtr.Zero)
            {
                NativeMethods.vello_text_shape_destroy(handle);
            }
        }

        public void Touch()
        {
            LastAccessUtc = DateTime.UtcNow;
        }

        ~ShapedRunHandle()
        {
            if (_handle != IntPtr.Zero)
            {
                NativeMethods.vello_text_shape_destroy(_handle);
            }
        }
    }
}
