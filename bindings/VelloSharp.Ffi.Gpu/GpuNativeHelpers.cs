using System;
using System.Runtime.InteropServices;

namespace VelloSharp.Ffi.Gpu;

internal static class GpuNativeHelpers
{
    public static void ThrowOnError(VelloStatus status, string message)
    {
        if (status == VelloStatus.Success)
        {
            return;
        }

        var detail = GetErrorMessage(NativeMethods.vello_last_error_message);
        if (!string.IsNullOrWhiteSpace(detail))
        {
            throw new InvalidOperationException($"{message}: {detail} (status: {status})");
        }

        throw new InvalidOperationException($"{message} (status: {status})");
    }

    public static string? GetLastErrorMessage()
    {
        var ptr = NativeMethods.vello_last_error_message();
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
    }

    private static string? GetErrorMessage(Func<IntPtr> getter)
    {
        var ptr = getter();
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
    }
}
