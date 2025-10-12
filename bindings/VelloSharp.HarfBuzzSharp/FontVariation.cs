using System;

namespace HarfBuzzSharp;

public readonly struct FontVariation : IEquatable<FontVariation>
{
    public FontVariation(Tag tag, float value)
    {
        Tag = tag;
        Value = value;
    }

    public Tag Tag { get; }

    public float Value { get; }

    public bool Equals(FontVariation other)
        => Tag.Equals(other.Tag) && Value.Equals(other.Value);

    public override bool Equals(object? obj)
        => obj is FontVariation variation && Equals(variation);

    public override int GetHashCode() => HashCode.Combine(Tag, Value);

    public override string ToString() => $"{Tag}:{Value}";
}

