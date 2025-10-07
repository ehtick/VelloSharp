using System;

namespace VelloSharp.Charting.Primitives;

/// <summary>
/// Represents an inclusive range.
/// </summary>
/// <typeparam name="T">Value type.</typeparam>
public readonly record struct Range<T>(T Start, T End)
{
    public bool IsAscending => Comparer<T>.Default.Compare(End, Start) >= 0;

    public Range<T> Normalize() => IsAscending ? this : new Range<T>(End, Start);

    public bool Contains(T value)
    {
        var comparer = Comparer<T>.Default;
        var (min, max) = Normalize();
        return comparer.Compare(value, min) >= 0 && comparer.Compare(value, max) <= 0;
    }

    public void Deconstruct(out T min, out T max)
    {
        var normalized = Normalize();
        min = normalized.Start;
        max = normalized.End;
    }
}
