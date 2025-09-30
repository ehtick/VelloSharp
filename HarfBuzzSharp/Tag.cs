using System;

namespace HarfBuzzSharp;

public readonly struct Tag : IEquatable<Tag>
{
    public Tag(uint value)
    {
        Value = value;
    }

    public uint Value { get; }

    public static Tag Parse(string tag)
    {
        if (tag is null)
        {
            throw new ArgumentNullException(nameof(tag));
        }

        if (tag.Length != 4)
        {
            throw new ArgumentException("Tag must be exactly four characters.", nameof(tag));
        }

        var value = ((uint)tag[0] << 24) | ((uint)tag[1] << 16) | ((uint)tag[2] << 8) | tag[3];
        return new Tag(value);
    }

    public bool Equals(Tag other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is Tag tag && Equals(tag);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString()
        => new string(new[] { (char)((Value >> 24) & 0xFF), (char)((Value >> 16) & 0xFF), (char)((Value >> 8) & 0xFF), (char)(Value & 0xFF) });

    public static implicit operator uint(Tag tag) => tag.Value;
}
