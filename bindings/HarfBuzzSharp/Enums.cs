using System;

namespace HarfBuzzSharp;

public enum Direction
{
    LeftToRight,
    RightToLeft,
}

public enum MemoryMode
{
    ReadOnly,
    ReadOnlyMayMakeWritable,
    Writable,
    WritablePersistent,
}

public enum OpenTypeMetricsTag
{
    UnderlineOffset,
    UnderlineSize,
    StrikeoutOffset,
    StrikeoutSize,
}
