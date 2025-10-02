using System;
using System.Runtime.InteropServices;

namespace VelloSharp;

internal static class SparseNativeHelpers
{
    public static void ThrowOnError(VelloSparseStatus status, string message)
    {
        if (status == VelloSparseStatus.Success)
        {
            return;
        }

        var detail = GetLastErrorMessage();
        if (!string.IsNullOrWhiteSpace(detail))
        {
            throw new InvalidOperationException($"{message}: {detail} (status: {status})");
        }

        throw new InvalidOperationException($"{message} (status: {status})");
    }

    public static string? GetLastErrorMessage()
    {
        var ptr = SparseNativeMethods.vello_sparse_last_error_message();
        return ptr != IntPtr.Zero ? Marshal.PtrToStringUTF8(ptr) : null;
    }
}
