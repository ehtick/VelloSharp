using System;
using VelloSharp.Charting.Primitives;

namespace VelloSharp.Charting.Scales;

/// <summary>
/// Base interface for scale abstractions.
/// </summary>
public interface IScale
{
    /// <summary>
    /// Gets the scale family.
    /// </summary>
    ScaleKind Kind { get; }

    /// <summary>
    /// Gets a value indicating whether inputs are clamped to the domain when projected.
    /// </summary>
    bool ClampToDomain { get; }

    /// <summary>
    /// Gets the underlying domain value type.
    /// </summary>
    Type DomainType { get; }
}

/// <summary>
/// Represents a scale that projects domain values into normalized unit space and can unproject back.
/// </summary>
/// <typeparam name="T">Domain data type.</typeparam>
public interface IScale<T> : IScale
{
    /// <summary>
    /// Gets the scale domain range.
    /// </summary>
    Range<T> Domain { get; }

    /// <summary>
    /// Projects the provided value into normalized unit space (0..1).
    /// </summary>
    double Project(T value);

    /// <summary>
    /// Attempts to project the provided value into unit space without throwing.
    /// </summary>
    bool TryProject(T value, out double unit);

    /// <summary>
    /// Converts a normalized unit value back into domain space.
    /// </summary>
    T Unproject(double unit);
}
