using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using VelloSharp;

namespace VelloSharp.Text;

public readonly record struct VelloGlyph(
    uint GlyphId,
    uint Cluster,
    float XAdvance,
    float YAdvance,
    float XOffset,
    float YOffset);

public static class VelloTextShaperCore
{
    public static IReadOnlyList<VelloGlyph> ShapeUtf16(
        IntPtr fontHandle,
        ReadOnlySpan<char> text,
        float fontSize,
        bool isRightToLeft,
        float letterSpacing = 0f)
        => ShapeUtf16(fontHandle, text, VelloTextShaperOptions.CreateDefault(fontSize, isRightToLeft, letterSpacing));

    public static IReadOnlyList<VelloGlyph> ShapeUtf16(
        IntPtr fontHandle,
        ReadOnlySpan<char> text,
        VelloTextShaperOptions options,
        Func<ReadOnlyMemory<byte>>? fontDataProvider = null,
        uint faceIndex = 0)
    {
        if (text.IsEmpty || fontHandle == IntPtr.Zero)
        {
            return Array.Empty<VelloGlyph>();
        }

        var segments = options.EnableScriptSegmentation
            ? SegmentByScript(text)
            : new List<(int Start, int Length, VelloScriptClass Script)> { (0, text.Length, VelloScriptClass.Unknown) };

        var results = new List<VelloGlyph>();
        foreach (var (start, length, _) in segments)
        {
            var span = text.Slice(start, length);
            var segmentGlyphs = ShapeSegment(fontHandle, span, options, baseCluster: start, fontDataProvider, faceIndex);
            if (segmentGlyphs.Count == 0)
            {
                continue;
            }

            results.AddRange(segmentGlyphs);
        }

        return results.Count == 0 ? Array.Empty<VelloGlyph>() : results;
    }

    private static IReadOnlyList<VelloGlyph> ShapeSegment(
        IntPtr fontHandle,
        ReadOnlySpan<char> text,
        VelloTextShaperOptions options,
        int baseCluster,
        Func<ReadOnlyMemory<byte>>? fontDataProvider,
        uint faceIndex)
    {
        _ = fontDataProvider;
        _ = faceIndex;
        var shouldUseFallback = (options.Features is { Count: > 0 }) || (options.VariationAxes is { Count: > 0 });

        if (!shouldUseFallback)
        {
            var glyphs = ShapeWithVello(fontHandle, text, options.FontSize, options.IsRightToLeft, options.LetterSpacing, baseCluster);
            if (glyphs.Count > 0)
            {
                return glyphs;
            }
        }

        return ShapeFallback(fontHandle, text, options.FontSize, options.IsRightToLeft, options.LetterSpacing, baseCluster);
    }

    private static IReadOnlyList<VelloGlyph> ShapeWithVello(
        IntPtr fontHandle,
        ReadOnlySpan<char> text,
        float fontSize,
        bool isRightToLeft,
        float letterSpacing,
        int baseCluster)
    {
        unsafe
        {
            fixed (char* textPtr = text)
            {
                var status = NativeMethods.vello_text_shape_utf16(
                    fontHandle,
                    (ushort*)textPtr,
                    (nuint)text.Length,
                    fontSize,
                    isRightToLeft ? 1 : 0,
                    out var run,
                    out var handle);

                if (status != VelloStatus.Success || run.Glyphs == IntPtr.Zero)
                {
                    return Array.Empty<VelloGlyph>();
                }

                try
                {
                    var glyphCount = checked((int)run.GlyphCount);
                    var glyphs = new VelloGlyph[glyphCount];
                    var span = new ReadOnlySpan<VelloShapedGlyphNative>((void*)run.Glyphs, glyphCount);
                    for (var i = 0; i < glyphCount; i++)
                    {
                        ref readonly var glyph = ref span[i];
                        glyphs[i] = new VelloGlyph(
                            glyph.GlyphId,
                            (uint)(baseCluster + glyph.Cluster),
                            glyph.XAdvance + letterSpacing,
                            0f,
                            glyph.XOffset,
                            glyph.YOffset);
                    }

                    if (isRightToLeft)
                    {
                        Array.Reverse(glyphs);
                    }

                    return glyphs;
                }
                finally
                {
                    NativeMethods.vello_text_shape_destroy(handle);
                }
            }
        }
    }

    private static IReadOnlyList<VelloGlyph> ShapeFallback(
        IntPtr fontHandle,
        ReadOnlySpan<char> text,
        float fontSize,
        bool isRightToLeft,
        float letterSpacing,
        int baseCluster)
    {
        var glyphs = new List<VelloGlyph>(text.Length);
        var enumerator = text.EnumerateRunes();
        var cluster = 0;
        foreach (var rune in enumerator)
        {
            ushort glyphId = 0;
            var status = NativeMethods.vello_font_get_glyph_index(fontHandle, (uint)rune.Value, out glyphId);
            if (status != VelloStatus.Success)
            {
                glyphId = 0;
            }

            float advance = 0f;
            if (glyphId != 0)
            {
                status = NativeMethods.vello_font_get_glyph_metrics(fontHandle, glyphId, fontSize, out var metrics);
                if (status == VelloStatus.Success)
                {
                    advance = metrics.Advance;
                }
            }

            advance += letterSpacing;
            glyphs.Add(new VelloGlyph(glyphId, (uint)(baseCluster + cluster), advance, 0f, 0f, 0f));
            cluster += rune.Utf16SequenceLength;
        }

        if (glyphs.Count == 0)
        {
            return Array.Empty<VelloGlyph>();
        }

        if (isRightToLeft)
        {
            glyphs.Reverse();
        }

        return glyphs;
    }

    private static List<(int Start, int Length, VelloScriptClass Script)> SegmentByScript(ReadOnlySpan<char> text)
    {
        var segments = new List<(int Start, int Length, VelloScriptClass Script)>();
        if (text.IsEmpty)
        {
            return segments;
        }

        var enumerator = text.EnumerateRunes();
        var start = 0;
        var length = 0;
        VelloScriptClass currentScript = VelloScriptClass.Unknown;

        while (enumerator.MoveNext())
        {
            var rune = enumerator.Current;
            var script = GetScript(rune);
            if (length > 0 && script != currentScript && script != VelloScriptClass.Unknown)
            {
                segments.Add((start, length, currentScript));
                start += length;
                length = 0;
            }

            currentScript = script;
            length += rune.Utf16SequenceLength;
        }

        if (length > 0)
        {
            segments.Add((start, length, currentScript));
        }

        return segments;
    }

    private static VelloScriptClass GetScript(Rune rune)
    {
        var value = rune.Value;

        if (value <= 0x024F)
        {
            return VelloScriptClass.Latin;
        }

        if (value is >= 0x0370 and <= 0x03FF)
        {
            return VelloScriptClass.Greek;
        }

        if (value is >= 0x0400 and <= 0x052F)
        {
            return VelloScriptClass.Cyrillic;
        }

        if (value is >= 0x0600 and <= 0x06FF)
        {
            return VelloScriptClass.Arabic;
        }

        if (value is >= 0x4E00 and <= 0x9FFF)
        {
            return VelloScriptClass.Han;
        }

        return VelloScriptClass.Unknown;
    }
}
