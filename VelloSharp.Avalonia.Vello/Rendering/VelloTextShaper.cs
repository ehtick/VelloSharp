using System;
using System.Collections.Generic;
using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Platform;

namespace VelloSharp.Avalonia.Vello.Rendering;

internal sealed class VelloTextShaper : ITextShaperImpl
{
    public ShapedBuffer ShapeText(ReadOnlyMemory<char> text, TextShaperOptions options)
    {
        if (text.IsEmpty)
        {
            return new ShapedBuffer(text, 0, options.Typeface, options.FontRenderingEmSize, options.BidiLevel);
        }

        var typeface = options.Typeface;
        var font = VelloFontManager.GetFont(typeface);
        var isRightToLeft = (options.BidiLevel & 1) != 0;

        unsafe
        {
            fixed (char* textPtr = text.Span)
            {
                var status = NativeMethods.vello_text_shape_utf16(
                    font.Handle,
                    (ushort*)textPtr,
                    (nuint)text.Length,
                    (float)options.FontRenderingEmSize,
                    isRightToLeft ? 1 : 0,
                    out var run,
                    out var handle);

                if (status != VelloStatus.Success || run.Glyphs == IntPtr.Zero)
                {
                    return ShapeWithFallback(text, options);
                }

                try
                {
                    var glyphCount = checked((int)run.GlyphCount);
                    var shapedBuffer = new ShapedBuffer(text, glyphCount, typeface, options.FontRenderingEmSize, options.BidiLevel);
                    var glyphSpan = new ReadOnlySpan<VelloShapedGlyphNative>((void*)run.Glyphs, glyphCount);


                    for (var i = 0; i < glyphCount; i++)
                    {
                        ref readonly var glyph = ref glyphSpan[i];
                        var advance = glyph.XAdvance + options.LetterSpacing;
                        var offset = new Vector(glyph.XOffset, -glyph.YOffset);
                        shapedBuffer[i] = new GlyphInfo((ushort)glyph.GlyphId, (int)glyph.Cluster, advance, offset);
                    }

                    return shapedBuffer;
                }
                finally
                {
                    NativeMethods.vello_text_shape_destroy(handle);
                }
            }
        }
    }

    private static ShapedBuffer ShapeWithFallback(ReadOnlyMemory<char> text, TextShaperOptions options)
    {
        var span = text.Span;
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
}
