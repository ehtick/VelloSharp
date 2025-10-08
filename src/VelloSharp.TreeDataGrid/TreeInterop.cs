using System;

namespace VelloSharp.TreeDataGrid;

internal static class TreeInterop
{
    public static Exception CreateException(string message)
    {
        var detail = NativeMethods.GetLastError();
        if (string.IsNullOrEmpty(detail))
        {
            return new InvalidOperationException(message);
        }

        return new InvalidOperationException($"{message}: {detail}");
    }

    public static void ThrowIfFalse(bool condition, string message)
    {
        if (!condition)
        {
            throw CreateException(message);
        }
    }
}
