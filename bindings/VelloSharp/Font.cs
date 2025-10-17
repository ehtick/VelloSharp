using System;
using System.Runtime.InteropServices;

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

    public FontMetricsInfo GetMetrics()
    {
        var status = NativeMethods.vello_font_get_metrics(Handle, 1f, out var native);
        NativeHelpers.ThrowOnError(status, "Failed to get font metrics");
        return new FontMetricsInfo(native);
    }

    internal bool TryGetGlyphOutline(ushort glyphId, float fontSize, out VelloPathElement[] commands, out VelloRect bounds)
    {
        commands = Array.Empty<VelloPathElement>();
        bounds = default;
        IntPtr outlineHandle = IntPtr.Zero;

        try
        {
            var status = NativeMethods.vello_font_get_glyph_outline(Handle, glyphId, fontSize, 0f, out outlineHandle);
            if (status != VelloStatus.Success || outlineHandle == IntPtr.Zero)
            {
                return false;
            }

            status = NativeMethods.vello_glyph_outline_get_data(outlineHandle, out var native);
            if (status != VelloStatus.Success || native.CommandCount == 0 || native.Commands == IntPtr.Zero)
            {
                return false;
            }

            var count = checked((int)native.CommandCount);
            commands = new VelloPathElement[count];
            unsafe
            {
                var source = (VelloPathElement*)native.Commands;
                var span = new ReadOnlySpan<VelloPathElement>(source, count);
                span.CopyTo(commands);
            }

            bounds = native.Bounds;
            return true;
        }
        finally
        {
            if (outlineHandle != IntPtr.Zero)
            {
                NativeMethods.vello_glyph_outline_destroy(outlineHandle);
            }
        }
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

public readonly record struct FontMetricsInfo(
    ushort UnitsPerEm,
    ushort GlyphCount,
    float Ascent,
    float Descent,
    float Leading,
    float UnderlinePosition,
    float UnderlineThickness,
    float StrikeoutPosition,
    float StrikeoutThickness,
    bool IsMonospace)
{
    internal FontMetricsInfo(VelloFontMetricsNative native)
        : this(
            native.UnitsPerEm,
            native.GlyphCount,
            native.Ascent,
            native.Descent,
            native.Leading,
            native.UnderlinePosition,
            native.UnderlineThickness,
            native.StrikeoutPosition,
            native.StrikeoutThickness,
            native.IsMonospace)
    {
    }
}
