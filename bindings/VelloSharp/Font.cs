using System;

namespace VelloSharp;

public sealed class Font : IDisposable
{
    private IntPtr _handle;

    private Font(IntPtr handle)
    {
        _handle = handle;
    }

    public IntPtr Handle
    {
        get
        {
            if (_handle == IntPtr.Zero)
            {
                throw new ObjectDisposedException(nameof(Font));
            }
            return _handle;
        }
    }

    public static Font Load(ReadOnlySpan<byte> fontData, uint index = 0)
    {
        unsafe
        {
            fixed (byte* ptr = fontData)
            {
                var native = NativeMethods.vello_font_create((IntPtr)ptr, (nuint)fontData.Length, index);
                if (native == IntPtr.Zero)
                {
                    throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to load font.");
                }
                return new Font(native);
            }
        }
    }

    public bool TryGetGlyphIndex(uint codePoint, out ushort glyphId)
    {
        var status = NativeMethods.vello_font_get_glyph_index(Handle, codePoint, out glyphId);
        return status == VelloStatus.Success;
    }

    public bool TryGetGlyphMetrics(ushort glyphId, float fontSize, out GlyphMetrics metrics)
    {
        var status = NativeMethods.vello_font_get_glyph_metrics(Handle, glyphId, fontSize, out var native);
        if (status != VelloStatus.Success)
        {
            metrics = default;
            return false;
        }

        metrics = new GlyphMetrics(native.Advance, native.XBearing, native.YBearing, native.Width, native.Height);
        return true;
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_font_destroy(_handle);
            _handle = IntPtr.Zero;
            GC.SuppressFinalize(this);
        }
    }

    ~Font()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.vello_font_destroy(_handle);
        }
    }
}
