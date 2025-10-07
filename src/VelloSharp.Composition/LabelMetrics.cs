namespace VelloSharp.Composition;

public readonly record struct LabelMetrics(float Width, float Height, float Ascent)
{
    public bool IsEmpty => Width <= 0f || Height <= 0f;
}
