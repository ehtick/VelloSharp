using System;

namespace HarfBuzzSharp;

public readonly struct Tag : IEquatable<Tag>
{
    public static readonly Tag None = new(0, 0, 0, 0);
    public static readonly Tag Max = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
    public static readonly Tag MaxSigned = new((byte)sbyte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

    private readonly uint _value;

    private Tag(uint value)
    {
        _value = value;
    }

    private Tag(byte c1, byte c2, byte c3, byte c4)
    {
        _value = (uint)((c1 << 24) | (c2 << 16) | (c3 << 8) | c4);
    }

    public Tag(char c1, char c2, char c3, char c4)
    {
        _value = (uint)(((byte)c1 << 24) | ((byte)c2 << 16) | ((byte)c3 << 8) | (byte)c4);
    }

    public static Tag Parse(string tag)
    {
        if (string.IsNullOrEmpty(tag))
        {
            return None;
        }

        var buffer = new char[4];
        var len = Math.Min(4, tag.Length);
        var index = 0;
        for (; index < len; index++)
        {
            buffer[index] = tag[index];
        }

        for (; index < 4; index++)
        {
            buffer[index] = ' ';
        }

        return new Tag(buffer[0], buffer[1], buffer[2], buffer[3]);
    }

    public override string ToString()
    {
        if (_value == None)
        {
            return nameof(None);
        }

        if (_value == Max)
        {
            return nameof(Max);
        }

        if (_value == MaxSigned)
        {
            return nameof(MaxSigned);
        }

        return string.Concat(
            (char)(byte)(_value >> 24),
            (char)(byte)(_value >> 16),
            (char)(byte)(_value >> 8),
            (char)(byte)_value);
    }

    public static implicit operator uint(Tag tag) => tag._value;

    public static implicit operator Tag(uint tag) => new(tag);

    public bool Equals(Tag other) => _value == other._value;

    public override bool Equals(object? obj) => obj is Tag tag && Equals(tag);

    public override int GetHashCode() => unchecked((int)_value);
}
