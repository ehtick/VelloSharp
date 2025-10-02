namespace HarfBuzzSharp;

public readonly struct Feature
{
    public Feature(Tag tag, uint value, uint start = 0, uint end = uint.MaxValue)
    {
        Tag = tag;
        Value = value;
        Start = start;
        End = end;
    }

    public Tag Tag { get; }
    public uint Value { get; }
    public uint Start { get; }
    public uint End { get; }
}
