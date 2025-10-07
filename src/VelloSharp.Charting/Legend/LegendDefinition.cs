using System;
using System.Collections.Generic;

namespace VelloSharp.Charting.Legend;

/// <summary>
/// Describes legend configuration and entries.
/// </summary>
public sealed class LegendDefinition
{
    public LegendDefinition(
        string id,
        LegendOrientation orientation,
        LegendPosition position,
        IReadOnlyList<LegendItem> items)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Legend id is required.", nameof(id));
        }

        Id = id;
        Orientation = orientation;
        Position = position;
        Items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public string Id { get; }

    public LegendOrientation Orientation { get; }

    public LegendPosition Position { get; }

    public IReadOnlyList<LegendItem> Items { get; }
}
