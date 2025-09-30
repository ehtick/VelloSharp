using System;
using System.Collections.Generic;
using System.Globalization;

namespace VelloSharp.Text;

public readonly record struct VelloOpenTypeFeature(string Tag, int Value, uint Start = 0, uint End = uint.MaxValue)
{
    public string Tag { get; } = ValidateTag(Tag);
    public int Value { get; init; } = Value;
    public uint Start { get; init; } = Start;
    public uint End { get; init; } = End;

    private static string ValidateTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag) || tag.Length != 4)
        {
            throw new ArgumentException("OpenType feature tag must be exactly four characters.", nameof(tag));
        }

        return tag;
    }
}

public readonly record struct VelloVariationAxisValue(string Tag, float Value)
{
    public string Tag { get; } = ValidateTag(Tag);
    public float Value { get; init; } = Value;

    private static string ValidateTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag) || tag.Length != 4)
        {
            throw new ArgumentException("Variation axis tag must be exactly four characters.", nameof(tag));
        }

        return tag;
    }
}

public readonly record struct VelloTextShaperOptions(
    float FontSize,
    bool IsRightToLeft,
    float LetterSpacing = 0f,
    IReadOnlyList<VelloOpenTypeFeature>? Features = null,
    IReadOnlyList<VelloVariationAxisValue>? VariationAxes = null,
    bool EnableScriptSegmentation = true,
    CultureInfo? Culture = null)
{
    public static VelloTextShaperOptions CreateDefault(float fontSize, bool isRightToLeft, float letterSpacing = 0f)
        => new(fontSize, isRightToLeft, letterSpacing);
}

internal enum VelloScriptClass
{
    Unknown,
    Latin,
    Cyrillic,
    Greek,
    Arabic,
    Han,
}
