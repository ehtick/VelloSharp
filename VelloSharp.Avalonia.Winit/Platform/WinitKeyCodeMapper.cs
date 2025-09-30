using System;
using Avalonia.Input;

namespace Avalonia.Winit;

internal static class WinitKeyCodeMapper
{
    public static PhysicalKey MapPhysicalKey(uint keyCode, string? keyCodeName)
    {
        if (!string.IsNullOrEmpty(keyCodeName))
        {
            var normalized = NormalizeKeyName(keyCodeName!);
            if (Enum.TryParse(normalized, ignoreCase: false, out PhysicalKey physical))
            {
                return physical;
            }

            if (FallbackMappings.TryMap(normalized, out physical))
            {
                return physical;
            }
        }

        // Fall back to casting when the incoming value already matches Avalonia's enum.
        if (Enum.IsDefined(typeof(PhysicalKey), keyCode))
        {
            return (PhysicalKey)keyCode;
        }

        return PhysicalKey.None;
    }

    private static string NormalizeKeyName(string name)
    {
        if (name.StartsWith("Numpad", StringComparison.Ordinal))
        {
            return "NumPad" + name.AsSpan(6).ToString();
        }

        return name;
    }

    private static class FallbackMappings
    {
        public static bool TryMap(string name, out PhysicalKey physical)
        {
            switch (name)
            {
                case "Backquote":
                    physical = PhysicalKey.Backquote;
                    return true;
                case "Quote":
                    physical = PhysicalKey.Quote;
                    return true;
                case "Minus":
                    physical = PhysicalKey.Minus;
                    return true;
                case "Equal":
                    physical = PhysicalKey.Equal;
                    return true;
                default:
                    physical = PhysicalKey.None;
                    return false;
            }
        }
    }
}
