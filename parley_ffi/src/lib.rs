#![cfg_attr(not(test), deny(clippy::all))]
#![allow(clippy::missing_safety_doc)]
#![allow(missing_docs)]
#![allow(missing_debug_implementations)]

use std::{
    borrow::Cow,
    cell::RefCell,
    ffi::{CStr, CString, c_char},
    ops::Range,
    ptr, slice,
};

use parley::fontique::Blob;
use parley::layout::{GlyphRun, Line, PositionedInlineBox, PositionedLayoutItem};
use parley::style::{FontStack, FontStyle, FontWeight, FontWidth, LineHeight};
use parley::{
    Alignment, AlignmentOptions, BreakReason, FontContext, Layout, LayoutContext, OverflowWrap,
    StyleProperty,
};

type BrushColor = [u8; 4];
type ParleyLayoutInner = Layout<BrushColor>;

thread_local! {
    static LAST_ERROR: RefCell<Option<CString>> = const { RefCell::new(None) };
}

fn clear_last_error() {
    LAST_ERROR.with(|slot| slot.borrow_mut().take());
}

fn set_last_error(msg: impl Into<String>) {
    let msg = msg.into();
    let cstr = CString::new(msg).unwrap_or_else(|_| CString::new("invalid error message").unwrap());
    LAST_ERROR.with(|slot| *slot.borrow_mut() = Some(cstr));
}

unsafe fn slice_from_raw<'a, T>(ptr: *const T, len: usize) -> Result<&'a [T], ParleyStatus> {
    if len == 0 {
        Ok(&[])
    } else if ptr.is_null() {
        set_last_error("slice pointer is null");
        Err(ParleyStatus::NullPointer)
    } else {
        Ok(unsafe { slice::from_raw_parts(ptr, len) })
    }
}

unsafe fn str_from_raw<'a>(ptr: *const c_char, len: usize) -> Result<&'a str, ParleyStatus> {
    if len == 0 {
        return Ok("");
    }
    let bytes = unsafe { slice_from_raw(ptr.cast::<u8>(), len)? };
    std::str::from_utf8(bytes).map_err(|_| {
        set_last_error("string must be valid UTF-8");
        ParleyStatus::Utf8Error
    })
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum ParleyStatus {
    Success = 0,
    NullPointer = 1,
    InvalidArgument = 2,
    OutOfMemory = 3,
    BufferTooSmall = 4,
    IndexOutOfBounds = 5,
    Utf8Error = 6,
    IoError = 7,
}

#[repr(C)]
pub struct ParleyFontContextHandle {
    ctx: FontContext,
}

#[repr(C)]
pub struct ParleyLayoutContextHandle {
    ctx: LayoutContext<BrushColor>,
}

#[repr(C)]
pub struct ParleyLayoutHandle {
    layout: ParleyLayoutInner,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, Default)]
pub struct ParleyColor {
    pub r: u8,
    pub g: u8,
    pub b: u8,
    pub a: u8,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum ParleyStylePropertyKind {
    FontStack = 0,
    FontSize = 1,
    FontWeight = 2,
    FontStyle = 3,
    FontWidth = 4,
    Brush = 5,
    LineHeight = 6,
    LetterSpacing = 7,
    WordSpacing = 8,
    Locale = 9,
    Underline = 10,
    UnderlineOffset = 11,
    UnderlineSize = 12,
    UnderlineBrush = 13,
    Strikethrough = 14,
    StrikethroughOffset = 15,
    StrikethroughSize = 16,
    StrikethroughBrush = 17,
    OverflowWrap = 18,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum ParleyFontStyle {
    Normal = 0,
    Italic = 1,
    Oblique = 2,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum ParleyLineHeightKind {
    MetricsRelative = 0,
    FontSizeRelative = 1,
    Absolute = 2,
}

impl Default for ParleyLineHeightKind {
    fn default() -> Self {
        ParleyLineHeightKind::MetricsRelative
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum ParleyOverflowWrapMode {
    Normal = 0,
    Anywhere = 1,
    BreakWord = 2,
}

impl Default for ParleyOverflowWrapMode {
    fn default() -> Self {
        ParleyOverflowWrapMode::Normal
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct ParleyStyleProperty {
    pub kind: ParleyStylePropertyKind,
    pub value_f32: f32,
    pub value_i32: i32,
    pub value_bool: bool,
    pub color: ParleyColor,
    pub font_style: ParleyFontStyle,
    pub font_style_angle: f32,
    pub line_height_kind: ParleyLineHeightKind,
    pub string_ptr: *const c_char,
    pub string_len: usize,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct ParleyStyleSpan {
    pub range_start: usize,
    pub range_end: usize,
    pub property: ParleyStyleProperty,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum ParleyAlignmentKind {
    Start = 0,
    End = 1,
    Left = 2,
    Center = 3,
    Right = 4,
    Justify = 5,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct ParleyAlignmentOptions {
    pub align_when_overflowing: bool,
}

impl Default for ParleyAlignmentOptions {
    fn default() -> Self {
        Self {
            align_when_overflowing: false,
        }
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum ParleyBreakReason {
    None = 0,
    Regular = 1,
    Explicit = 2,
    Emergency = 3,
}

impl Default for ParleyBreakReason {
    fn default() -> Self {
        ParleyBreakReason::None
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone, Default)]
pub struct ParleyLineInfo {
    pub text_start: usize,
    pub text_end: usize,
    pub break_reason: ParleyBreakReason,
    pub advance: f32,
    pub trailing_whitespace: f32,
    pub line_height: f32,
    pub baseline: f32,
    pub offset: f32,
    pub ascent: f32,
    pub descent: f32,
    pub leading: f32,
    pub min_coord: f32,
    pub max_coord: f32,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, Default)]
pub struct ParleyGlyph {
    pub id: u32,
    pub style_index: u16,
    pub _reserved: u16,
    pub x: f32,
    pub y: f32,
    pub advance: f32,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, Default)]
pub struct ParleyGlyphRunInfo {
    pub glyph_count: usize,
    pub style_index: u16,
    pub font_blob_id: u64,
    pub font_index: u32,
    pub font_data: *const u8,
    pub font_data_len: usize,
    pub font_size: f32,
    pub ascent: f32,
    pub descent: f32,
    pub leading: f32,
    pub baseline: f32,
    pub offset: f32,
    pub advance: f32,
    pub is_rtl: bool,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, Default)]
pub struct ParleyInlineBoxInfo {
    pub id: u64,
    pub x: f32,
    pub y: f32,
    pub width: f32,
    pub height: f32,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, Default)]
pub struct ParleyStyleInfo {
    pub brush: ParleyColor,
    pub underline: bool,
    pub underline_offset: f32,
    pub underline_size: f32,
    pub underline_brush: ParleyColor,
    pub strikethrough: bool,
    pub strikethrough_offset: f32,
    pub strikethrough_size: f32,
    pub strikethrough_brush: ParleyColor,
}

#[unsafe(no_mangle)]
pub extern "C" fn parley_last_error_message() -> *const c_char {
    LAST_ERROR.with(|slot| match slot.borrow().as_ref() {
        Some(cstr) => cstr.as_ptr(),
        None => ptr::null(),
    })
}

#[unsafe(no_mangle)]
pub extern "C" fn parley_font_context_create() -> *mut ParleyFontContextHandle {
    clear_last_error();
    let ctx = FontContext::new();
    Box::into_raw(Box::new(ParleyFontContextHandle { ctx }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_font_context_destroy(ctx: *mut ParleyFontContextHandle) {
    if !ctx.is_null() {
        unsafe { drop(Box::from_raw(ctx)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_font_context_register_fonts_from_path(
    ctx: *mut ParleyFontContextHandle,
    path: *const c_char,
) -> ParleyStatus {
    clear_last_error();
    let Some(ctx) = (unsafe { ctx.as_mut() }) else {
        return ParleyStatus::NullPointer;
    };
    if path.is_null() {
        set_last_error("path pointer is null");
        return ParleyStatus::NullPointer;
    }
    let c_path = unsafe { CStr::from_ptr(path) };
    let path_str = match c_path.to_str() {
        Ok(value) => value,
        Err(_) => {
            set_last_error("path must be valid UTF-8");
            return ParleyStatus::Utf8Error;
        }
    };
    match std::fs::read(path_str) {
        Ok(bytes) => register_fonts(ctx, bytes),
        Err(err) => {
            set_last_error(err.to_string());
            ParleyStatus::IoError
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_font_context_register_fonts_from_memory(
    ctx: *mut ParleyFontContextHandle,
    data: *const u8,
    length: usize,
) -> ParleyStatus {
    clear_last_error();
    let Some(ctx) = (unsafe { ctx.as_mut() }) else {
        return ParleyStatus::NullPointer;
    };
    if length == 0 {
        set_last_error("font data length must be greater than zero");
        return ParleyStatus::InvalidArgument;
    }
    let slice = match unsafe { slice_from_raw(data, length) } {
        Ok(slice) => slice,
        Err(status) => return status,
    };
    register_fonts(ctx, slice.to_vec())
}

fn register_fonts(ctx: &mut ParleyFontContextHandle, bytes: Vec<u8>) -> ParleyStatus {
    if bytes.is_empty() {
        set_last_error("font data was empty");
        return ParleyStatus::InvalidArgument;
    }
    let blob = Blob::from(bytes);
    let registered = ctx.ctx.collection.register_fonts(blob, None);
    if registered.is_empty() {
        set_last_error("failed to register any fonts from the provided data");
        return ParleyStatus::InvalidArgument;
    }
    ParleyStatus::Success
}

#[unsafe(no_mangle)]
pub extern "C" fn parley_layout_context_create() -> *mut ParleyLayoutContextHandle {
    clear_last_error();
    let ctx = LayoutContext::new();
    Box::into_raw(Box::new(ParleyLayoutContextHandle { ctx }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_layout_context_destroy(ctx: *mut ParleyLayoutContextHandle) {
    if !ctx.is_null() {
        unsafe { drop(Box::from_raw(ctx)) };
    }
}
#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_layout_build_ranged(
    layout_ctx: *mut ParleyLayoutContextHandle,
    font_ctx: *mut ParleyFontContextHandle,
    text: *const u8,
    text_len: usize,
    scale: f32,
    quantize: bool,
    defaults: *const ParleyStyleProperty,
    default_len: usize,
    spans: *const ParleyStyleSpan,
    span_len: usize,
    out_layout: *mut *mut ParleyLayoutHandle,
) -> ParleyStatus {
    clear_last_error();
    if out_layout.is_null() {
        set_last_error("out_layout pointer is null");
        return ParleyStatus::NullPointer;
    }
    let Some(layout_ctx) = (unsafe { layout_ctx.as_mut() }) else {
        set_last_error("layout context pointer is null");
        return ParleyStatus::NullPointer;
    };
    let Some(font_ctx) = (unsafe { font_ctx.as_mut() }) else {
        set_last_error("font context pointer is null");
        return ParleyStatus::NullPointer;
    };

    let bytes = match unsafe { slice_from_raw(text, text_len) } {
        Ok(slice) => slice,
        Err(status) => return status,
    };
    let text_string = match std::str::from_utf8(bytes) {
        Ok(value) => value.to_owned(),
        Err(_) => {
            set_last_error("text must be valid UTF-8");
            return ParleyStatus::Utf8Error;
        }
    };

    let default_properties = match unsafe { slice_from_raw(defaults, default_len) } {
        Ok(slice) => slice,
        Err(status) => return status,
    };
    let span_properties = match unsafe { slice_from_raw(spans, span_len) } {
        Ok(slice) => slice,
        Err(status) => return status,
    };

    let mut builder =
        layout_ctx
            .ctx
            .ranged_builder(&mut font_ctx.ctx, text_string.as_str(), scale, quantize);

    for property in default_properties {
        let style_property = match convert_style_property(property) {
            Ok(value) => value,
            Err(status) => return status,
        };
        builder.push_default(style_property);
    }

    for span in span_properties {
        let style_property = match convert_style_property(&span.property) {
            Ok(value) => value,
            Err(status) => return status,
        };
        let range = match span_range(span, text_string.len()) {
            Ok(range) => range,
            Err(status) => return status,
        };
        builder.push(style_property, range);
    }

    let layout = builder.build(text_string.as_str());
    let handle = Box::new(ParleyLayoutHandle { layout });
    unsafe { *out_layout = Box::into_raw(handle) };
    ParleyStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_layout_destroy(layout: *mut ParleyLayoutHandle) {
    if !layout.is_null() {
        unsafe { drop(Box::from_raw(layout)) };
    }
}
#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_layout_scale(
    layout: *const ParleyLayoutHandle,
    scale: *mut f32,
) -> ParleyStatus {
    clear_last_error();
    let Some(layout) = (unsafe { layout.as_ref() }) else {
        set_last_error("layout pointer is null");
        return ParleyStatus::NullPointer;
    };
    if scale.is_null() {
        set_last_error("scale pointer is null");
        return ParleyStatus::NullPointer;
    }
    unsafe { *scale = layout.layout.scale() };
    ParleyStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_layout_width(
    layout: *const ParleyLayoutHandle,
    width: *mut f32,
) -> ParleyStatus {
    clear_last_error();
    let Some(layout) = (unsafe { layout.as_ref() }) else {
        set_last_error("layout pointer is null");
        return ParleyStatus::NullPointer;
    };
    if width.is_null() {
        set_last_error("width pointer is null");
        return ParleyStatus::NullPointer;
    }
    unsafe { *width = layout.layout.width() };
    ParleyStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_layout_full_width(
    layout: *const ParleyLayoutHandle,
    width: *mut f32,
) -> ParleyStatus {
    clear_last_error();
    let Some(layout) = (unsafe { layout.as_ref() }) else {
        set_last_error("layout pointer is null");
        return ParleyStatus::NullPointer;
    };
    if width.is_null() {
        set_last_error("width pointer is null");
        return ParleyStatus::NullPointer;
    }
    unsafe { *width = layout.layout.full_width() };
    ParleyStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_layout_height(
    layout: *const ParleyLayoutHandle,
    height: *mut f32,
) -> ParleyStatus {
    clear_last_error();
    let Some(layout) = (unsafe { layout.as_ref() }) else {
        set_last_error("layout pointer is null");
        return ParleyStatus::NullPointer;
    };
    if height.is_null() {
        set_last_error("height pointer is null");
        return ParleyStatus::NullPointer;
    }
    unsafe { *height = layout.layout.height() };
    ParleyStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_layout_is_rtl(
    layout: *const ParleyLayoutHandle,
    is_rtl: *mut bool,
) -> ParleyStatus {
    clear_last_error();
    let Some(layout) = (unsafe { layout.as_ref() }) else {
        set_last_error("layout pointer is null");
        return ParleyStatus::NullPointer;
    };
    if is_rtl.is_null() {
        set_last_error("is_rtl pointer is null");
        return ParleyStatus::NullPointer;
    }
    unsafe { *is_rtl = layout.layout.is_rtl() };
    ParleyStatus::Success
}
#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_layout_break_all_lines(
    layout: *mut ParleyLayoutHandle,
    max_width: f32,
    has_max_width: bool,
) -> ParleyStatus {
    clear_last_error();
    let Some(layout) = (unsafe { layout.as_mut() }) else {
        set_last_error("layout pointer is null");
        return ParleyStatus::NullPointer;
    };
    let limit = if has_max_width { Some(max_width) } else { None };
    layout.layout.break_all_lines(limit);
    ParleyStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_layout_align(
    layout: *mut ParleyLayoutHandle,
    alignment_kind: ParleyAlignmentKind,
    container_width: f32,
    has_container_width: bool,
    options: ParleyAlignmentOptions,
) -> ParleyStatus {
    clear_last_error();
    let Some(layout) = (unsafe { layout.as_mut() }) else {
        set_last_error("layout pointer is null");
        return ParleyStatus::NullPointer;
    };
    let alignment = match alignment_from_kind(alignment_kind) {
        Some(value) => value,
        None => return ParleyStatus::InvalidArgument,
    };
    let container = if has_container_width {
        Some(container_width)
    } else {
        None
    };
    let options = AlignmentOptions {
        align_when_overflowing: options.align_when_overflowing,
    };
    layout.layout.align(container, alignment, options);
    ParleyStatus::Success
}
#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_layout_line_count(
    layout: *const ParleyLayoutHandle,
    count: *mut usize,
) -> ParleyStatus {
    clear_last_error();
    let Some(layout) = (unsafe { layout.as_ref() }) else {
        set_last_error("layout pointer is null");
        return ParleyStatus::NullPointer;
    };
    if count.is_null() {
        set_last_error("count pointer is null");
        return ParleyStatus::NullPointer;
    }
    unsafe { *count = layout.layout.len() };
    ParleyStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_layout_line_get_info(
    layout: *const ParleyLayoutHandle,
    line_index: usize,
    info: *mut ParleyLineInfo,
) -> ParleyStatus {
    clear_last_error();
    let Some(layout) = (unsafe { layout.as_ref() }) else {
        set_last_error("layout pointer is null");
        return ParleyStatus::NullPointer;
    };
    if info.is_null() {
        set_last_error("info pointer is null");
        return ParleyStatus::NullPointer;
    }
    let Some(line) = layout.layout.get(line_index) else {
        set_last_error("line index out of bounds");
        return ParleyStatus::IndexOutOfBounds;
    };
    let mut output = ParleyLineInfo::default();
    let metrics = line.metrics();
    let range = line.text_range();
    output.text_start = range.start;
    output.text_end = range.end;
    output.break_reason = break_reason_to_enum(line.break_reason());
    output.advance = metrics.advance;
    output.trailing_whitespace = metrics.trailing_whitespace;
    output.line_height = metrics.line_height;
    output.baseline = metrics.baseline;
    output.offset = metrics.offset;
    output.ascent = metrics.ascent;
    output.descent = metrics.descent;
    output.leading = metrics.leading;
    output.min_coord = metrics.min_coord;
    output.max_coord = metrics.max_coord;
    unsafe { *info = output };
    ParleyStatus::Success
}
#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_layout_line_get_glyph_run_count(
    layout: *const ParleyLayoutHandle,
    line_index: usize,
    count: *mut usize,
) -> ParleyStatus {
    clear_last_error();
    let Some(layout) = (unsafe { layout.as_ref() }) else {
        set_last_error("layout pointer is null");
        return ParleyStatus::NullPointer;
    };
    if count.is_null() {
        set_last_error("count pointer is null");
        return ParleyStatus::NullPointer;
    }
    let Some(line) = layout.layout.get(line_index) else {
        set_last_error("line index out of bounds");
        return ParleyStatus::IndexOutOfBounds;
    };
    unsafe { *count = glyph_run_count(line) };
    ParleyStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_layout_line_get_glyph_run_info(
    layout: *const ParleyLayoutHandle,
    line_index: usize,
    run_index: usize,
    info: *mut ParleyGlyphRunInfo,
) -> ParleyStatus {
    clear_last_error();
    let Some(layout) = (unsafe { layout.as_ref() }) else {
        set_last_error("layout pointer is null");
        return ParleyStatus::NullPointer;
    };
    if info.is_null() {
        set_last_error("info pointer is null");
        return ParleyStatus::NullPointer;
    }
    let Some(line) = layout.layout.get(line_index) else {
        set_last_error("line index out of bounds");
        return ParleyStatus::IndexOutOfBounds;
    };
    let Some(run) = nth_glyph_run(line, run_index) else {
        set_last_error("glyph run index out of bounds");
        return ParleyStatus::IndexOutOfBounds;
    };
    let run_info = match build_glyph_run_info(&run) {
        Ok(info) => info,
        Err(status) => return status,
    };
    unsafe { *info = run_info };
    ParleyStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_layout_line_copy_glyph_run_glyphs(
    layout: *const ParleyLayoutHandle,
    line_index: usize,
    run_index: usize,
    glyphs: *mut ParleyGlyph,
    capacity: usize,
    written: *mut usize,
) -> ParleyStatus {
    clear_last_error();
    let Some(layout) = (unsafe { layout.as_ref() }) else {
        set_last_error("layout pointer is null");
        return ParleyStatus::NullPointer;
    };
    if written.is_null() {
        set_last_error("written pointer is null");
        return ParleyStatus::NullPointer;
    }
    if capacity > 0 && glyphs.is_null() {
        set_last_error("glyph buffer pointer is null");
        return ParleyStatus::NullPointer;
    }
    let Some(line) = layout.layout.get(line_index) else {
        set_last_error("line index out of bounds");
        return ParleyStatus::IndexOutOfBounds;
    };
    let Some(run) = nth_glyph_run(line, run_index) else {
        set_last_error("glyph run index out of bounds");
        return ParleyStatus::IndexOutOfBounds;
    };

    let glyphs_iter = run.positioned_glyphs();
    let total = glyphs_iter.clone().count();
    unsafe { *written = total };
    if capacity < total {
        return ParleyStatus::BufferTooSmall;
    }
    if total == 0 {
        return ParleyStatus::Success;
    }
    let buffer = unsafe { std::slice::from_raw_parts_mut(glyphs, capacity) };
    for (idx, glyph) in glyphs_iter.enumerate() {
        if idx >= capacity {
            break;
        }
        let mut entry = ParleyGlyph::default();
        entry.id = glyph.id;
        let style_index = glyph.style_index();
        if style_index > u16::MAX as usize {
            set_last_error("style index exceeds supported range");
            return ParleyStatus::InvalidArgument;
        }
        entry.style_index = style_index as u16;
        entry.x = glyph.x;
        entry.y = glyph.y;
        entry.advance = glyph.advance;
        buffer[idx] = entry;
    }
    ParleyStatus::Success
}
#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_layout_line_get_inline_box_count(
    layout: *const ParleyLayoutHandle,
    line_index: usize,
    count: *mut usize,
) -> ParleyStatus {
    clear_last_error();
    let Some(layout) = (unsafe { layout.as_ref() }) else {
        set_last_error("layout pointer is null");
        return ParleyStatus::NullPointer;
    };
    if count.is_null() {
        set_last_error("count pointer is null");
        return ParleyStatus::NullPointer;
    }
    let Some(line) = layout.layout.get(line_index) else {
        set_last_error("line index out of bounds");
        return ParleyStatus::IndexOutOfBounds;
    };
    unsafe { *count = inline_box_count(line) };
    ParleyStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_layout_line_get_inline_box_info(
    layout: *const ParleyLayoutHandle,
    line_index: usize,
    box_index: usize,
    info: *mut ParleyInlineBoxInfo,
) -> ParleyStatus {
    clear_last_error();
    let Some(layout) = (unsafe { layout.as_ref() }) else {
        set_last_error("layout pointer is null");
        return ParleyStatus::NullPointer;
    };
    if info.is_null() {
        set_last_error("info pointer is null");
        return ParleyStatus::NullPointer;
    }
    let Some(line) = layout.layout.get(line_index) else {
        set_last_error("line index out of bounds");
        return ParleyStatus::IndexOutOfBounds;
    };
    let Some(inline_box) = nth_inline_box(line, box_index) else {
        set_last_error("inline box index out of bounds");
        return ParleyStatus::IndexOutOfBounds;
    };
    let mut output = ParleyInlineBoxInfo::default();
    output.id = inline_box.id;
    output.x = inline_box.x;
    output.y = inline_box.y;
    output.width = inline_box.width;
    output.height = inline_box.height;
    unsafe { *info = output };
    ParleyStatus::Success
}
#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_layout_style_count(
    layout: *const ParleyLayoutHandle,
    count: *mut usize,
) -> ParleyStatus {
    clear_last_error();
    let Some(layout) = (unsafe { layout.as_ref() }) else {
        set_last_error("layout pointer is null");
        return ParleyStatus::NullPointer;
    };
    if count.is_null() {
        set_last_error("count pointer is null");
        return ParleyStatus::NullPointer;
    }
    unsafe { *count = layout.layout.styles().len() };
    ParleyStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn parley_layout_get_style_info(
    layout: *const ParleyLayoutHandle,
    style_index: usize,
    info: *mut ParleyStyleInfo,
) -> ParleyStatus {
    clear_last_error();
    let Some(layout) = (unsafe { layout.as_ref() }) else {
        set_last_error("layout pointer is null");
        return ParleyStatus::NullPointer;
    };
    if info.is_null() {
        set_last_error("info pointer is null");
        return ParleyStatus::NullPointer;
    }
    let Some(style) = layout.layout.styles().get(style_index) else {
        set_last_error("style index out of bounds");
        return ParleyStatus::IndexOutOfBounds;
    };
    let mut output = ParleyStyleInfo::default();
    output.brush = color_from_brush(style.brush);
    if let Some(underline) = &style.underline {
        output.underline = true;
        output.underline_offset = underline.offset.unwrap_or_default();
        output.underline_size = underline.size.unwrap_or_default();
        output.underline_brush = color_from_brush(underline.brush);
    }
    if let Some(strike) = &style.strikethrough {
        output.strikethrough = true;
        output.strikethrough_offset = strike.offset.unwrap_or_default();
        output.strikethrough_size = strike.size.unwrap_or_default();
        output.strikethrough_brush = color_from_brush(strike.brush);
    }
    unsafe { *info = output };
    ParleyStatus::Success
}
fn brush_from_color(color: ParleyColor) -> BrushColor {
    [color.r, color.g, color.b, color.a]
}

fn color_from_brush(brush: BrushColor) -> ParleyColor {
    ParleyColor {
        r: brush[0],
        g: brush[1],
        b: brush[2],
        a: brush[3],
    }
}

fn convert_overflow_wrap_mode(mode: ParleyOverflowWrapMode) -> Option<OverflowWrap> {
    match mode {
        ParleyOverflowWrapMode::Normal => Some(OverflowWrap::Normal),
        ParleyOverflowWrapMode::Anywhere => Some(OverflowWrap::Anywhere),
        ParleyOverflowWrapMode::BreakWord => Some(OverflowWrap::BreakWord),
    }
}

fn alignment_from_kind(kind: ParleyAlignmentKind) -> Option<Alignment> {
    let alignment = match kind {
        ParleyAlignmentKind::Start => Alignment::Start,
        ParleyAlignmentKind::End => Alignment::End,
        ParleyAlignmentKind::Left => Alignment::Left,
        ParleyAlignmentKind::Center => Alignment::Center,
        ParleyAlignmentKind::Right => Alignment::Right,
        ParleyAlignmentKind::Justify => Alignment::Justify,
    };
    Some(alignment)
}

fn break_reason_to_enum(reason: BreakReason) -> ParleyBreakReason {
    match reason {
        BreakReason::None => ParleyBreakReason::None,
        BreakReason::Regular => ParleyBreakReason::Regular,
        BreakReason::Explicit => ParleyBreakReason::Explicit,
        BreakReason::Emergency => ParleyBreakReason::Emergency,
    }
}

fn convert_line_height(kind: ParleyLineHeightKind, value: f32) -> LineHeight {
    match kind {
        ParleyLineHeightKind::MetricsRelative => LineHeight::MetricsRelative(value),
        ParleyLineHeightKind::FontSizeRelative => LineHeight::FontSizeRelative(value),
        ParleyLineHeightKind::Absolute => LineHeight::Absolute(value),
    }
}

fn convert_style_property(
    property: &ParleyStyleProperty,
) -> Result<StyleProperty<'static, BrushColor>, ParleyStatus> {
    let result = match property.kind {
        ParleyStylePropertyKind::FontStack => {
            let stack = unsafe { str_from_raw(property.string_ptr, property.string_len)? };
            StyleProperty::FontStack(FontStack::Source(Cow::Owned(stack.to_owned())))
        }
        ParleyStylePropertyKind::FontSize => StyleProperty::FontSize(property.value_f32),
        ParleyStylePropertyKind::FontWeight => {
            StyleProperty::FontWeight(FontWeight::new(property.value_f32))
        }
        ParleyStylePropertyKind::FontStyle => {
            let angle = if property.font_style_angle.abs() > f32::EPSILON {
                Some(property.font_style_angle)
            } else {
                None
            };
            let style = match property.font_style {
                ParleyFontStyle::Normal => FontStyle::Normal,
                ParleyFontStyle::Italic => FontStyle::Italic,
                ParleyFontStyle::Oblique => FontStyle::Oblique(angle),
            };
            StyleProperty::FontStyle(style)
        }
        ParleyStylePropertyKind::FontWidth => {
            if property.value_f32 <= 0.0 {
                set_last_error("font width ratio must be greater than zero");
                return Err(ParleyStatus::InvalidArgument);
            }
            StyleProperty::FontWidth(FontWidth::from_ratio(property.value_f32))
        }
        ParleyStylePropertyKind::Brush => StyleProperty::Brush(brush_from_color(property.color)),
        ParleyStylePropertyKind::LineHeight => StyleProperty::LineHeight(convert_line_height(
            property.line_height_kind,
            property.value_f32,
        )),
        ParleyStylePropertyKind::LetterSpacing => StyleProperty::LetterSpacing(property.value_f32),
        ParleyStylePropertyKind::WordSpacing => StyleProperty::WordSpacing(property.value_f32),
        ParleyStylePropertyKind::Locale => {
            set_last_error("Locale property is not supported");
            return Err(ParleyStatus::InvalidArgument);
        }
        ParleyStylePropertyKind::Underline => StyleProperty::Underline(property.value_bool),
        ParleyStylePropertyKind::UnderlineOffset => {
            let value = if property.value_bool {
                Some(property.value_f32)
            } else {
                None
            };
            StyleProperty::UnderlineOffset(value)
        }
        ParleyStylePropertyKind::UnderlineSize => {
            let value = if property.value_bool {
                Some(property.value_f32)
            } else {
                None
            };
            StyleProperty::UnderlineSize(value)
        }
        ParleyStylePropertyKind::UnderlineBrush => {
            let value = if property.value_bool {
                Some(brush_from_color(property.color))
            } else {
                None
            };
            StyleProperty::UnderlineBrush(value)
        }
        ParleyStylePropertyKind::Strikethrough => StyleProperty::Strikethrough(property.value_bool),
        ParleyStylePropertyKind::StrikethroughOffset => {
            let value = if property.value_bool {
                Some(property.value_f32)
            } else {
                None
            };
            StyleProperty::StrikethroughOffset(value)
        }
        ParleyStylePropertyKind::StrikethroughSize => {
            let value = if property.value_bool {
                Some(property.value_f32)
            } else {
                None
            };
            StyleProperty::StrikethroughSize(value)
        }
        ParleyStylePropertyKind::StrikethroughBrush => {
            let value = if property.value_bool {
                Some(brush_from_color(property.color))
            } else {
                None
            };
            StyleProperty::StrikethroughBrush(value)
        }
        ParleyStylePropertyKind::OverflowWrap => {
            let mode = match property.value_i32 {
                0 => ParleyOverflowWrapMode::Normal,
                1 => ParleyOverflowWrapMode::Anywhere,
                2 => ParleyOverflowWrapMode::BreakWord,
                _ => {
                    set_last_error("invalid overflow wrap value");
                    return Err(ParleyStatus::InvalidArgument);
                }
            };
            let wrap = convert_overflow_wrap_mode(mode).unwrap();
            StyleProperty::OverflowWrap(wrap)
        }
    };
    Ok(result)
}

fn span_range(span: &ParleyStyleSpan, text_len: usize) -> Result<Range<usize>, ParleyStatus> {
    if span.range_end > text_len {
        set_last_error("style span end exceeds text length");
        return Err(ParleyStatus::InvalidArgument);
    }
    if span.range_start > span.range_end {
        set_last_error("style span start is greater than end");
        return Err(ParleyStatus::InvalidArgument);
    }
    Ok(span.range_start..span.range_end)
}

fn glyph_run_count(line: Line<'_, BrushColor>) -> usize {
    line.items()
        .filter(|item| matches!(item, PositionedLayoutItem::GlyphRun(_)))
        .count()
}

fn nth_glyph_run<'a>(line: Line<'a, BrushColor>, index: usize) -> Option<GlyphRun<'a, BrushColor>> {
    line.items()
        .filter_map(|item| match item {
            PositionedLayoutItem::GlyphRun(run) => Some(run),
            _ => None,
        })
        .nth(index)
}

fn build_glyph_run_info(
    run: &GlyphRun<'_, BrushColor>,
) -> Result<ParleyGlyphRunInfo, ParleyStatus> {
    let mut info = ParleyGlyphRunInfo::default();
    let mut glyph_iter = run.glyphs();
    if let Some(first) = glyph_iter.next() {
        if first.style_index() > u16::MAX as usize {
            set_last_error("style index exceeds supported range");
            return Err(ParleyStatus::InvalidArgument);
        }
        info.style_index = first.style_index() as u16;
        info.glyph_count = 1 + glyph_iter.count();
    } else {
        info.style_index = 0;
        info.glyph_count = 0;
    }

    let font = run.run().font();
    info.font_blob_id = font.data.id();
    info.font_index = font.index;
    let font_data = font.data.data();
    info.font_data = font_data.as_ptr();
    info.font_data_len = font_data.len();
    info.font_size = run.run().font_size();
    let metrics = run.run().metrics();
    info.ascent = metrics.ascent;
    info.descent = metrics.descent;
    info.leading = metrics.leading;
    info.baseline = run.baseline();
    info.offset = run.offset();
    info.advance = run.advance();
    info.is_rtl = run.run().is_rtl();
    Ok(info)
}

fn inline_box_count(line: Line<'_, BrushColor>) -> usize {
    line.items()
        .filter(|item| matches!(item, PositionedLayoutItem::InlineBox(_)))
        .count()
}

fn nth_inline_box(line: Line<'_, BrushColor>, index: usize) -> Option<PositionedInlineBox> {
    line.items()
        .filter_map(|item| match item {
            PositionedLayoutItem::InlineBox(inline_box) => Some(inline_box),
            _ => None,
        })
        .nth(index)
}
