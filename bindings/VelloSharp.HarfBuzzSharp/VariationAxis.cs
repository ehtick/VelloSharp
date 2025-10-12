using System;

namespace HarfBuzzSharp;

public readonly struct VariationAxis : IEquatable<VariationAxis>
{
    public VariationAxis(Tag tag, float minValue, float defaultValue, float maxValue)
    {
        Tag = tag;
        MinValue = minValue;
        DefaultValue = defaultValue;
        MaxValue = maxValue;
    }

    public Tag Tag { get; }

    public float MinValue { get; }

    public float DefaultValue { get; }

    public float MaxValue { get; }

    public bool Equals(VariationAxis other)
        => Tag.Equals(other.Tag)
        && MinValue.Equals(other.MinValue)
        && DefaultValue.Equals(other.DefaultValue)
        && MaxValue.Equals(other.MaxValue);

    public override bool Equals(object? obj)
        => obj is VariationAxis axis && Equals(axis);

    public override int GetHashCode()
        => HashCode.Combine(Tag, MinValue, DefaultValue, MaxValue);

    public override string ToString()
        => $"{Tag}:{MinValue}-{DefaultValue}-{MaxValue}";
}

