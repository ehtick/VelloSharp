using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;
using VelloSharp;

namespace Avalonia.Winit;

internal sealed class WinitClipboard : IClipboard
{
    private readonly object _lock = new();
    private readonly bool _nativeAvailable;
    private string? _fallbackText;

    public WinitClipboard()
    {
        _nativeAvailable = WinitNativeMethods.winit_clipboard_is_available();
    }

    public Task ClearAsync() => SetTextAsync(null);

    public Task<string?> GetTextAsync()
    {
        return Task.FromResult(GetClipboardText());
    }

    public Task SetTextAsync(string? text)
    {
        if (_nativeAvailable)
        {
            nint ptr = IntPtr.Zero;

            try
            {
                if (text is not null)
                {
                    ptr = NativeHelpers.AllocUtf8String(text);
                }

                var status = WinitNativeMethods.winit_clipboard_set_text(ptr);
                NativeHelpers.ThrowOnError(status, "winit_clipboard_set_text");
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    unsafe
                    {
                        NativeMemory.Free((void*)ptr);
                    }
                }
            }

            lock (_lock)
            {
                _fallbackText = text;
            }

            return Task.CompletedTask;
        }

        lock (_lock)
        {
            _fallbackText = text;
        }

        return Task.CompletedTask;
    }

    public Task SetDataObjectAsync(IDataObject data)
    {
        return SetTextAsync(data.Contains(DataFormats.Text) ? data.Get(DataFormats.Text) as string : null);
    }

    public Task FlushAsync() => Task.CompletedTask;

    public Task<string[]> GetFormatsAsync()
    {
        var text = GetClipboardText();
        return Task.FromResult(string.IsNullOrEmpty(text)
            ? Array.Empty<string>()
            : new[] { DataFormats.Text });
    }

    public Task<object?> GetDataAsync(string format)
    {
        if (format == DataFormats.Text)
        {
            return Task.FromResult<object?>(GetClipboardText());
        }

        return Task.FromResult<object?>(null);
    }

    public Task<IDataObject?> TryGetInProcessDataObjectAsync() => Task.FromResult<IDataObject?>(null);

    private string? GetClipboardText()
    {
        if (_nativeAvailable)
        {
            var text = GetNativeText();

            lock (_lock)
            {
                _fallbackText = text;
            }

            return text;
        }

        lock (_lock)
        {
            return _fallbackText;
        }
    }

    private string? GetNativeText()
    {
        var status = WinitNativeMethods.winit_clipboard_get_text(out var ptr);
        NativeHelpers.ThrowOnError(status, "winit_clipboard_get_text");

        if (ptr == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUTF8(ptr);
        }
        finally
        {
            WinitNativeMethods.winit_clipboard_free_text(ptr);
        }
    }
}
