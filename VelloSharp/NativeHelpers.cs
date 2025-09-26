using System;
using System.Runtime.InteropServices;

namespace VelloSharp;

internal static class NativeHelpers
{
    public static string? GetLastErrorMessage()
    {
        var ptr = NativeMethods.vello_last_error_message();
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
    }

    internal static nint AllocUtf8String(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var buffer = Marshal.AllocHGlobal(bytes.Length + 1);
        if (bytes.Length > 0)
        {
            Marshal.Copy(bytes, 0, buffer, bytes.Length);
        }

        Marshal.WriteByte(buffer, bytes.Length, 0);
        return buffer;
    }

    internal static void ThrowOnError(VelloStatus status, string message)
    {
        if (status == VelloStatus.Success)
        {
            return;
        }

        Throw(message, status, NativeMethods.vello_last_error_message);
    }

    internal static void ThrowOnError(KurboStatus status, string message)
    {
        if (status == KurboStatus.Success)
        {
            return;
        }

        Throw(message, status, KurboNativeMethods.kurbo_last_error_message);
    }

    internal static void ThrowOnError(PenikoStatus status, string message)
    {
        if (status == PenikoStatus.Success)
        {
            return;
        }

        Throw(message, status, PenikoNativeMethods.peniko_last_error_message);
    }

    internal static void ThrowOnError(WinitStatus status, string message)
    {
        if (status == WinitStatus.Success)
        {
            return;
        }

        Throw(message, status, WinitNativeMethods.winit_last_error_message);
    }

    private static void Throw<TStatus>(string message, TStatus status, Func<nint> getter)
        where TStatus : struct
    {
        var native = GetErrorMessage(getter);
        if (!string.IsNullOrWhiteSpace(native))
        {
            throw new InvalidOperationException($"{message}: {native} (status: {status})");
        }

        throw new InvalidOperationException($"{message} (status: {status})");
    }

    private static string? GetErrorMessage(Func<nint> getter)
    {
        var ptr = getter();
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
    }
}
