using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace HarfBuzzSharp;

public sealed class Buffer : IDisposable
{
    public const int DefaultReplacementCodepoint = '\uFFFD';

    private char[] _text = Array.Empty<char>();
    private uint[] _codepoints = Array.Empty<uint>();
    private uint[] _clusters = Array.Empty<uint>();
    private GlyphInfo[] _glyphInfos = Array.Empty<GlyphInfo>();
    private GlyphPosition[] _glyphPositions = Array.Empty<GlyphPosition>();
    private int _length;
    private int _originalLength;
    private int _textLength;
    private int _clusterOffset;
    private ContentType _contentType = ContentType.Invalid;
    private BufferFlags _flags = BufferFlags.Default;
    private ClusterLevel _clusterLevel = ClusterLevel.Default;
    private Script _script = Script.Common;
    private UnicodeFunctions _unicodeFunctions = UnicodeFunctions.Empty;
    private uint _replacementCodepoint = DefaultReplacementCodepoint;
    private uint _invisibleGlyph;

    public Direction Direction { get; set; } = Direction.LeftToRight;
    public Language Language { get; set; } = Language.FromBcp47("und");

    public ContentType ContentType
    {
        get => _contentType;
        set => _contentType = value;
    }

    public BufferFlags Flags
    {
        get => _flags;
        set => _flags = value;
    }

    public ClusterLevel ClusterLevel
    {
        get => _clusterLevel;
        set => _clusterLevel = value;
    }

    public uint ReplacementCodepoint
    {
        get => _replacementCodepoint;
        set => _replacementCodepoint = value;
    }

    public uint InvisibleGlyph
    {
        get => _invisibleGlyph;
        set => _invisibleGlyph = value;
    }

    public Script Script
    {
        get => _script;
        set => _script = value;
    }

    public UnicodeFunctions UnicodeFunctions
    {
        get => _unicodeFunctions;
        set => _unicodeFunctions = value ?? UnicodeFunctions.Empty;
    }

    public int Length => _length;

    internal int OriginalLength => _originalLength;
    internal int TextLength => _textLength;
    internal int ClusterOffset => _clusterOffset;

    internal ReadOnlySpan<char> TextSpan => _text.AsSpan(0, _textLength);
    internal ReadOnlySpan<uint> OriginalCodepoints => _codepoints.AsSpan(0, _originalLength);
    internal ReadOnlySpan<uint> OriginalClusters => _clusters.AsSpan(0, _originalLength);

    public void Reset()
    {
        ClearContents();
        Direction = Direction.LeftToRight;
        Language = Language.FromBcp47("und");
        _contentType = ContentType.Invalid;
        _flags = BufferFlags.Default;
        _clusterLevel = ClusterLevel.Default;
        _script = Script.Common;
        _replacementCodepoint = DefaultReplacementCodepoint;
        _invisibleGlyph = 0;
        _unicodeFunctions = UnicodeFunctions.Empty;
    }

    public void ClearContents()
    {
        _length = 0;
        _originalLength = 0;
        _textLength = 0;
        _clusterOffset = 0;
    }

    public void Add(uint codepoint, uint cluster)
    {
        EnsureCanAcceptText();

        EnsureGlyphCapacity(_length + 1);
        _codepoints[_length] = codepoint;
        _clusters[_length] = cluster;
        _glyphInfos[_length] = new GlyphInfo(codepoint, cluster);
        _glyphPositions[_length] = default;
        _length++;
        _originalLength = _length;
        _contentType = ContentType.Unicode;
    }

    public void AddUtf16(ReadOnlySpan<char> text, int start, int length)
    {
        EnsureCanAcceptText();

        if (start < 0 || length < 0 || start + length > text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        EnsureTextCapacity(length);
        text.Slice(start, length).CopyTo(_text);
        _textLength = length;
        _clusterOffset = start;
        _contentType = ContentType.Unicode;

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

    public void AddUtf8(ReadOnlySpan<byte> text) => AddUtf8(text, 0, -1);

    public void AddUtf8(ReadOnlySpan<byte> text, int itemOffset, int itemLength)
    {
        EnsureCanAcceptText();

        if (itemOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(itemOffset));
        }

        if (itemLength < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(itemLength));
        }

        if (itemOffset > text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(itemOffset));
        }

        var slice = itemLength < 0 ? text[itemOffset..] : text.Slice(itemOffset, itemLength);
        if (slice.Length == 0)
        {
            return;
        }

        using var buffer = MemoryPool<char>.Shared.Rent(Encoding.UTF8.GetMaxCharCount(slice.Length));
        var span = buffer.Memory.Span;
        var decodedLength = Encoding.UTF8.GetChars(slice, span);
        AddUtf16(span.Slice(0, decodedLength), 0, decodedLength);
    }

    public void AddUtf8(IntPtr text, int textLength, int itemOffset, int itemLength)
    {
        if (text == IntPtr.Zero)
        {
            throw new ArgumentNullException(nameof(text));
        }

        unsafe
        {
            var span = new ReadOnlySpan<byte>((byte*)text, textLength);
            AddUtf8(span, itemOffset, itemLength);
        }
    }

    public void AddUtf32(ReadOnlySpan<int> text) => AddUtf32(text, 0, -1);

    public void AddUtf32(ReadOnlySpan<int> text, int itemOffset, int itemLength)
    {
        EnsureCanAcceptText();

        if (itemOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(itemOffset));
        }

        if (itemLength < -1)
        {
            throw new ArgumentOutOfRangeException(nameof(itemLength));
        }

        if (itemOffset > text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(itemOffset));
        }

        var slice = itemLength < 0 ? text[itemOffset..] : text.Slice(itemOffset, itemLength);
        EnsureGlyphCapacity(slice.Length);
        ClearContents();

        for (var i = 0; i < slice.Length; i++)
        {
            var codepoint = (uint)slice[i];
            _codepoints[i] = codepoint;
            _clusters[i] = (uint)(_clusterOffset + i);
            _glyphInfos[i] = new GlyphInfo(codepoint, (uint)(_clusterOffset + i));
            _glyphPositions[i] = default;
        }

        _length = slice.Length;
        _originalLength = _length;
        _contentType = ContentType.Unicode;
    }

    public void GuessSegmentProperties()
    {
        if (_contentType != ContentType.Unicode || _length == 0)
        {
            return;
        }

        if (_unicodeFunctions.Script is not null)
        {
            var codepoint = _codepoints[0];
            _script = _unicodeFunctions.Script(_unicodeFunctions, codepoint);
            var direction = _script.HorizontalDirection;
            if (direction == Direction.LeftToRight || direction == Direction.RightToLeft)
            {
                Direction = direction;
            }
        }
        else
        {
            _script = Script.Unknown;
        }
    }

    public void Reverse()
    {
        Array.Reverse(_glyphInfos, 0, _length);
        Array.Reverse(_glyphPositions, 0, _length);
    }

    public void ReverseRange(int start, int end)
    {
        if (start < 0 || end < -1 || start >= _length)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (end == -1)
        {
            end = _length;
        }

        var count = end - start;
        Array.Reverse(_glyphInfos, start, count);
        Array.Reverse(_glyphPositions, start, count);
    }

    public void ReverseClusters()
    {
        if (_length <= 1)
        {
            return;
        }

        var index = 0;
        while (index < _length)
        {
            var end = GetClusterEnd(index, mergeUnsafeToBreak: true);
            var count = end - index;
            if (count > 1)
            {
                Array.Reverse(_glyphInfos, index, count);
                Array.Reverse(_glyphPositions, index, count);
            }

            index = end;
        }

        Reverse();
    }

    public void NormalizeGlyphs()
    {
        if (_contentType != ContentType.Glyphs)
        {
            throw new InvalidOperationException("ContentType should be of type Glyphs.");
        }

        if (_length == 0)
        {
            throw new InvalidOperationException("GlyphPositions can't be empty.");
        }

        if (_glyphPositions.Length < _length)
        {
            throw new InvalidOperationException("GlyphPositions can't be empty.");
        }

        var backward = Direction == Direction.RightToLeft || Direction == Direction.BottomToTop;
        var index = 0;
        while (index < _length)
        {
            var end = GetClusterEnd(index, mergeUnsafeToBreak: true);
            NormalizeGlyphCluster(index, end, backward);
            index = end;
        }
    }

    public string SerializeGlyphs() => SerializeGlyphs(0, -1, null, SerializeFormat.Text, SerializeFlag.Default);

    public string SerializeGlyphs(int start, int end) => SerializeGlyphs(start, end, null, SerializeFormat.Text, SerializeFlag.Default);

    public string SerializeGlyphs(Font font) => SerializeGlyphs(0, -1, font, SerializeFormat.Text, SerializeFlag.Default);

    public string SerializeGlyphs(Font? font, SerializeFormat format, SerializeFlag flags)
        => SerializeGlyphs(0, -1, font, format, flags);

    public string SerializeGlyphs(int start, int end, Font? font, SerializeFormat format, SerializeFlag flags)
    {
        if (_length == 0)
        {
            throw new InvalidOperationException("Buffer should not be empty.");
        }

        if (_contentType != ContentType.Glyphs)
        {
            throw new InvalidOperationException("ContentType should be of type Glyphs.");
        }

        if (end == -1)
        {
            end = _length;
        }

        if (start < 0 || start >= _length || end > _length || start >= end)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        return format switch
        {
            SerializeFormat.Text => SerializeGlyphsText(start, end, font, flags),
            SerializeFormat.Json => SerializeGlyphsJson(start, end, font, flags),
            _ => throw new NotSupportedException($"SerializeFormat '{format}' is not supported by the managed shim."),
        };
    }

    private string SerializeGlyphsText(int start, int end, Font? font, SerializeFlag flags)
    {
        var includeClusters = (flags & SerializeFlag.NoClusters) == 0;
        var includePositions = (flags & SerializeFlag.NoPositions) == 0;
        var includeAdvances = includePositions && (flags & SerializeFlag.NoAdvances) == 0;
        var includeGlyphNames = (flags & SerializeFlag.NoGlyphNames) == 0;
        var includeGlyphFlags = (flags & SerializeFlag.GlyphFlags) != 0;
        var includeExtents = (flags & SerializeFlag.GlyphExtents) != 0 && font is not null;

        var builder = new StringBuilder();
        builder.EnsureCapacity(Math.Max(16, (end - start) * 24));
        builder.Append('[');

        float accumX = 0f;
        float accumY = 0f;

        for (var i = start; i < end; i++)
        {
            if (i > start)
            {
                builder.Append('|');
            }

            ref readonly var info = ref _glyphInfos[i];
            ref readonly var position = ref _glyphPositions[i];

            if (includeGlyphNames)
            {
                builder.Append(FormatGlyphToken(font, info.Codepoint));
            }
            else
            {
                AppendUInt(builder, info.Codepoint);
            }

            if (includeClusters)
            {
                builder.Append('=');
                AppendUInt(builder, info.Cluster);
            }

            if (includePositions)
            {
                var dx = position.XOffset + accumX;
                var dy = position.YOffset + accumY;

                if (!IsEffectivelyZero(dx) || !IsEffectivelyZero(dy))
                {
                    builder.Append('@');
                    AppendInt(builder, dx);
                    builder.Append(',');
                    AppendInt(builder, dy);
                }

                if (includeAdvances)
                {
                    builder.Append('+');
                    AppendInt(builder, position.XAdvance);
                    if (!IsEffectivelyZero(position.YAdvance))
                    {
                        builder.Append(',');
                        AppendInt(builder, position.YAdvance);
                    }
                }
                else
                {
                    accumX += position.XAdvance;
                    accumY += position.YAdvance;
                }
            }

            if (includeGlyphFlags)
            {
                var mask = info.Flags & GlyphFlags.Defined;
                if (mask != 0)
                {
                    builder.Append('#');
                    builder.Append(((uint)mask).ToString("X", CultureInfo.InvariantCulture));
                }
            }

            if (includeExtents && font is not null && font.TryGetGlyphExtents((ushort)info.Codepoint, out var extents))
            {
                builder.Append('<');
                AppendInt(builder, extents.XBearing);
                builder.Append(',');
                AppendInt(builder, extents.YBearing);
                builder.Append(',');
                AppendInt(builder, extents.Width);
                builder.Append(',');
                AppendInt(builder, extents.Height);
                builder.Append('>');
            }
        }

        builder.Append(']');
        return builder.ToString();
    }

    private string SerializeGlyphsJson(int start, int end, Font? font, SerializeFlag flags)
    {
        var includeGlyphNames = (flags & SerializeFlag.NoGlyphNames) == 0;
        var includeClusters = (flags & SerializeFlag.NoClusters) == 0;
        var includePositions = (flags & SerializeFlag.NoPositions) == 0;
        var includeAdvances = includePositions && (flags & SerializeFlag.NoAdvances) == 0;
        var includeGlyphFlags = (flags & SerializeFlag.GlyphFlags) != 0;
        var includeExtents = (flags & SerializeFlag.GlyphExtents) != 0 && font is not null;

        var builder = new StringBuilder();
        builder.EnsureCapacity(Math.Max(16, (end - start) * 48));
        builder.Append('[');

        float accumX = 0f;
        float accumY = 0f;

        for (var i = start; i < end; i++)
        {
            if (i > start)
            {
                builder.Append(',');
            }

            ref readonly var info = ref _glyphInfos[i];
            ref readonly var position = ref _glyphPositions[i];

            builder.Append('{');
            builder.Append("\"g\":");
            if (includeGlyphNames)
            {
                AppendJsonString(builder, FormatGlyphToken(font, info.Codepoint));
            }
            else
            {
                AppendUInt(builder, info.Codepoint);
            }

            if (includeClusters)
            {
                builder.Append(",\"cl\":");
                AppendUInt(builder, info.Cluster);
            }

            if (includePositions)
            {
                var dx = position.XOffset + accumX;
                var dy = position.YOffset + accumY;

                builder.Append(",\"dx\":");
                AppendInt(builder, dx);
                builder.Append(",\"dy\":");
                AppendInt(builder, dy);

                if (includeAdvances)
                {
                    builder.Append(",\"ax\":");
                    AppendInt(builder, position.XAdvance);
                    builder.Append(",\"ay\":");
                    AppendInt(builder, position.YAdvance);
                }
                else
                {
                    accumX += position.XAdvance;
                    accumY += position.YAdvance;
                }
            }

            if (includeGlyphFlags)
            {
                var mask = info.Flags & GlyphFlags.Defined;
                if (mask != 0)
                {
                    builder.Append(",\"fl\":");
                    AppendUInt(builder, (uint)mask);
                }
            }

            if (includeExtents && font is not null && font.TryGetGlyphExtents((ushort)info.Codepoint, out var extents))
            {
                builder.Append(",\"xb\":");
                AppendInt(builder, extents.XBearing);
                builder.Append(",\"yb\":");
                AppendInt(builder, extents.YBearing);
                builder.Append(",\"w\":");
                AppendInt(builder, extents.Width);
                builder.Append(",\"h\":");
                AppendInt(builder, extents.Height);
            }

            builder.Append('}');
        }

        builder.Append(']');
        return builder.ToString();
    }

    public void DeserializeGlyphs(string data) => DeserializeGlyphs(data, null, SerializeFormat.Text);

    public void DeserializeGlyphs(string data, Font? font) => DeserializeGlyphs(data, font, SerializeFormat.Text);

    public void DeserializeGlyphs(string data, Font? font, SerializeFormat format)
    {
        if (_length != 0)
        {
            throw new InvalidOperationException("Buffer must be empty.");
        }

        if (_contentType == ContentType.Glyphs)
        {
            throw new InvalidOperationException("ContentType must not be Glyphs.");
        }

        if (string.IsNullOrWhiteSpace(data))
        {
            return;
        }

        switch (format)
        {
            case SerializeFormat.Text:
                DeserializeGlyphsText(data, font);
                break;
            case SerializeFormat.Json:
                DeserializeGlyphsJson(data, font);
                break;
            default:
                throw new NotSupportedException($"SerializeFormat '{format}' is not supported by the managed shim.");
        }
    }

    private void DeserializeGlyphsText(string data, Font? font)
    {
        var trimmed = data.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '[' || trimmed[^1] != ']')
        {
            throw new FormatException("Serialized glyph string has an invalid format.");
        }

        var payload = trimmed.Substring(1, trimmed.Length - 2);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return;
        }

        var entries = payload.Split('|', StringSplitOptions.RemoveEmptyEntries);
        EnsureGlyphCapacity(entries.Length);
        _length = entries.Length;
        _originalLength = _length;
        _contentType = ContentType.Glyphs;

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i].Trim();
            if (entry.Length == 0)
            {
                throw new FormatException("Serialized glyph entry cannot be empty.");
            }

            var cursor = 0;
            var glyphToken = ReadUntil(entry, ref cursor, '=', '@', '+', '#', '<');
            var glyphId = ParseGlyphToken(glyphToken, font);

            uint cluster = (uint)(_clusterOffset + i);
            float xOffset = 0f;
            float yOffset = 0f;
            float xAdvance = 0f;
            float yAdvance = 0f;
            GlyphFlags glyphFlags = 0;

            while (cursor < entry.Length)
            {
                switch (entry[cursor])
                {
                    case '=':
                        cursor++;
                        var clusterToken = ReadUntil(entry, ref cursor, '@', '+', '#', '<');
                        cluster = ParseUInt(clusterToken);
                        break;
                    case '@':
                        cursor++;
                        var offsetToken = ReadUntil(entry, ref cursor, '+', '#', '<');
                        var offsets = offsetToken.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        if (offsets.Length != 2)
                        {
                            throw new FormatException($"Unable to parse offset from '{offsetToken}'.");
                        }

                        xOffset = float.Parse(offsets[0], CultureInfo.InvariantCulture);
                        yOffset = float.Parse(offsets[1], CultureInfo.InvariantCulture);
                        break;
                    case '+':
                        cursor++;
                        var advanceToken = ReadUntil(entry, ref cursor, '#', '<');
                        var advances = advanceToken.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        if (advances.Length >= 1)
                        {
                            xAdvance = float.Parse(advances[0], CultureInfo.InvariantCulture);
                        }

                        if (advances.Length == 2)
                        {
                            yAdvance = float.Parse(advances[1], CultureInfo.InvariantCulture);
                        }
                        else if (advances.Length > 2)
                        {
                            throw new FormatException($"Unable to parse advance from '{advanceToken}'.");
                        }
                        break;
                    case '#':
                        cursor++;
                        var flagToken = ReadUntil(entry, ref cursor, '<');
                        if (string.IsNullOrWhiteSpace(flagToken))
                        {
                            throw new FormatException($"Unable to parse glyph flags from '{entry}'.");
                        }

                        glyphFlags = (GlyphFlags)Convert.ToUInt32(flagToken, 16);
                        break;
                    case '<':
                        var closeIndex = entry.IndexOf('>', cursor);
                        if (closeIndex == -1)
                        {
                            throw new FormatException($"Serialized glyph entry '{entry}' is missing '>' terminator.");
                        }

                        cursor = closeIndex + 1;
                        break;
                    default:
                        throw new FormatException($"Unexpected token '{entry[cursor]}' in serialized glyph entry '{entry}'.");
                }
            }

            _glyphInfos[i] = new GlyphInfo(glyphId, cluster, glyphFlags);
            _glyphPositions[i] = new GlyphPosition(xAdvance, yAdvance, xOffset, yOffset);
            _clusters[i] = cluster;
            _codepoints[i] = glyphId;
        }
    }

    private void DeserializeGlyphsJson(string data, Font? font)
    {
        using var document = JsonDocument.Parse(data);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("Serialized glyph JSON must be an array.");
        }

        var array = document.RootElement;
        if (array.GetArrayLength() == 0)
        {
            return;
        }

        EnsureGlyphCapacity(array.GetArrayLength());
        _length = array.GetArrayLength();
        _originalLength = _length;
        _contentType = ContentType.Glyphs;

        var index = 0;
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("Serialized glyph JSON must contain objects.");
            }

            if (!element.TryGetProperty("g", out var glyphProperty))
            {
                throw new FormatException("Serialized glyph JSON element is missing 'g' property.");
            }

            uint glyphId = glyphProperty.ValueKind switch
            {
                JsonValueKind.String => ParseGlyphToken(glyphProperty.GetString() ?? string.Empty, font),
                JsonValueKind.Number => glyphProperty.GetUInt32(),
                _ => throw new FormatException("Serialized glyph JSON 'g' property must be a string or number."),
            };

            uint cluster = element.TryGetProperty("cl", out var clusterProperty) && clusterProperty.ValueKind == JsonValueKind.Number
                ? clusterProperty.GetUInt32()
                : (uint)(_clusterOffset + index);

            float xOffset = element.TryGetProperty("dx", out var dxProperty) && dxProperty.ValueKind == JsonValueKind.Number
                ? dxProperty.GetInt32()
                : 0f;

            float yOffset = element.TryGetProperty("dy", out var dyProperty) && dyProperty.ValueKind == JsonValueKind.Number
                ? dyProperty.GetInt32()
                : 0f;

            float xAdvance = element.TryGetProperty("ax", out var axProperty) && axProperty.ValueKind == JsonValueKind.Number
                ? axProperty.GetInt32()
                : 0f;

            float yAdvance = element.TryGetProperty("ay", out var ayProperty) && ayProperty.ValueKind == JsonValueKind.Number
                ? ayProperty.GetInt32()
                : 0f;

            GlyphFlags glyphFlags = 0;
            if (element.TryGetProperty("fl", out var flagsProperty) && flagsProperty.ValueKind == JsonValueKind.Number)
            {
                glyphFlags = (GlyphFlags)flagsProperty.GetUInt32();
            }

            _glyphInfos[index] = new GlyphInfo(glyphId, cluster, glyphFlags);
            _glyphPositions[index] = new GlyphPosition(xAdvance, yAdvance, xOffset, yOffset);
            _clusters[index] = cluster;
            _codepoints[index] = glyphId;

            index++;
        }
    }

    private static void AppendInt(StringBuilder builder, float value)
    {
        var rounded = (int)MathF.Round(value);
        builder.Append(rounded.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendUInt(StringBuilder builder, uint value)
        => builder.Append(value.ToString(CultureInfo.InvariantCulture));

    private static bool IsEffectivelyZero(float value)
        => MathF.Abs(value) <= 0.0001f;

    private static string FormatGlyphToken(Font? font, uint glyphId)
    {
        if (font is not null && font.TryGetGlyphName(glyphId, out var name) && !string.IsNullOrEmpty(name))
        {
            return name;
        }

        return $"gid{glyphId}";
    }

    private static void AppendJsonString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var ch in value)
        {
            if (ch == '"' || ch == '\\')
            {
                builder.Append('\\');
            }

            builder.Append(ch);
        }

        builder.Append('"');
    }

    private static uint ParseGlyphToken(string token, Font? font)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new FormatException("Glyph token cannot be empty.");
        }

        token = token.Trim();

        if (uint.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            return numeric;
        }

        if (token.StartsWith("gid", StringComparison.OrdinalIgnoreCase) &&
            uint.TryParse(token.AsSpan(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out numeric))
        {
            return numeric;
        }

        if (font is not null && font.TryGetGlyphFromName(token, out var glyph))
        {
            return glyph;
        }

        throw new FormatException($"Unable to parse glyph token '{token}'.");
    }

    private static uint ParseUInt(string text)
    {
        if (!uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormatException($"Unable to parse unsigned integer from '{text}'.");
        }

        return value;
    }

    private static string ReadUntil(string text, ref int cursor, params char[] delimiters)
    {
        if (cursor >= text.Length)
        {
            return string.Empty;
        }

        var start = cursor;
        while (cursor < text.Length && Array.IndexOf(delimiters, text[cursor]) == -1)
        {
            cursor++;
        }

        return text[start..cursor];
    }

    public Span<GlyphInfo> GetGlyphInfoSpan() => _glyphInfos.AsSpan(0, _length);

    public Span<GlyphPosition> GetGlyphPositionSpan() => _glyphPositions.AsSpan(0, _length);

#if DEBUG
    public string DebugDescribeGlyphs(SerializeFormat format = SerializeFormat.Text, SerializeFlag flags = SerializeFlag.Default)
    {
        if (_length == 0 || _contentType != ContentType.Glyphs)
        {
            return "[]";
        }

        var effectiveFlags = flags;
        if ((effectiveFlags & SerializeFlag.NoGlyphNames) == 0)
        {
            effectiveFlags |= SerializeFlag.NoGlyphNames;
        }

        if ((effectiveFlags & SerializeFlag.GlyphFlags) == 0)
        {
            effectiveFlags |= SerializeFlag.GlyphFlags;
        }

        return SerializeGlyphs(0, -1, null, format, effectiveFlags);
    }

    public string DebugDescribeClusters()
    {
        if (_length == 0)
        {
            return "[]";
        }

        var builder = new StringBuilder();
        builder.Append('[');
        for (var i = 0; i < _length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(_glyphInfos[i].Cluster.ToString(CultureInfo.InvariantCulture));
        }

        builder.Append(']');
        return builder.ToString();
    }
#endif

    public void Dispose()
    {
    }

    internal void ApplyUnicodeProcessing()
    {
        if (_contentType != ContentType.Unicode || _length == 0)
        {
            return;
        }

        var modified = false;

        if (ShouldRemoveDefaultIgnorables())
        {
            modified |= RemoveDefaultIgnorables();
        }

        modified |= ApplyMirroringIfNeeded();
        modified |= ReorderCombiningMarks();

        if (modified)
        {
            UpdateTextFromCodepoints();
        }
    }

    internal void SetLength(int length)
    {
        EnsureGlyphCapacity(length);
        _length = length;
    }

    internal void SetGlyph(int index, uint glyphId, uint cluster, GlyphFlags flags = 0)
    {
        _glyphInfos[index] = new GlyphInfo(glyphId, cluster, flags);
    }

    internal void SetPosition(int index, float xAdvance, float yAdvance, float xOffset, float yOffset)
    {
        _glyphPositions[index] = new GlyphPosition(xAdvance, yAdvance, xOffset, yOffset);
    }

    internal void PopulateFallback(IntPtr fontHandle, float scale)
    {
        SetLength(_originalLength);
        _contentType = ContentType.Glyphs;
        for (var i = 0; i < _originalLength; i++)
        {
            var codepoint = _codepoints[i];
            ushort glyph = 0;
            if (fontHandle != IntPtr.Zero)
            {
                if (global::VelloSharp.NativeMethods.vello_font_get_glyph_index(fontHandle, codepoint, out var mapped) == global::VelloSharp.VelloStatus.Success)
                {
                    glyph = mapped;
                }
            }

            SetGlyph(i, glyph, _clusters[i]);

            float advance = 0f;
            if (fontHandle != IntPtr.Zero && glyph != 0 &&
                global::VelloSharp.NativeMethods.vello_font_get_glyph_metrics(fontHandle, glyph, scale, out var metrics) == global::VelloSharp.VelloStatus.Success)
            {
                advance = metrics.Advance;
            }

            SetPosition(i, advance, 0f, 0f, 0f);
        }
    }

    private bool ShouldRemoveDefaultIgnorables()
        => (_flags & BufferFlags.RemoveDefaultIgnorables) != 0
            && (_flags & BufferFlags.PreserveDefaultIgnorables) == 0;

    private bool RemoveDefaultIgnorables()
    {
        var write = 0;
        var modified = false;

        for (var read = 0; read < _length; read++)
        {
            var codepoint = _codepoints[read];
            if (IsDefaultIgnorable(codepoint))
            {
                modified = true;
                continue;
            }

            if (write != read)
            {
                _codepoints[write] = codepoint;
                _clusters[write] = _clusters[read];
            }

            write++;
        }

        if (modified)
        {
            _length = write;
        }

        return modified;
    }

    private bool ApplyMirroringIfNeeded()
    {
        if (Direction != Direction.RightToLeft)
        {
            return false;
        }

        if (_unicodeFunctions.Mirroring is not { } mirror)
        {
            return false;
        }

        var modified = false;

        for (var i = 0; i < _length; i++)
        {
            var mirrored = mirror(_unicodeFunctions, _codepoints[i]);
            if (mirrored == 0 || mirrored == _codepoints[i])
            {
                continue;
            }

            _codepoints[i] = mirrored;
            modified = true;
        }

        return modified;
    }

    private bool ReorderCombiningMarks()
    {
        if (_length <= 1)
        {
            return false;
        }

        var classes = ArrayPool<int>.Shared.Rent(_length);
        try
        {
            var hasNonZero = false;
            for (var i = 0; i < _length; i++)
            {
                var combiningClass = GetCombiningClass(_codepoints[i]);
                classes[i] = combiningClass;
                if (combiningClass != 0)
                {
                    hasNonZero = true;
                }
            }

            if (!hasNonZero)
            {
                return false;
            }

            var modified = false;
            for (var i = 1; i < _length; i++)
            {
                var keyClass = classes[i];
                if (keyClass == 0)
                {
                    continue;
                }

                var keyCodepoint = _codepoints[i];
                var keyCluster = _clusters[i];

                var j = i - 1;
                while (j >= 0 && classes[j] > keyClass)
                {
                    classes[j + 1] = classes[j];
                    _codepoints[j + 1] = _codepoints[j];
                    _clusters[j + 1] = _clusters[j];
                    j--;
                }

                if (j + 1 != i)
                {
                    classes[j + 1] = keyClass;
                    _codepoints[j + 1] = keyCodepoint;
                    _clusters[j + 1] = keyCluster;
                    modified = true;
                }
            }

            return modified;
        }
        finally
        {
            ArrayPool<int>.Shared.Return(classes);
        }
    }

    private void UpdateTextFromCodepoints()
    {
        if (_length == 0)
        {
            _textLength = 0;
            _originalLength = 0;
            return;
        }

        var requiredChars = 0;
        for (var i = 0; i < _length; i++)
        {
            if (!Rune.TryCreate((int)_codepoints[i], out var rune))
            {
                rune = new Rune((int)_replacementCodepoint);
                _codepoints[i] = (uint)rune.Value;
            }

            requiredChars += rune.Utf16SequenceLength;
        }

        EnsureTextCapacity(requiredChars);

        var offset = 0;
        for (var i = 0; i < _length; i++)
        {
            if (!Rune.TryCreate((int)_codepoints[i], out var rune))
            {
                rune = new Rune((int)_replacementCodepoint);
                _codepoints[i] = (uint)rune.Value;
            }

            var start = offset;
            offset += rune.EncodeToUtf16(_text.AsSpan(offset));
            var cluster = (uint)(_clusterOffset + start);
            _clusters[i] = cluster;
            _glyphInfos[i] = new GlyphInfo(_codepoints[i], cluster);
            _glyphPositions[i] = default;
        }

        _textLength = offset;
        _originalLength = _length;
    }

    private int GetCombiningClass(uint codepoint)
    {
        if (_unicodeFunctions.CombiningClass is { } custom)
        {
            return (int)custom(_unicodeFunctions, codepoint);
        }

        if (!Rune.TryCreate((int)codepoint, out var rune))
        {
            return 0;
        }

        return Rune.GetUnicodeCategory(rune) switch
        {
            UnicodeCategory.NonSpacingMark => 230,
            UnicodeCategory.SpacingCombiningMark => 220,
            UnicodeCategory.EnclosingMark => 230,
            _ => 0,
        };
    }

    private bool IsDefaultIgnorable(uint codepoint)
    {
        if (_unicodeFunctions.GeneralCategory is { } custom)
        {
            var category = custom(_unicodeFunctions, codepoint);
            return category == UnicodeGeneralCategory.Format;
        }

        if (!Rune.TryCreate((int)codepoint, out var rune))
        {
            return false;
        }

        return Rune.GetUnicodeCategory(rune) == UnicodeCategory.Format;
    }

    private void EnsureCanAcceptText()
    {
        if (_length != 0 && _contentType != ContentType.Unicode && _contentType != ContentType.Invalid)
        {
            throw new InvalidOperationException("Non empty buffer's ContentType must be of type Unicode.");
        }

        if (_contentType == ContentType.Glyphs && _length != 0)
        {
            throw new InvalidOperationException("ContentType must not be Glyphs.");
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

    private int GetClusterEnd(int start, bool mergeUnsafeToBreak)
    {
        var cluster = _glyphInfos[start].Cluster;
        var end = start + 1;
        while (end < _length && _glyphInfos[end].Cluster == cluster)
        {
            end++;
        }

        if (!mergeUnsafeToBreak)
        {
            return end;
        }

        var extendedEnd = end;
        while (extendedEnd < _length && (_glyphInfos[extendedEnd].Flags & GlyphFlags.UnsafeToBreak) != 0)
        {
            extendedEnd++;
        }

        return extendedEnd;
    }

    private void NormalizeGlyphCluster(int start, int end, bool backward)
    {
        if (end - start <= 1)
        {
            return;
        }

        var totalXAdvance = 0f;
        var totalYAdvance = 0f;
        for (var i = start; i < end; i++)
        {
            totalXAdvance += _glyphPositions[i].XAdvance;
            totalYAdvance += _glyphPositions[i].YAdvance;
        }

        var accumX = 0f;
        var accumY = 0f;
        for (var i = start; i < end; i++)
        {
            var position = _glyphPositions[i];
            _glyphPositions[i] = new GlyphPosition(
                position.XAdvance,
                position.YAdvance,
                position.XOffset + accumX,
                position.YOffset + accumY);

            accumX += position.XAdvance;
            accumY += position.YAdvance;
        }

        if (backward)
        {
            var lastIndex = end - 1;
            var last = _glyphPositions[lastIndex];
            _glyphPositions[lastIndex] = new GlyphPosition(
                totalXAdvance,
                totalYAdvance,
                last.XOffset,
                last.YOffset);

            for (var i = start; i < lastIndex; i++)
            {
                var position = _glyphPositions[i];
                _glyphPositions[i] = new GlyphPosition(
                    position.XAdvance,
                    position.YAdvance,
                    position.XOffset,
                    position.YOffset);
            }

            var count = end - start - 1;
            if (count > 1)
            {
                SortGlyphsByCodepointDescending(start, count);
            }
        }
        else
        {
            var first = _glyphPositions[start];
            _glyphPositions[start] = new GlyphPosition(
                first.XAdvance + totalXAdvance,
                first.YAdvance + totalYAdvance,
                first.XOffset,
                first.YOffset);

            for (var i = start + 1; i < end; i++)
            {
                var position = _glyphPositions[i];
                _glyphPositions[i] = new GlyphPosition(
                    position.XAdvance,
                    position.YAdvance,
                    position.XOffset - totalXAdvance,
                    position.YOffset - totalYAdvance);
            }

            var count = end - start - 1;
            if (count > 1)
            {
                SortGlyphsByCodepointDescending(start + 1, count);
            }
        }
    }

    private void SortGlyphsByCodepointDescending(int start, int count)
    {
        if (count <= 1)
        {
            return;
        }

        var end = start + count;
        for (var i = start + 1; i < end; i++)
        {
            var info = _glyphInfos[i];
            var position = _glyphPositions[i];
            var j = i - 1;
            while (j >= start && _glyphInfos[j].Codepoint < info.Codepoint)
            {
                _glyphInfos[j + 1] = _glyphInfos[j];
                _glyphPositions[j + 1] = _glyphPositions[j];
                j--;
            }

            _glyphInfos[j + 1] = info;
            _glyphPositions[j + 1] = position;
        }
    }
}
