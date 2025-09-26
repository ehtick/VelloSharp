using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VelloSharp;

internal static partial class ParleyNativeMethods
{
    private const string LibraryName = "parley_ffi";

    [LibraryImport(LibraryName, EntryPoint = "parley_last_error_message")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nint parley_last_error_message();

    [LibraryImport(LibraryName, EntryPoint = "parley_font_context_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nint parley_font_context_create();

    [LibraryImport(LibraryName, EntryPoint = "parley_font_context_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void parley_font_context_destroy(nint ctx);

    [LibraryImport(LibraryName, EntryPoint = "parley_font_context_register_fonts_from_path")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial ParleyStatus parley_font_context_register_fonts_from_path(nint ctx, nint path);

    [LibraryImport(LibraryName, EntryPoint = "parley_font_context_register_fonts_from_memory")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial ParleyStatus parley_font_context_register_fonts_from_memory(nint ctx, byte* data, nuint length);

    [LibraryImport(LibraryName, EntryPoint = "parley_layout_context_create")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial nint parley_layout_context_create();

    [LibraryImport(LibraryName, EntryPoint = "parley_layout_context_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void parley_layout_context_destroy(nint ctx);

    [LibraryImport(LibraryName, EntryPoint = "parley_layout_build_ranged")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial ParleyStatus parley_layout_build_ranged(
        nint layoutCtx,
        nint fontCtx,
        byte* text,
        nuint textLength,
        float scale,
        [MarshalAs(UnmanagedType.I1)] bool quantize,
        ParleyStylePropertyNative* defaults,
        nuint defaultLength,
        ParleyStyleSpanNative* spans,
        nuint spanLength,
        out nint layout);

    [LibraryImport(LibraryName, EntryPoint = "parley_layout_destroy")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial void parley_layout_destroy(nint layout);

    [LibraryImport(LibraryName, EntryPoint = "parley_layout_scale")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial ParleyStatus parley_layout_scale(nint layout, float* scale);

    [LibraryImport(LibraryName, EntryPoint = "parley_layout_width")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial ParleyStatus parley_layout_width(nint layout, float* width);

    [LibraryImport(LibraryName, EntryPoint = "parley_layout_full_width")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial ParleyStatus parley_layout_full_width(nint layout, float* width);

    [LibraryImport(LibraryName, EntryPoint = "parley_layout_height")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial ParleyStatus parley_layout_height(nint layout, float* height);

    [LibraryImport(LibraryName, EntryPoint = "parley_layout_is_rtl")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial ParleyStatus parley_layout_is_rtl(nint layout, byte* isRtl);

    [LibraryImport(LibraryName, EntryPoint = "parley_layout_break_all_lines")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial ParleyStatus parley_layout_break_all_lines(
        nint layout,
        float maxWidth,
        [MarshalAs(UnmanagedType.I1)] bool hasMaxWidth);

    [LibraryImport(LibraryName, EntryPoint = "parley_layout_align")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static partial ParleyStatus parley_layout_align(
        nint layout,
        ParleyAlignmentKindNative alignment,
        float containerWidth,
        [MarshalAs(UnmanagedType.I1)] bool hasContainerWidth,
        ParleyAlignmentOptionsNative options);

    [LibraryImport(LibraryName, EntryPoint = "parley_layout_line_count")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial ParleyStatus parley_layout_line_count(nint layout, nuint* count);

    [LibraryImport(LibraryName, EntryPoint = "parley_layout_line_get_info")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial ParleyStatus parley_layout_line_get_info(nint layout, nuint lineIndex, ParleyLineInfoNative* info);

    [LibraryImport(LibraryName, EntryPoint = "parley_layout_line_get_glyph_run_count")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial ParleyStatus parley_layout_line_get_glyph_run_count(nint layout, nuint lineIndex, nuint* count);

    [LibraryImport(LibraryName, EntryPoint = "parley_layout_line_get_glyph_run_info")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial ParleyStatus parley_layout_line_get_glyph_run_info(nint layout, nuint lineIndex, nuint runIndex, ParleyGlyphRunInfoNative* info);

    [LibraryImport(LibraryName, EntryPoint = "parley_layout_line_copy_glyph_run_glyphs")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial ParleyStatus parley_layout_line_copy_glyph_run_glyphs(
        nint layout,
        nuint lineIndex,
        nuint runIndex,
        ParleyGlyph* glyphs,
        nuint capacity,
        nuint* written);

    [LibraryImport(LibraryName, EntryPoint = "parley_layout_line_get_inline_box_count")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial ParleyStatus parley_layout_line_get_inline_box_count(nint layout, nuint lineIndex, nuint* count);

    [LibraryImport(LibraryName, EntryPoint = "parley_layout_line_get_inline_box_info")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial ParleyStatus parley_layout_line_get_inline_box_info(nint layout, nuint lineIndex, nuint boxIndex, ParleyInlineBoxInfoNative* info);

    [LibraryImport(LibraryName, EntryPoint = "parley_layout_style_count")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial ParleyStatus parley_layout_style_count(nint layout, nuint* count);

    [LibraryImport(LibraryName, EntryPoint = "parley_layout_get_style_info")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static unsafe partial ParleyStatus parley_layout_get_style_info(nint layout, nuint styleIndex, ParleyStyleInfoNative* info);
}
