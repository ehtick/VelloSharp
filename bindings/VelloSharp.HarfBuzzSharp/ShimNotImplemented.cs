using System;

namespace HarfBuzzSharp;

internal static class ShimNotImplemented
{
    internal static void Throw(string memberName, string? details = null)
    {
#if DEBUG
        var suffix = string.IsNullOrWhiteSpace(details) ? string.Empty : $" ({details})";
        throw new NotImplementedException($"TODO: {memberName}{suffix}");
#else
        _ = memberName;
        _ = details;
#endif
    }
}
