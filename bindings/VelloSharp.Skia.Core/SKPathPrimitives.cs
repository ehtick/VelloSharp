namespace SkiaSharp;

public enum SKPathFillType
{
    Winding = 0,
    EvenOdd = 1,
    InverseWinding = 2,
    InverseEvenOdd = 3,
}

public enum SKPathDirection
{
    Clockwise = 0,
    CounterClockwise = 1,
}

public enum SKPathArcSize
{
    Small = 0,
    Large = 1,
}

public enum SKPathVerb
{
    Move,
    Line,
    Quad,
    Conic,
    Cubic,
    Close,
    Done,
}

public enum SKPathOp
{
    Difference,
    Intersect,
    Union,
    Xor,
    ReverseDifference,
}
