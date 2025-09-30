using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace HarfBuzzSharp;

public sealed class Buffer : IDisposable
{
    private char[] _text = Array.Empty<char>();
    private uint[] _codepoints = Array.Empty<uint>();
    private uint[] _clusters = Array.Empty<uint>();
    private GlyphInfo[] _glyphInfos = Array.Empty<GlyphInfo>();
    private GlyphPosition[] _glyphPositions = Array.Empty<GlyphPosition>();
    private int _length;
    private int _originalLength;
    private int _textLength;
    private int _clusterOffset;

    public Direction Direction { get; set; } = Direction.LeftToRight;
    public Language Language { get; set; } = Language.FromBcp47("und");

    public int Length => _length;

    internal int OriginalLength => _originalLength;
    internal int TextLength => _textLength;
    internal int ClusterOffset => _clusterOffset;

    internal ReadOnlySpan<char> TextSpan => _text.AsSpan(0, _textLength);
    internal ReadOnlySpan<uint> OriginalCodepoints => _codepoints.AsSpan(0, _originalLength);
    internal ReadOnlySpan<uint> OriginalClusters => _clusters.AsSpan(0, _originalLength);

    public void Reset()
    {
        _length = 0;
        _originalLength = 0;
        _textLength = 0;
        _clusterOffset = 0;
        Direction = Direction.LeftToRight;
        Language = Language.FromBcp47("und");
    }

    public void AddUtf16(ReadOnlySpan<char> text, int start, int length)
    {
        if (start < 0 || length < 0 || start + length > text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        EnsureTextCapacity(length);
        text.Slice(start, length).CopyTo(_text);
        _textLength = length;
        _clusterOffset = start;

        var substring = _text.AsSpan(0, length);
        var glyphCount = 0;
        var offset = 0;
        while (offset < substring.Length)
        {
            if (Rune.DecodeFromUtf16(substring.Slice(offset), out var rune, out var consumed) != OperationStatus.Done)
            {
                break;
            }

            glyphCount++;
            offset += consumed;
        }

        EnsureGlyphCapacity(glyphCount);
        _originalLength = glyphCount;
        _length = glyphCount;

        offset = 0;
        for (var i = 0; i < glyphCount; i++)
        {
            Rune.DecodeFromUtf16(substring.Slice(offset), out var rune, out var consumed);
            _codepoints[i] = (uint)rune.Value;
            _clusters[i] = (uint)(_clusterOffset + offset);
            _glyphInfos[i] = new GlyphInfo((uint)rune.Value, (uint)(_clusterOffset + offset));
            _glyphPositions[i] = default;
            offset += consumed;
        }
    }

    public void AddUtf16(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        AddUtf16(text.AsSpan(), 0, text.Length);
    }

    public void GuessSegmentProperties()
    {
    }

    public void Reverse()
    {
        Array.Reverse(_glyphInfos, 0, _length);
        Array.Reverse(_glyphPositions, 0, _length);
    }

    public Span<GlyphInfo> GetGlyphInfoSpan() => _glyphInfos.AsSpan(0, _length);

    public Span<GlyphPosition> GetGlyphPositionSpan() => _glyphPositions.AsSpan(0, _length);

    public void Dispose()
    {
    }

    internal void SetLength(int length)
    {
        EnsureGlyphCapacity(length);
        _length = length;
    }

    internal void SetGlyph(int index, uint glyphId, uint cluster)
    {
        _glyphInfos[index] = new GlyphInfo(glyphId, cluster);
    }

    internal void SetPosition(int index, float xAdvance, float yAdvance, float xOffset, float yOffset)
    {
        _glyphPositions[index] = new GlyphPosition(xAdvance, yAdvance, xOffset, yOffset);
    }

    internal void PopulateFallback(IntPtr fontHandle, float scale)
    {
        SetLength(_originalLength);
        for (var i = 0; i < _originalLength; i++)
        {
            var codepoint = _codepoints[i];
            ushort glyph = 0;
            if (fontHandle != IntPtr.Zero)
            {
                if (VelloSharp.NativeMethods.vello_font_get_glyph_index(fontHandle, codepoint, out var mapped) == VelloSharp.VelloStatus.Success)
                {
                    glyph = mapped;
                }
            }

            SetGlyph(i, glyph, _clusters[i]);

            float advance = 0f;
            if (fontHandle != IntPtr.Zero && glyph != 0 &&
                VelloSharp.NativeMethods.vello_font_get_glyph_metrics(fontHandle, glyph, scale, out var metrics) == VelloSharp.VelloStatus.Success)
            {
                advance = metrics.Advance;
            }

            SetPosition(i, advance, 0f, 0f, 0f);
        }
    }

    private void EnsureTextCapacity(int length)
    {
        if (_text.Length < length)
        {
            Array.Resize(ref _text, length);
        }
    }

    private void EnsureGlyphCapacity(int count)
    {
        if (_codepoints.Length < count)
        {
            Array.Resize(ref _codepoints, count);
        }

        if (_clusters.Length < count)
        {
            Array.Resize(ref _clusters, count);
        }

        if (_glyphInfos.Length < count)
        {
            Array.Resize(ref _glyphInfos, count);
        }

        if (_glyphPositions.Length < count)
        {
            Array.Resize(ref _glyphPositions, count);
        }
    }

    public GlyphInfo[] GlyphInfos => _glyphInfos.AsSpan(0, _length).ToArray();

    public GlyphPosition[] GlyphPositions => _glyphPositions.AsSpan(0, _length).ToArray();
}
