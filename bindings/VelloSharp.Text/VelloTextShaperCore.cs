using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
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

        _ = fontDataProvider;
        _ = faceIndex;

        var languageBytes = GetLanguageBytes(options.Culture);
        var languageLength = languageBytes is null ? (nuint)0 : (nuint)(languageBytes.Length - 1);

        var variationArray = PrepareVariations(options.VariationAxes, out var variationSpan);
        try
        {
            var segments = GetScriptSegments(text);

            if (segments.Count == 0)
            {
                segments = new List<ScriptSegment>(1)
                {
                    new ScriptSegment(0, text.Length, 0)
                };
            }

            if (!options.EnableScriptSegmentation && segments.Count > 1)
            {
                var scriptTag = segments[0].ScriptTag;
                segments = new List<ScriptSegment>(1)
                {
                    new ScriptSegment(0, text.Length, scriptTag)
                };
            }

            var results = new List<VelloGlyph>();
            foreach (var segment in segments)
            {
                if (segment.Length <= 0)
                {
                    continue;
                }

                var slice = text.Slice(segment.Start, segment.Length);
                var glyphs = ShapeSegment(
                    fontHandle,
                    slice,
                    options,
                    segment.Start,
                    segment.ScriptTag,
                    variationSpan,
                    languageBytes,
                    languageLength);

                if (glyphs.Count == 0)
                {
                    continue;
                }

                results.AddRange(glyphs);
            }

            return results.Count == 0 ? Array.Empty<VelloGlyph>() : results;
        }
        finally
        {
            if (variationArray is not null)
            {
                ArrayPool<VelloVariationAxisValueNative>.Shared.Return(variationArray);
            }
        }
    }

    private static IReadOnlyList<VelloGlyph> ShapeSegment(
        IntPtr fontHandle,
        ReadOnlySpan<char> text,
        VelloTextShaperOptions options,
        int baseCluster,
        uint scriptTag,
        ReadOnlySpan<VelloVariationAxisValueNative> variations,
        byte[]? languageBytes,
        nuint languageLength)
    {
        Span<VelloOpenTypeFeatureNative> featureSpan = Span<VelloOpenTypeFeatureNative>.Empty;
        VelloOpenTypeFeatureNative[]? featureBuffer = null;

        if (options.Features is { Count: > 0 } features)
        {
            var featureCount = CountFeaturesForSegment(features, baseCluster, text.Length);
            if (featureCount > 0)
            {
                featureBuffer = ArrayPool<VelloOpenTypeFeatureNative>.Shared.Rent(featureCount);
                featureSpan = featureBuffer.AsSpan(0, featureCount);
                PopulateFeaturesForSegment(features, baseCluster, text.Length, featureSpan);
            }
        }

        try
        {
            var glyphs = ShapeWithVello(
                fontHandle,
                text,
                options,
                baseCluster,
                scriptTag,
                featureSpan,
                variations,
                languageBytes,
                languageLength);

            if (glyphs.Count > 0)
            {
                return glyphs;
            }

            return ShapeFallback(fontHandle, text, options, baseCluster);
        }
        finally
        {
            if (featureBuffer is not null)
            {
                ArrayPool<VelloOpenTypeFeatureNative>.Shared.Return(featureBuffer);
            }
        }
    }

    private static unsafe IReadOnlyList<VelloGlyph> ShapeWithVello(
        IntPtr fontHandle,
        ReadOnlySpan<char> text,
        VelloTextShaperOptions options,
        int baseCluster,
        uint scriptTag,
        ReadOnlySpan<VelloOpenTypeFeatureNative> features,
        ReadOnlySpan<VelloVariationAxisValueNative> variations,
        byte[]? languageBytes,
        nuint languageLength)
    {
        if (text.IsEmpty)
        {
            return Array.Empty<VelloGlyph>();
        }

        VelloShapedRunNative run;
        IntPtr handle;
        var status = VelloStatus.Success;

        fixed (char* textPtr = text)
        fixed (VelloOpenTypeFeatureNative* featurePtr = features)
        fixed (VelloVariationAxisValueNative* variationPtr = variations)
        {
            Span<byte> languageSpan = languageBytes is null ? Span<byte>.Empty : languageBytes.AsSpan();
            fixed (byte* languagePtr = languageSpan)
            {
                var optionsNative = new VelloTextShapeOptionsNative
                {
                    FontSize = options.FontSize,
                    Direction = options.IsRightToLeft ? 1 : 0,
                    ScriptTag = scriptTag,
                    Language = languageBytes is null ? null : languagePtr,
                    LanguageLength = languageLength,
                    Features = featurePtr,
                    FeatureCount = (nuint)features.Length,
                    VariationAxes = variationPtr,
                    VariationAxisCount = (nuint)variations.Length,
                };

                status = NativeMethods.vello_text_shape_utf16(
                    fontHandle,
                    (ushort*)textPtr,
                    (nuint)text.Length,
                    &optionsNative,
                    out run,
                    out handle);
            }
        }

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
                    glyph.XAdvance + options.LetterSpacing,
                    0f,
                    glyph.XOffset,
                    glyph.YOffset);
            }

            if (options.IsRightToLeft && glyphs.Length > 1)
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

    private static IReadOnlyList<VelloGlyph> ShapeFallback(
        IntPtr fontHandle,
        ReadOnlySpan<char> text,
        VelloTextShaperOptions options,
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
                status = NativeMethods.vello_font_get_glyph_metrics(fontHandle, glyphId, options.FontSize, out var metrics);
                if (status == VelloStatus.Success)
                {
                    advance = metrics.Advance;
                }
            }

            advance += options.LetterSpacing;
            glyphs.Add(new VelloGlyph(glyphId, (uint)(baseCluster + cluster), advance, 0f, 0f, 0f));
            cluster += rune.Utf16SequenceLength;
        }

        if (glyphs.Count == 0)
        {
            return Array.Empty<VelloGlyph>();
        }

        if (options.IsRightToLeft)
        {
            glyphs.Reverse();
        }

        return glyphs;
    }

    private static unsafe List<ScriptSegment> GetScriptSegments(ReadOnlySpan<char> text)
    {
        var segments = new List<ScriptSegment>();
        if (text.IsEmpty)
        {
            return segments;
        }

        fixed (char* textPtr = text)
        {
            var status = NativeMethods.vello_text_segment_utf16(
                (ushort*)textPtr,
                (nuint)text.Length,
                out var handle,
                out var array);

            try
            {
                if (status != VelloStatus.Success || array.Count == 0 || array.Segments == IntPtr.Zero)
                {
                    return segments;
                }

                var span = new ReadOnlySpan<VelloScriptSegmentNative>((void*)array.Segments, checked((int)array.Count));
                foreach (var segment in span)
                {
                    var start = (int)segment.Start;
                    var length = (int)segment.Length;
                    if (length <= 0)
                    {
                        continue;
                    }

                    segments.Add(new ScriptSegment(start, length, segment.ScriptTag));
                }
            }
            finally
            {
                if (handle != IntPtr.Zero)
                {
                    NativeMethods.vello_text_segments_destroy(handle);
                }
            }
        }

        segments.Sort((a, b) => a.Start.CompareTo(b.Start));
        return NormalizeSegments(segments, text.Length);
    }

    private static List<ScriptSegment> NormalizeSegments(List<ScriptSegment> segments, int totalLength)
    {
        if (segments.Count == 0)
        {
            return segments;
        }

        var normalized = new List<ScriptSegment>(segments.Count);
        var position = 0;
        uint lastScript = 0;

        foreach (var segment in segments)
        {
            var start = Math.Clamp(segment.Start, 0, totalLength);
            if (start >= totalLength)
            {
                break;
            }

            var available = totalLength - start;
            if (available <= 0)
            {
                continue;
            }

            var length = Math.Min(segment.Length, available);
            if (length <= 0)
            {
                continue;
            }

            if (start > position)
            {
                normalized.Add(new ScriptSegment(position, start - position, lastScript));
                position = start;
            }

            var scriptTag = segment.ScriptTag != 0 ? segment.ScriptTag : lastScript;
            normalized.Add(new ScriptSegment(start, length, scriptTag));
            position = start + length;

            if (scriptTag != 0)
            {
                lastScript = scriptTag;
            }
        }

        if (position < totalLength)
        {
            normalized.Add(new ScriptSegment(position, totalLength - position, lastScript));
        }

        return normalized;
    }

    private static VelloVariationAxisValueNative[]? PrepareVariations(
        IReadOnlyList<VelloVariationAxisValue>? axes,
        out ReadOnlySpan<VelloVariationAxisValueNative> span)
    {
        if (axes is not { Count: > 0 })
        {
            span = ReadOnlySpan<VelloVariationAxisValueNative>.Empty;
            return null;
        }

        var buffer = ArrayPool<VelloVariationAxisValueNative>.Shared.Rent(axes.Count);
        var slice = buffer.AsSpan(0, axes.Count);
        for (var i = 0; i < axes.Count; i++)
        {
            slice[i] = new VelloVariationAxisValueNative
            {
                Tag = EncodeTag(axes[i].Tag),
                Value = axes[i].Value,
            };
        }

        span = slice;
        return buffer;
    }

    private static int CountFeaturesForSegment(
        IReadOnlyList<VelloOpenTypeFeature> features,
        int segmentStart,
        int segmentLength)
    {
        var segmentEnd = segmentStart + segmentLength;
        var count = 0;

        for (var i = 0; i < features.Count; i++)
        {
            var feature = features[i];
            var featureStart = feature.Start > int.MaxValue ? int.MaxValue : (int)feature.Start;
            var featureEnd = feature.End == uint.MaxValue ? int.MaxValue : (int)Math.Min(feature.End, int.MaxValue);

            if (featureEnd <= segmentStart || featureStart >= segmentEnd)
            {
                continue;
            }

            var start = Math.Max(segmentStart, featureStart);
            var end = feature.End == uint.MaxValue ? segmentEnd : Math.Min(segmentEnd, featureEnd);
            if (start >= end)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private static void PopulateFeaturesForSegment(
        IReadOnlyList<VelloOpenTypeFeature> features,
        int segmentStart,
        int segmentLength,
        Span<VelloOpenTypeFeatureNative> destination)
    {
        var segmentEnd = segmentStart + segmentLength;
        var index = 0;

        for (var i = 0; i < features.Count; i++)
        {
            var feature = features[i];
            var featureStart = feature.Start > int.MaxValue ? int.MaxValue : (int)feature.Start;
            var featureEnd = feature.End == uint.MaxValue ? int.MaxValue : (int)Math.Min(feature.End, int.MaxValue);

            if (featureEnd <= segmentStart || featureStart >= segmentEnd)
            {
                continue;
            }

            var start = Math.Max(segmentStart, featureStart);
            var end = feature.End == uint.MaxValue ? segmentEnd : Math.Min(segmentEnd, featureEnd);
            if (start >= end)
            {
                continue;
            }

            var relativeStart = start - segmentStart;
            var relativeEnd = end - segmentStart;

            destination[index++] = new VelloOpenTypeFeatureNative
            {
                Tag = EncodeTag(feature.Tag),
                Value = feature.Value < 0 ? 0u : (uint)feature.Value,
                Start = (uint)relativeStart,
                End = feature.End == uint.MaxValue ? uint.MaxValue : (uint)relativeEnd,
            };
        }
    }

    private static byte[]? GetLanguageBytes(CultureInfo? culture)
    {
        if (culture is null)
        {
            return null;
        }

        var tag = culture.IetfLanguageTag;
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var normalized = tag.Replace('_', '-');
        var bytes = Encoding.UTF8.GetBytes(normalized);
        var buffer = new byte[bytes.Length + 1];
        bytes.CopyTo(buffer, 0);
        buffer[^1] = 0;
        return buffer;
    }

    private static uint EncodeTag(string tag) => EncodeTag(tag.AsSpan());

    private static uint EncodeTag(ReadOnlySpan<char> tag)
    {
        if (tag.Length != 4)
        {
            throw new ArgumentException("OpenType tags must be exactly four characters long.", nameof(tag));
        }

        return ((uint)tag[0] << 24) | ((uint)tag[1] << 16) | ((uint)tag[2] << 8) | tag[3];
    }

    private readonly record struct ScriptSegment(int Start, int Length, uint ScriptTag);
}
