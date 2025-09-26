using System;
using System.Runtime.InteropServices;
using System.Text;

namespace VelloSharp;

public sealed class ParleyFontContext : IDisposable
{
    private nint _handle;

    public ParleyFontContext()
    {
        _handle = ParleyNativeMethods.parley_font_context_create();
        if (_handle == nint.Zero)
        {
            throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create Parley font context.");
        }
    }

    internal nint Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    public void RegisterFontsFromPath(string path)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrEmpty(path);

        var utf8 = NativeHelpers.AllocUtf8String(path);
        try
        {
            var status = ParleyNativeMethods.parley_font_context_register_fonts_from_path(_handle, utf8);
            NativeHelpers.ThrowOnError(status, $"Failed to register fonts from '{path}'");
        }
        finally
        {
            if (utf8 != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(utf8);
            }
        }
    }

    public unsafe void RegisterFontsFromMemory(ReadOnlySpan<byte> data)
    {
        ThrowIfDisposed();
        if (data.IsEmpty)
        {
            throw new ArgumentException("Font data cannot be empty.", nameof(data));
        }

        var buffer = data.ToArray();
        fixed (byte* ptr = buffer)
        {
            var status = ParleyNativeMethods.parley_font_context_register_fonts_from_memory(_handle, ptr, (nuint)data.Length);
            NativeHelpers.ThrowOnError(status, "Failed to register fonts from memory");
        }
    }

    public void Dispose()
    {
        if (_handle != nint.Zero)
        {
            ParleyNativeMethods.parley_font_context_destroy(_handle);
            _handle = nint.Zero;
            GC.SuppressFinalize(this);
        }
    }

    ~ParleyFontContext()
    {
        if (_handle != nint.Zero)
        {
            ParleyNativeMethods.parley_font_context_destroy(_handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_handle == nint.Zero)
        {
            throw new ObjectDisposedException(nameof(ParleyFontContext));
        }
    }
}

public sealed class ParleyLayoutContext : IDisposable
{
    private nint _handle;

    public ParleyLayoutContext()
    {
        _handle = ParleyNativeMethods.parley_layout_context_create();
        if (_handle == nint.Zero)
        {
            throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Failed to create Parley layout context.");
        }
    }

    internal nint Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    public void Dispose()
    {
        if (_handle != nint.Zero)
        {
            ParleyNativeMethods.parley_layout_context_destroy(_handle);
            _handle = nint.Zero;
            GC.SuppressFinalize(this);
        }
    }

    ~ParleyLayoutContext()
    {
        if (_handle != nint.Zero)
        {
            ParleyNativeMethods.parley_layout_context_destroy(_handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_handle == nint.Zero)
        {
            throw new ObjectDisposedException(nameof(ParleyLayoutContext));
        }
    }
}

public sealed class ParleyLayout : IDisposable
{
    private nint _handle;

    private ParleyLayout(nint handle)
    {
        _handle = handle;
    }

    public static ParleyLayout Build(
        ParleyLayoutContext layoutContext,
        ParleyFontContext fontContext,
        string text,
        float scale,
        bool quantize,
        ReadOnlySpan<ParleyStyleProperty> defaultProperties,
        ReadOnlySpan<ParleyStyleSpan> spans)
    {
        ArgumentNullException.ThrowIfNull(text);
        var bytes = Encoding.UTF8.GetBytes(text);
        return Build(layoutContext, fontContext, bytes, scale, quantize, defaultProperties, spans);
    }

    public static ParleyLayout Build(
        ParleyLayoutContext layoutContext,
        ParleyFontContext fontContext,
        ReadOnlySpan<byte> utf8Text,
        float scale,
        bool quantize,
        ReadOnlySpan<ParleyStyleProperty> defaultProperties,
        ReadOnlySpan<ParleyStyleSpan> spans)
    {
        ArgumentNullException.ThrowIfNull(layoutContext);
        ArgumentNullException.ThrowIfNull(fontContext);
        if (utf8Text.IsEmpty)
        {
            throw new ArgumentException("Text cannot be empty.", nameof(utf8Text));
        }

        using var defaultsNative = new NativeStylePropertyList(defaultProperties);
        using var spansNative = new NativeStyleSpanList(spans);

        var layoutHandle = layoutContext.Handle;
        var fontHandle = fontContext.Handle;

        var textBuffer = utf8Text.ToArray();

        unsafe
        {
            fixed (byte* textPtr = textBuffer)
            {
                var status = ParleyNativeMethods.parley_layout_build_ranged(
                    layoutHandle,
                    fontHandle,
                    textPtr,
                    (nuint)textBuffer.Length,
                    scale,
                    quantize,
                    defaultsNative.Pointer,
                    (nuint)defaultsNative.Native.Length,
                    spansNative.Pointer,
                    (nuint)spansNative.Native.Length,
                    out var layoutPtr);

                NativeHelpers.ThrowOnError(status, "Failed to build Parley layout");
                return new ParleyLayout(layoutPtr);
            }
        }
    }

    internal nint Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    public float Scale
    {
        get
        {
            ThrowIfDisposed();
            unsafe
            {
                float value = 0f;
                NativeHelpers.ThrowOnError(ParleyNativeMethods.parley_layout_scale(_handle, &value), "Failed to query scale");
                return value;
            }
        }
    }

    public float Width
    {
        get
        {
            ThrowIfDisposed();
            unsafe
            {
                float value = 0f;
                NativeHelpers.ThrowOnError(ParleyNativeMethods.parley_layout_width(_handle, &value), "Failed to query width");
                return value;
            }
        }
    }

    public float FullWidth
    {
        get
        {
            ThrowIfDisposed();
            unsafe
            {
                float value = 0f;
                NativeHelpers.ThrowOnError(ParleyNativeMethods.parley_layout_full_width(_handle, &value), "Failed to query full width");
                return value;
            }
        }
    }

    public float Height
    {
        get
        {
            ThrowIfDisposed();
            unsafe
            {
                float value = 0f;
                NativeHelpers.ThrowOnError(ParleyNativeMethods.parley_layout_height(_handle, &value), "Failed to query height");
                return value;
            }
        }
    }

    public bool IsRightToLeft
    {
        get
        {
            ThrowIfDisposed();
            unsafe
            {
                byte value = 0;
                NativeHelpers.ThrowOnError(ParleyNativeMethods.parley_layout_is_rtl(_handle, &value), "Failed to query layout direction");
                return value != 0;
            }
        }
    }

    public int LineCount
    {
        get
        {
            ThrowIfDisposed();
            unsafe
            {
                nuint count = 0;
                NativeHelpers.ThrowOnError(ParleyNativeMethods.parley_layout_line_count(_handle, &count), "Failed to query line count");
                return (int)count;
            }
        }
    }

    public void BreakAllLines(float? maxWidth)
    {
        ThrowIfDisposed();
        var status = ParleyNativeMethods.parley_layout_break_all_lines(_handle, maxWidth ?? 0f, maxWidth.HasValue);
        NativeHelpers.ThrowOnError(status, "Failed to break lines");
    }

    public void Align(ParleyAlignmentKind alignment, float? containerWidth, bool alignWhenOverflowing)
    {
        ThrowIfDisposed();
        var options = new ParleyAlignmentOptionsNative
        {
            AlignWhenOverflowing = alignWhenOverflowing ? (byte)1 : (byte)0,
        };
        var status = ParleyNativeMethods.parley_layout_align(
            _handle,
            (ParleyAlignmentKindNative)alignment,
            containerWidth ?? 0f,
            containerWidth.HasValue,
            options);
        NativeHelpers.ThrowOnError(status, "Failed to align layout");
    }

    public ParleyLineInfo GetLineInfo(int index)
    {
        ThrowIfDisposed();
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        unsafe
        {
            ParleyLineInfoNative native = default;
            NativeHelpers.ThrowOnError(ParleyNativeMethods.parley_layout_line_get_info(_handle, (nuint)index, &native), "Failed to get line info");
            return new ParleyLineInfo(native);
        }
    }

    public int GetGlyphRunCount(int lineIndex)
    {
        ThrowIfDisposed();
        if (lineIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineIndex));
        }

        unsafe
        {
            nuint count = 0;
            NativeHelpers.ThrowOnError(ParleyNativeMethods.parley_layout_line_get_glyph_run_count(_handle, (nuint)lineIndex, &count), "Failed to get glyph run count");
            return (int)count;
        }
    }

    public ParleyGlyphRunInfo GetGlyphRunInfo(int lineIndex, int runIndex)
    {
        ThrowIfDisposed();
        if (lineIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineIndex));
        }
        if (runIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(runIndex));
        }

        unsafe
        {
            ParleyGlyphRunInfoNative native = default;
            NativeHelpers.ThrowOnError(ParleyNativeMethods.parley_layout_line_get_glyph_run_info(_handle, (nuint)lineIndex, (nuint)runIndex, &native), "Failed to get glyph run info");
            return new ParleyGlyphRunInfo(native);
        }
    }

    public ParleyGlyphInfo[] GetGlyphs(int lineIndex, int runIndex)
    {
        ThrowIfDisposed();
        if (lineIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineIndex));
        }
        if (runIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(runIndex));
        }

        unsafe
        {
            nuint written = 0;
            var status = ParleyNativeMethods.parley_layout_line_copy_glyph_run_glyphs(_handle, (nuint)lineIndex, (nuint)runIndex, null, 0, &written);
            if (status != ParleyStatus.Success && status != ParleyStatus.BufferTooSmall)
            {
                NativeHelpers.ThrowOnError(status, "Failed to query glyph run size");
            }

            if (written == 0)
            {
                return Array.Empty<ParleyGlyphInfo>();
            }

            var nativeGlyphs = new ParleyGlyph[(int)written];
            fixed (ParleyGlyph* ptr = nativeGlyphs)
            {
                NativeHelpers.ThrowOnError(
                    ParleyNativeMethods.parley_layout_line_copy_glyph_run_glyphs(_handle, (nuint)lineIndex, (nuint)runIndex, ptr, (nuint)nativeGlyphs.Length, &written),
                    "Failed to copy glyph run glyphs");
            }

            var managed = new ParleyGlyphInfo[nativeGlyphs.Length];
            for (var i = 0; i < nativeGlyphs.Length; i++)
            {
                managed[i] = new ParleyGlyphInfo(nativeGlyphs[i]);
            }
            return managed;
        }
    }

    public int GetInlineBoxCount(int lineIndex)
    {
        ThrowIfDisposed();
        if (lineIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineIndex));
        }

        unsafe
        {
            nuint count = 0;
            NativeHelpers.ThrowOnError(ParleyNativeMethods.parley_layout_line_get_inline_box_count(_handle, (nuint)lineIndex, &count), "Failed to get inline box count");
            return (int)count;
        }
    }

    public ParleyInlineBoxInfo GetInlineBoxInfo(int lineIndex, int boxIndex)
    {
        ThrowIfDisposed();
        if (lineIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineIndex));
        }
        if (boxIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(boxIndex));
        }

        unsafe
        {
            ParleyInlineBoxInfoNative native = default;
            NativeHelpers.ThrowOnError(ParleyNativeMethods.parley_layout_line_get_inline_box_info(_handle, (nuint)lineIndex, (nuint)boxIndex, &native), "Failed to get inline box info");
            return new ParleyInlineBoxInfo(native);
        }
    }

    public int StyleCount
    {
        get
        {
            ThrowIfDisposed();
            unsafe
            {
                nuint count = 0;
                NativeHelpers.ThrowOnError(ParleyNativeMethods.parley_layout_style_count(_handle, &count), "Failed to get style count");
                return (int)count;
            }
        }
    }

    public ParleyStyleInfo GetStyleInfo(int index)
    {
        ThrowIfDisposed();
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        unsafe
        {
            ParleyStyleInfoNative native = default;
            NativeHelpers.ThrowOnError(ParleyNativeMethods.parley_layout_get_style_info(_handle, (nuint)index, &native), "Failed to get style info");
            return new ParleyStyleInfo(native);
        }
    }

    public void Dispose()
    {
        if (_handle != nint.Zero)
        {
            ParleyNativeMethods.parley_layout_destroy(_handle);
            _handle = nint.Zero;
            GC.SuppressFinalize(this);
        }
    }

    ~ParleyLayout()
    {
        if (_handle != nint.Zero)
        {
            ParleyNativeMethods.parley_layout_destroy(_handle);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_handle == nint.Zero)
        {
            throw new ObjectDisposedException(nameof(ParleyLayout));
        }
    }
}
