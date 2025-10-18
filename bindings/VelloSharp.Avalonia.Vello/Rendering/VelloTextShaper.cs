using System;
using System.Buffers;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Media.TextFormatting.Unicode;
using Avalonia.Platform;
using VelloSharp.Text;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal sealed class VelloTextShaper : ITextShaperImpl
{
    private const char ZeroWidthNonJoiner = '\u200C';

    public ShapedBuffer ShapeText(ReadOnlyMemory<char> text, TextShaperOptions options)
    {
        if (text.IsEmpty)
        {
            return new ShapedBuffer(text, 0, options.Typeface, options.FontRenderingEmSize, options.BidiLevel);
        }

        var typeface = options.Typeface;
        var font = VelloFontManager.GetFont(typeface);
        var isRightToLeft = (options.BidiLevel & 1) != 0;

        var textSpan = text.Span;
        char[]? normalizationBuffer = null;
        var shapingSpan = NormalizeBreakCharacters(textSpan, out normalizationBuffer);

        try
        {
            var glyphs = VelloTextShaperCore.ShapeUtf16(
                font.Handle,
                shapingSpan,
                (float)options.FontRenderingEmSize,
                isRightToLeft,
                (float)options.LetterSpacing);

            if (glyphs.Count == 0)
            {
                return ShapeWithFallback(text, shapingSpan, options);
            }

            var shapedBuffer = new ShapedBuffer(text, glyphs.Count, typeface, options.FontRenderingEmSize, options.BidiLevel);
            for (var i = 0; i < glyphs.Count; i++)
            {
                var glyph = glyphs[i];
                var offset = new Vector(glyph.XOffset, -glyph.YOffset);
                shapedBuffer[i] = new GlyphInfo((ushort)glyph.GlyphId, (int)glyph.Cluster, glyph.XAdvance, offset);
            }

            return shapedBuffer;
        }
        finally
        {
            if (normalizationBuffer is not null)
            {
                ArrayPool<char>.Shared.Return(normalizationBuffer);
            }
        }
    }

    private static ShapedBuffer ShapeWithFallback(ReadOnlyMemory<char> text, ReadOnlySpan<char> shapingSpan, TextShaperOptions options)
    {
        var span = shapingSpan;
        var isRightToLeft = (options.BidiLevel & 1) != 0;

        var entries = new List<GlyphInfo>(span.Length);
        var designEm = options.Typeface.Metrics.DesignEmHeight;
        var scale = designEm != 0 ? options.FontRenderingEmSize / designEm : 1.0;
        var letterSpacing = options.LetterSpacing;

        var runeEnumerator = span.EnumerateRunes();
        var cluster = 0;

        foreach (var rune in runeEnumerator)
        {
            var glyphIndex = options.Typeface.GetGlyph((uint)rune.Value);
            var advance = options.Typeface.GetGlyphAdvance(glyphIndex) * scale + letterSpacing;
            entries.Add(new GlyphInfo(glyphIndex, cluster, advance, Vector.Zero));
            cluster += rune.Utf16SequenceLength;
        }

        if (entries.Count == 0)
        {
            return new ShapedBuffer(text, 0, options.Typeface, options.FontRenderingEmSize, options.BidiLevel);
        }

        var shapedBuffer = new ShapedBuffer(text, entries.Count, options.Typeface, options.FontRenderingEmSize, options.BidiLevel);

        if (isRightToLeft)
        {
            entries.Reverse();
        }

        for (var i = 0; i < entries.Count; i++)
        {
            shapedBuffer[i] = entries[i];
        }

        return shapedBuffer;
    }

    private static ReadOnlySpan<char> NormalizeBreakCharacters(ReadOnlySpan<char> span, out char[]? rentedBuffer)
    {
        rentedBuffer = null;

        for (var i = 0; i < span.Length; i++)
        {
            if (!new Codepoint(span[i]).IsBreakChar)
            {
                continue;
            }

            var buffer = ArrayPool<char>.Shared.Rent(span.Length);
            span.CopyTo(buffer);

            var bufferSpan = buffer.AsSpan(0, span.Length);
            for (var j = 0; j < bufferSpan.Length; j++)
            {
                if (new Codepoint(bufferSpan[j]).IsBreakChar)
                {
                    bufferSpan[j] = ZeroWidthNonJoiner;
                }
            }

            rentedBuffer = buffer;
            return bufferSpan;
        }

        return span;
    }
}
