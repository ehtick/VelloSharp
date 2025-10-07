using System;
using System.Collections.Generic;
using VelloSharp.Charting.Primitives;

namespace VelloSharp.Charting.Scales;

/// <summary>
/// Projects discrete categorical values onto evenly spaced unit coordinates.
/// </summary>
public sealed class OrdinalScale<T> : IScale<T> where T : notnull
{
    private readonly IReadOnlyList<T> _values;
    private readonly Dictionary<T, int> _lookup;

    public OrdinalScale(IEnumerable<T> values, bool clampToDomain = true, IEqualityComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(values);

        _values = values switch
        {
            IReadOnlyList<T> list => list.Count > 0
                ? list
                : throw new ArgumentException("Ordinal scale requires at least one category.", nameof(values)),
            _ => new List<T>(values)
        };

        if (_values.Count == 0)
        {
            throw new ArgumentException("Ordinal scale requires at least one category.", nameof(values));
        }

        ClampToDomain = clampToDomain;
        _lookup = new Dictionary<T, int>(comparer ?? EqualityComparer<T>.Default);

        for (var i = 0; i < _values.Count; i++)
        {
            _lookup[_values[i]] = i;
        }

        Domain = new Range<T>(_values[0], _values[^1]);
    }

    public ScaleKind Kind => ScaleKind.Ordinal;

    public bool ClampToDomain { get; }

    public Type DomainType => typeof(T);

    public Range<T> Domain { get; }

    /// <summary>
    /// Gets the categories in their defined order.
    /// </summary>
    public IReadOnlyList<T> Categories => _values;

    public double Project(T value)
    {
        if (!TryProject(value, out var unit))
        {
            throw new KeyNotFoundException($"Value '{value}' is not part of the ordinal domain.");
        }

        return unit;
    }

    public bool TryProject(T value, out double unit)
    {
        if (!_lookup.TryGetValue(value, out var index))
        {
            unit = double.NaN;
            return false;
        }

        unit = _values.Count == 1
            ? 0d
            : index / (double)(_values.Count - 1);
        return true;
    }

    public T Unproject(double unit)
    {
        if (!double.IsFinite(unit))
        {
            throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unit value must be finite.");
        }

        if (ClampToDomain)
        {
            unit = Math.Clamp(unit, 0d, 1d);
        }

        var index = _values.Count == 1
            ? 0
            : (int)Math.Round(unit * (_values.Count - 1), MidpointRounding.AwayFromZero);

        index = Math.Clamp(index, 0, _values.Count - 1);
        return _values[index];
    }
}
