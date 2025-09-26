using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace VelloSharp;

internal static class NativeHelpers
{
    public static string? GetLastErrorMessage()
    {
        var ptr = NativeMethods.vello_last_error_message();
        return ptr == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(ptr);
    }

    internal static unsafe nint AllocUtf8String(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var chars = text.AsSpan();
        var byteCount = Encoding.UTF8.GetByteCount(chars);
        var length = byteCount + 1;
        var buffer = NativeMemory.Alloc((nuint)length);
        try
        {
            var destination = new Span<byte>(buffer, length);
            Encoding.UTF8.GetBytes(chars, destination[..byteCount]);
            destination[byteCount] = 0;
            return (nint)buffer;
        }
        catch
        {
            NativeMemory.Free(buffer);
            throw;
        }
    }

    internal static Span<byte> EncodeUtf8NullTerminated(string? text, Span<byte> scratch, ref byte[]? rented)
    {
        rented = null;
        if (string.IsNullOrEmpty(text))
        {
            return Span<byte>.Empty;
        }

        var chars = text.AsSpan();
        var byteCount = Encoding.UTF8.GetByteCount(chars);
        var required = byteCount + 1;
        Span<byte> destination;

        if (required <= scratch.Length)
        {
            destination = scratch[..required];
        }
        else
        {
            rented = ArrayPool<byte>.Shared.Rent(required);
            destination = rented.AsSpan(0, required);
        }

        Encoding.UTF8.GetBytes(chars, destination[..byteCount]);
        destination[byteCount] = 0;
        return destination;
    }

    internal static Span<byte> EncodeUtf8(string text, Span<byte> scratch, ref byte[]? rented)
    {
        ArgumentNullException.ThrowIfNull(text);

        var chars = text.AsSpan();
        var byteCount = Encoding.UTF8.GetByteCount(chars);
        if (byteCount <= scratch.Length)
        {
            var destination = scratch[..byteCount];
            Encoding.UTF8.GetBytes(chars, destination);
            rented = null;
            return destination;
        }

        rented = ArrayPool<byte>.Shared.Rent(byteCount);
        var span = rented.AsSpan(0, byteCount);
        Encoding.UTF8.GetBytes(chars, span);
        return span;
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
