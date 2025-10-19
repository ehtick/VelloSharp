using System;

namespace SkiaSharp;

internal static class ShimNotImplemented
{
    internal static void Throw(string memberName, string? details = null)
    {
        var suffix = string.IsNullOrWhiteSpace(details) ? string.Empty : $" ({details})";
        throw new NotImplementedException($"TODO: {memberName}{suffix}");
    }
}
