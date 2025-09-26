using System;
using System.Runtime.InteropServices;
using System.Text;

namespace VelloSharp;

/// <summary>Identifies the kind of style property applied to a Parley layout.</summary>
public enum ParleyStylePropertyKind
{
    FontStack = (int)ParleyStylePropertyKindNative.FontStack,
    FontSize = (int)ParleyStylePropertyKindNative.FontSize,
    FontWeight = (int)ParleyStylePropertyKindNative.FontWeight,
    FontStyle = (int)ParleyStylePropertyKindNative.FontStyle,
    FontWidth = (int)ParleyStylePropertyKindNative.FontWidth,
    Brush = (int)ParleyStylePropertyKindNative.Brush,
    LineHeight = (int)ParleyStylePropertyKindNative.LineHeight,
    LetterSpacing = (int)ParleyStylePropertyKindNative.LetterSpacing,
    WordSpacing = (int)ParleyStylePropertyKindNative.WordSpacing,
    Locale = (int)ParleyStylePropertyKindNative.Locale,
    Underline = (int)ParleyStylePropertyKindNative.Underline,
    UnderlineOffset = (int)ParleyStylePropertyKindNative.UnderlineOffset,
    UnderlineSize = (int)ParleyStylePropertyKindNative.UnderlineSize,
    UnderlineBrush = (int)ParleyStylePropertyKindNative.UnderlineBrush,
    Strikethrough = (int)ParleyStylePropertyKindNative.Strikethrough,
    StrikethroughOffset = (int)ParleyStylePropertyKindNative.StrikethroughOffset,
    StrikethroughSize = (int)ParleyStylePropertyKindNative.StrikethroughSize,
    StrikethroughBrush = (int)ParleyStylePropertyKindNative.StrikethroughBrush,
    OverflowWrap = (int)ParleyStylePropertyKindNative.OverflowWrap,
}

/// <summary>Specifies the stylistic posture of a font.</summary>
public enum ParleyFontStyle
{
    Normal = (int)ParleyFontStyleNative.Normal,
    Italic = (int)ParleyFontStyleNative.Italic,
    Oblique = (int)ParleyFontStyleNative.Oblique,
}

/// <summary>Determines how line height values are interpreted.</summary>
public enum ParleyLineHeightKind
{
    MetricsRelative = (int)ParleyLineHeightKindNative.MetricsRelative,
    FontSizeRelative = (int)ParleyLineHeightKindNative.FontSizeRelative,
    Absolute = (int)ParleyLineHeightKindNative.Absolute,
}

/// <summary>Controls how text is allowed to wrap beyond word boundaries.</summary>
public enum ParleyOverflowWrapMode
{
    Normal = (int)ParleyOverflowWrapModeNative.Normal,
    Anywhere = (int)ParleyOverflowWrapModeNative.Anywhere,
    BreakWord = (int)ParleyOverflowWrapModeNative.BreakWord,
}

/// <summary>Specifies the alignment mode used when positioning lines.</summary>
public enum ParleyAlignmentKind
{
    Start = (int)ParleyAlignmentKindNative.Start,
    End = (int)ParleyAlignmentKindNative.End,
    Left = (int)ParleyAlignmentKindNative.Left,
    Center = (int)ParleyAlignmentKindNative.Center,
    Right = (int)ParleyAlignmentKindNative.Right,
    Justify = (int)ParleyAlignmentKindNative.Justify,
}

/// <summary>Indicates why a line break occurred.</summary>
public enum ParleyBreakReason
{
    None = (int)ParleyBreakReasonNative.None,
    Regular = (int)ParleyBreakReasonNative.Regular,
    Explicit = (int)ParleyBreakReasonNative.Explicit,
    Emergency = (int)ParleyBreakReasonNative.Emergency,
}

public partial struct ParleyColor
{
    public ParleyColor(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public static ParleyColor FromBytes(byte r, byte g, byte b, byte a = 255) => new(r, g, b, a);
}

public readonly struct ParleyStyleProperty
{
    internal ParleyStylePropertyKind Kind { get; }
    internal float ValueF32 { get; }
    internal int ValueI32 { get; }
    internal bool ValueBool { get; }
    internal ParleyColor Color { get; }
    internal ParleyFontStyle FontStyleKind { get; }
    internal float FontStyleAngle { get; }
    internal ParleyLineHeightKind LineHeightKind { get; }
    internal string? Text { get; }

    private ParleyStyleProperty(
        ParleyStylePropertyKind kind,
        float valueF32,
        int valueI32,
        bool valueBool,
        ParleyColor color,
        ParleyFontStyle fontStyle,
        float fontStyleAngle,
        ParleyLineHeightKind lineHeightKind,
        string? text)
    {
        Kind = kind;
        ValueF32 = valueF32;
        ValueI32 = valueI32;
        ValueBool = valueBool;
        Color = color;
        FontStyleKind = fontStyle;
        FontStyleAngle = fontStyleAngle;
        LineHeightKind = lineHeightKind;
        Text = text;
    }

    public static ParleyStyleProperty FontStack(string stack)
    {
        ArgumentException.ThrowIfNullOrEmpty(stack);
        return new ParleyStyleProperty(ParleyStylePropertyKind.FontStack, 0f, 0, false, default, ParleyFontStyle.Normal, 0f, ParleyLineHeightKind.MetricsRelative, stack);
    }

    public static ParleyStyleProperty FontSize(float size) => new(ParleyStylePropertyKind.FontSize, size, 0, false, default, ParleyFontStyle.Normal, 0f, ParleyLineHeightKind.MetricsRelative, null);

    public static ParleyStyleProperty FontWeight(float weight) => new(ParleyStylePropertyKind.FontWeight, weight, 0, false, default, ParleyFontStyle.Normal, 0f, ParleyLineHeightKind.MetricsRelative, null);

    public static ParleyStyleProperty FontStyle(ParleyFontStyle style, float angle = 0f) => new(ParleyStylePropertyKind.FontStyle, 0f, 0, false, default, style, angle, ParleyLineHeightKind.MetricsRelative, null);

    public static ParleyStyleProperty FontWidth(float ratio) => new(ParleyStylePropertyKind.FontWidth, ratio, 0, false, default, ParleyFontStyle.Normal, 0f, ParleyLineHeightKind.MetricsRelative, null);

    public static ParleyStyleProperty Brush(ParleyColor color) => new(ParleyStylePropertyKind.Brush, 0f, 0, false, color, ParleyFontStyle.Normal, 0f, ParleyLineHeightKind.MetricsRelative, null);

    public static ParleyStyleProperty LineHeight(ParleyLineHeightKind kind, float value) => new(ParleyStylePropertyKind.LineHeight, value, 0, false, default, ParleyFontStyle.Normal, 0f, kind, null);

    public static ParleyStyleProperty LetterSpacing(float value) => new(ParleyStylePropertyKind.LetterSpacing, value, 0, false, default, ParleyFontStyle.Normal, 0f, ParleyLineHeightKind.MetricsRelative, null);

    public static ParleyStyleProperty WordSpacing(float value) => new(ParleyStylePropertyKind.WordSpacing, value, 0, false, default, ParleyFontStyle.Normal, 0f, ParleyLineHeightKind.MetricsRelative, null);

    public static ParleyStyleProperty Underline(bool enabled) => new(ParleyStylePropertyKind.Underline, 0f, 0, enabled, default, ParleyFontStyle.Normal, 0f, ParleyLineHeightKind.MetricsRelative, null);

    public static ParleyStyleProperty UnderlineOffset(float? value) => new(ParleyStylePropertyKind.UnderlineOffset, value ?? 0f, 0, value.HasValue, default, ParleyFontStyle.Normal, 0f, ParleyLineHeightKind.MetricsRelative, null);

    public static ParleyStyleProperty UnderlineSize(float? value) => new(ParleyStylePropertyKind.UnderlineSize, value ?? 0f, 0, value.HasValue, default, ParleyFontStyle.Normal, 0f, ParleyLineHeightKind.MetricsRelative, null);

    public static ParleyStyleProperty UnderlineBrush(ParleyColor? color) => new(ParleyStylePropertyKind.UnderlineBrush, 0f, 0, color.HasValue, color ?? default, ParleyFontStyle.Normal, 0f, ParleyLineHeightKind.MetricsRelative, null);

    public static ParleyStyleProperty Strikethrough(bool enabled) => new(ParleyStylePropertyKind.Strikethrough, 0f, 0, enabled, default, ParleyFontStyle.Normal, 0f, ParleyLineHeightKind.MetricsRelative, null);

    public static ParleyStyleProperty StrikethroughOffset(float? value) => new(ParleyStylePropertyKind.StrikethroughOffset, value ?? 0f, 0, value.HasValue, default, ParleyFontStyle.Normal, 0f, ParleyLineHeightKind.MetricsRelative, null);

    public static ParleyStyleProperty StrikethroughSize(float? value) => new(ParleyStylePropertyKind.StrikethroughSize, value ?? 0f, 0, value.HasValue, default, ParleyFontStyle.Normal, 0f, ParleyLineHeightKind.MetricsRelative, null);

    public static ParleyStyleProperty StrikethroughBrush(ParleyColor? color) => new(ParleyStylePropertyKind.StrikethroughBrush, 0f, 0, color.HasValue, color ?? default, ParleyFontStyle.Normal, 0f, ParleyLineHeightKind.MetricsRelative, null);

    public static ParleyStyleProperty OverflowWrap(ParleyOverflowWrapMode mode) => new(ParleyStylePropertyKind.OverflowWrap, 0f, (int)mode, false, default, ParleyFontStyle.Normal, 0f, ParleyLineHeightKind.MetricsRelative, null);

}

public readonly struct ParleyStyleSpan
{
    public ParleyStyleSpan(int start, int end, ParleyStyleProperty property)
    {
        if (start < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }
        if (end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end));
        }
        Start = start;
        End = end;
        Property = property;
    }

    public int Start { get; }
    public int End { get; }
    public ParleyStyleProperty Property { get; }
}

public readonly struct ParleyLineInfo
{
    internal ParleyLineInfo(ParleyLineInfoNative native)
    {
        TextStart = (int)native.TextStart;
        TextEnd = (int)native.TextEnd;
        BreakReason = (ParleyBreakReason)native.BreakReason;
        Advance = native.Advance;
        TrailingWhitespace = native.TrailingWhitespace;
        LineHeight = native.LineHeight;
        Baseline = native.Baseline;
        Offset = native.Offset;
        Ascent = native.Ascent;
        Descent = native.Descent;
        Leading = native.Leading;
        MinCoordinate = native.MinCoord;
        MaxCoordinate = native.MaxCoord;
    }

    public int TextStart { get; }
    public int TextEnd { get; }
    public ParleyBreakReason BreakReason { get; }
    public float Advance { get; }
    public float TrailingWhitespace { get; }
    public float LineHeight { get; }
    public float Baseline { get; }
    public float Offset { get; }
    public float Ascent { get; }
    public float Descent { get; }
    public float Leading { get; }
    public float MinCoordinate { get; }
    public float MaxCoordinate { get; }
}

public readonly struct ParleyInlineBoxInfo
{
    internal ParleyInlineBoxInfo(ParleyInlineBoxInfoNative native)
    {
        Id = native.Id;
        X = native.X;
        Y = native.Y;
        Width = native.Width;
        Height = native.Height;
    }

    public ulong Id { get; }
    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }
}

public readonly struct ParleyGlyphInfo
{
    internal ParleyGlyphInfo(ParleyGlyph native)
    {
        Id = native.Id;
        StyleIndex = native.StyleIndex;
        X = native.X;
        Y = native.Y;
        Advance = native.Advance;
    }

    public uint Id { get; }
    public ushort StyleIndex { get; }
    public float X { get; }
    public float Y { get; }
    public float Advance { get; }
}

public readonly struct ParleyStyleInfo
{
    internal ParleyStyleInfo(ParleyStyleInfoNative native)
    {
        Brush = native.Brush;
        Underline = native.Underline != 0;
        UnderlineOffset = native.UnderlineOffset;
        UnderlineSize = native.UnderlineSize;
        UnderlineBrush = native.UnderlineBrush;
        Strikethrough = native.Strikethrough != 0;
        StrikethroughOffset = native.StrikethroughOffset;
        StrikethroughSize = native.StrikethroughSize;
        StrikethroughBrush = native.StrikethroughBrush;
    }

    public ParleyColor Brush { get; }
    public bool Underline { get; }
    public float UnderlineOffset { get; }
    public float UnderlineSize { get; }
    public ParleyColor UnderlineBrush { get; }
    public bool Strikethrough { get; }
    public float StrikethroughOffset { get; }
    public float StrikethroughSize { get; }
    public ParleyColor StrikethroughBrush { get; }
}

public readonly struct ParleyGlyphRunInfo
{
    private readonly IntPtr _fontData;
    private readonly nuint _fontDataLength;

    internal ParleyGlyphRunInfo(ParleyGlyphRunInfoNative native)
    {
        GlyphCount = (int)native.GlyphCount;
        StyleIndex = native.StyleIndex;
        FontBlobId = native.FontBlobId;
        FontIndex = native.FontIndex;
        FontSize = native.FontSize;
        Ascent = native.Ascent;
        Descent = native.Descent;
        Leading = native.Leading;
        Baseline = native.Baseline;
        Offset = native.Offset;
        Advance = native.Advance;
        IsRightToLeft = native.IsRtl != 0;
        _fontData = native.FontData;
        _fontDataLength = native.FontDataLength;
    }

    public int GlyphCount { get; }
    public ushort StyleIndex { get; }
    public ulong FontBlobId { get; }
    public uint FontIndex { get; }
    public float FontSize { get; }
    public float Ascent { get; }
    public float Descent { get; }
    public float Leading { get; }
    public float Baseline { get; }
    public float Offset { get; }
    public float Advance { get; }
    public bool IsRightToLeft { get; }

    public byte[] GetFontData()
    {
        if (_fontData == IntPtr.Zero || _fontDataLength == 0)
        {
            return Array.Empty<byte>();
        }

        var length = checked((int)_fontDataLength);
        var buffer = new byte[length];
        Marshal.Copy(_fontData, buffer, 0, length);
        return buffer;
    }

    public Font CreateFont()
    {
        var data = GetFontData();
        return Font.Load(data, FontIndex);
    }
}

internal sealed class NativeStyleProperty : IDisposable
{
    private readonly byte[]? _utf8;
    private readonly GCHandle _utf8Handle;

    internal ParleyStylePropertyNative Value;

    internal NativeStyleProperty(ParleyStyleProperty property)
    {
        Value = new ParleyStylePropertyNative
        {
            Kind = (ParleyStylePropertyKindNative)property.Kind,
            ValueF32 = property.ValueF32,
            ValueI32 = property.ValueI32,
            ValueBool = property.ValueBool ? (byte)1 : (byte)0,
            Color = property.Color,
            FontStyle = (ParleyFontStyleNative)property.FontStyleKind,
            FontStyleAngle = property.FontStyleAngle,
            LineHeightKind = (ParleyLineHeightKindNative)property.LineHeightKind,
            StringPtr = IntPtr.Zero,
            StringLength = 0,
        };

        if (!string.IsNullOrEmpty(property.Text))
        {
            _utf8 = Encoding.UTF8.GetBytes(property.Text);
            _utf8Handle = GCHandle.Alloc(_utf8, GCHandleType.Pinned);
            Value.StringPtr = _utf8Handle.AddrOfPinnedObject();
            Value.StringLength = (nuint)_utf8.Length;
        }
    }

    public void Dispose()
    {
        if (_utf8Handle.IsAllocated)
        {
            _utf8Handle.Free();
        }
    }
}

internal sealed class NativeStylePropertyList : IDisposable
{
    private readonly NativeStyleProperty[] _helpers;
    private readonly GCHandle _arrayHandle;

    internal ParleyStylePropertyNative[] Native { get; }

    internal unsafe ParleyStylePropertyNative* Pointer => _arrayHandle.IsAllocated ? (ParleyStylePropertyNative*)_arrayHandle.AddrOfPinnedObject() : null;

    internal NativeStylePropertyList(ReadOnlySpan<ParleyStyleProperty> properties)
    {
        if (properties.Length == 0)
        {
            _helpers = Array.Empty<NativeStyleProperty>();
            Native = Array.Empty<ParleyStylePropertyNative>();
            return;
        }

        _helpers = new NativeStyleProperty[properties.Length];
        Native = new ParleyStylePropertyNative[properties.Length];
        for (var i = 0; i < properties.Length; i++)
        {
            var helper = new NativeStyleProperty(properties[i]);
            _helpers[i] = helper;
            Native[i] = helper.Value;
        }

        _arrayHandle = GCHandle.Alloc(Native, GCHandleType.Pinned);
    }

    public void Dispose()
    {
        foreach (var helper in _helpers)
        {
            helper?.Dispose();
        }

        if (_arrayHandle.IsAllocated)
        {
            _arrayHandle.Free();
        }
    }
}

internal sealed class NativeStyleSpanList : IDisposable
{
    private readonly NativeStyleProperty[] _helpers;
    private readonly GCHandle _arrayHandle;

    internal ParleyStyleSpanNative[] Native { get; }

    internal unsafe ParleyStyleSpanNative* Pointer => _arrayHandle.IsAllocated ? (ParleyStyleSpanNative*)_arrayHandle.AddrOfPinnedObject() : null;

    internal NativeStyleSpanList(ReadOnlySpan<ParleyStyleSpan> spans)
    {
        if (spans.Length == 0)
        {
            _helpers = Array.Empty<NativeStyleProperty>();
            Native = Array.Empty<ParleyStyleSpanNative>();
            return;
        }

        _helpers = new NativeStyleProperty[spans.Length];
        Native = new ParleyStyleSpanNative[spans.Length];
        for (var i = 0; i < spans.Length; i++)
        {
            var helper = new NativeStyleProperty(spans[i].Property);
            _helpers[i] = helper;
            Native[i] = new ParleyStyleSpanNative
            {
                RangeStart = (nuint)spans[i].Start,
                RangeEnd = (nuint)spans[i].End,
                Property = helper.Value,
            };
        }

        _arrayHandle = GCHandle.Alloc(Native, GCHandleType.Pinned);
    }

    public void Dispose()
    {
        foreach (var helper in _helpers)
        {
            helper?.Dispose();
        }

        if (_arrayHandle.IsAllocated)
        {
            _arrayHandle.Free();
        }
    }
}
