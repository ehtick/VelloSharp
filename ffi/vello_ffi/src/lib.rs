#![cfg_attr(not(test), deny(clippy::all))]
#![allow(clippy::missing_safety_doc)]
#![allow(missing_docs)]
#![allow(missing_debug_implementations)]

use std::{
    borrow::Cow,
    cell::RefCell,
    convert::TryFrom,
    ffi::{CStr, CString, c_char, c_ulong, c_void},
    io::Cursor,
    mem,
    mem::ManuallyDrop,
    num::{NonZeroIsize, NonZeroU64, NonZeroUsize},
    ptr::NonNull,
    slice,
    sync::Mutex,
};

use fontique::{
    Attributes as FontiqueAttributes, FontStyle as FontiqueStyle, FontWeight as FontiqueWeight,
    FontWidth as FontiqueWidth, GenericFamily as FontiqueGenericFamily,
};
use futures_intrusive::channel::shared::oneshot_channel;
use harfrust::{
    Direction as HrDirection, FontRef as HrFontRef, ShaperData as HrShaperData,
    UnicodeBuffer as HrUnicodeBuffer,
};
use image::{ColorType as ImageColorType, ImageDecoder, codecs::ico::IcoDecoder};
use once_cell::sync::Lazy;
use parley::FontContext;
use png::{BitDepth, ColorType, Compression, Decoder, Encoder};
use raw_window_handle::{
    AppKitDisplayHandle, AppKitWindowHandle, RawDisplayHandle, RawWindowHandle,
    WaylandDisplayHandle, WaylandWindowHandle, Win32WindowHandle, WindowsDisplayHandle,
    XlibDisplayHandle, XlibWindowHandle,
};

#[cfg(target_os = "android")]
use raw_window_handle::{AndroidDisplayHandle, AndroidNdkWindowHandle};
use skrifa::raw::TableProvider;
use skrifa::raw::{self, ReadError, tables::mvar::tags as MvarTag};
use skrifa::{
    FontRef, GlyphId as SkrifaGlyphId, MetadataProvider, Tag as SkrifaTag,
    instance::{Location, NormalizedCoord, Size as SkrifaSize},
    metrics::GlyphMetrics as SkrifaGlyphMetrics,
    prelude::LocationRef as SkrifaLocationRef,
    setting::VariationSetting,
};
use linesweeper::{BinaryOp as LinesweeperBinaryOp, FillRule as LinesweeperFillRule};
use kurbo_v11::{BezPath as BezPathV11, PathEl as PathElV11, Point as PointV11};
use swash::{
    FontRef as SwashFontRef, GlyphId as SwashGlyphId,
    scale::{ScaleContext, outline::Outline},
    zeno::Verb,
};
use velato::{self, Composition as VelatoComposition, Renderer as VelatoRenderer};
use vello::kurbo::{Affine, BezPath, Cap, Join, PathEl, Point, Rect, Shape, Stroke, StrokeOpts, Vec2};
use vello::peniko::{
    self, BlendMode, Blob, Brush, BrushRef, Color, ColorStop, ColorStops, Extend, Fill, FontData,
    Gradient, ImageAlphaType, ImageBrush, ImageData, ImageFormat, ImageQuality,
};
use vello::{
    AaConfig, AaSupport, Glyph, RenderParams, Renderer, RendererOptions, Scene,
    util::{RenderContext, RenderSurface},
};
use vello_svg::{self, usvg};
use wgpu::{
    Adapter, Backend, Buffer, CompositeAlphaMode, Device, Dx12Compiler, Features, Instance,
    InstanceDescriptor, InstanceFlags, Limits, PipelineCache, PowerPreference, Queue,
    RequestAdapterOptions, SurfaceConfiguration, SurfaceError, SurfaceTargetUnsafe, SurfaceTexture,
    TextureAspect, TextureFormat, TextureUsages, TextureView, TextureViewDescriptor,
    TextureViewDimension, util::TextureBlitter,
};

#[cfg(target_os = "windows")]
#[cfg(target_os = "windows")]
mod windows_shared_texture;

#[cfg(feature = "trace-paths")]
macro_rules! trace_path {
    ($($arg:tt)*) => {
        println!($($arg)*);
    };
}

#[cfg(not(feature = "trace-paths"))]
macro_rules! trace_path {
    ($($arg:tt)*) => {};
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloStatus {
    Success = 0,
    NullPointer = 1,
    InvalidArgument = 2,
    DeviceCreationFailed = 3,
    RenderError = 4,
    MapFailed = 5,
    Unsupported = 6,
    Timeout = 7,
}

thread_local! {
    static LAST_ERROR: RefCell<Option<CString>> = const { RefCell::new(None) };
}

static PARLEY_FONT_CONTEXT: Lazy<Mutex<FontContext>> = Lazy::new(|| Mutex::new(FontContext::new()));

fn clear_last_error() {
    LAST_ERROR.with(|slot| slot.borrow_mut().take());
}

fn set_last_error(msg: impl Into<String>) {
    let msg = msg.into();
    let cstr = CString::new(msg).unwrap_or_else(|_| CString::new("invalid error message").unwrap());
    LAST_ERROR.with(|slot| *slot.borrow_mut() = Some(cstr));
}

unsafe fn slice_from_raw<'a, T>(ptr: *const T, len: usize) -> Result<&'a [T], VelloStatus> {
    if len == 0 {
        Ok(&[])
    } else if ptr.is_null() {
        Err(VelloStatus::NullPointer)
    } else {
        Ok(unsafe { slice::from_raw_parts(ptr, len) })
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn vello_last_error_message() -> *const c_char {
    LAST_ERROR.with(|slot| match slot.borrow().as_ref() {
        Some(cstr) => cstr.as_ptr(),
        None => std::ptr::null(),
    })
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloAffine {
    pub m11: f64,
    pub m12: f64,
    pub m21: f64,
    pub m22: f64,
    pub dx: f64,
    pub dy: f64,
}

impl From<VelloAffine> for Affine {
    fn from(value: VelloAffine) -> Self {
        Affine::new([
            value.m11, value.m12, value.m21, value.m22, value.dx, value.dy,
        ])
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloColor {
    pub r: f32,
    pub g: f32,
    pub b: f32,
    pub a: f32,
}

impl From<VelloColor> for Color {
    fn from(value: VelloColor) -> Self {
        Color::new([value.r, value.g, value.b, value.a])
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone)]
pub enum VelloFillRule {
    NonZero = 0,
    EvenOdd = 1,
}

impl From<VelloFillRule> for Fill {
    fn from(value: VelloFillRule) -> Self {
        match value {
            VelloFillRule::NonZero => Fill::NonZero,
            VelloFillRule::EvenOdd => Fill::EvenOdd,
        }
    }
}

impl From<VelloFillRule> for LinesweeperFillRule {
    fn from(value: VelloFillRule) -> Self {
        match value {
            VelloFillRule::NonZero => LinesweeperFillRule::NonZero,
            VelloFillRule::EvenOdd => LinesweeperFillRule::EvenOdd,
        }
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone)]
pub enum VelloPathVerb {
    MoveTo = 0,
    LineTo = 1,
    QuadTo = 2,
    CubicTo = 3,
    Close = 4,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloPathElement {
    pub verb: VelloPathVerb,
    pub _padding: i32,
    pub x0: f64,
    pub y0: f64,
    pub x1: f64,
    pub y1: f64,
    pub x2: f64,
    pub y2: f64,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone)]
pub enum VelloPathBooleanOp {
    Union = 0,
    Intersection = 1,
    Difference = 2,
    Xor = 3,
}

impl From<VelloPathBooleanOp> for LinesweeperBinaryOp {
    fn from(value: VelloPathBooleanOp) -> Self {
        match value {
            VelloPathBooleanOp::Union => LinesweeperBinaryOp::Union,
            VelloPathBooleanOp::Intersection => LinesweeperBinaryOp::Intersection,
            VelloPathBooleanOp::Difference => LinesweeperBinaryOp::Difference,
            VelloPathBooleanOp::Xor => LinesweeperBinaryOp::Xor,
        }
    }
}

pub struct VelloPathCommandListHandle {
    commands: Box<[VelloPathElement]>,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloPathCommandList {
    pub commands: *const VelloPathElement,
    pub command_count: usize,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloPoint {
    pub x: f64,
    pub y: f64,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloRect {
    pub x: f64,
    pub y: f64,
    pub width: f64,
    pub height: f64,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloTextStackProbe {
    pub parley_layout_height: f32,
    pub fontique_normal_weight: f32,
    pub swash_cache_entries: u32,
    pub skrifa_test_gid: u16,
}

#[unsafe(no_mangle)]
pub extern "C" fn vello_text_stack_probe(result: *mut VelloTextStackProbe) -> VelloStatus {
    if result.is_null() {
        return VelloStatus::NullPointer;
    }

    clear_last_error();

    let layout_height = parley::layout::Layout::<[u8; 4]>::new().height();
    let attributes = fontique::Attributes::new(
        fontique::FontWidth::NORMAL,
        fontique::FontStyle::Normal,
        fontique::FontWeight::NORMAL,
    );
    let weight_value = attributes.weight.value();

    let _swash_ctx = swash::scale::ScaleContext::with_max_entries(16);
    let glyph_id = skrifa::GlyphId::new(0);
    let glyph_value = glyph_id.to_u32() as u16;

    unsafe {
        *result = VelloTextStackProbe {
            parley_layout_height: layout_height,
            fontique_normal_weight: weight_value,
            swash_cache_entries: 16,
            skrifa_test_gid: glyph_value,
        };
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_string_destroy(ptr: *mut c_char) {
    if !ptr.is_null() {
        unsafe { drop(CString::from_raw(ptr)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_parley_font_handle_destroy(handle: *mut VelloParleyFontHandle) {
    if !handle.is_null() {
        unsafe { drop(Box::from_raw(handle)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_parley_string_array_destroy(handle: *mut VelloStringArrayHandle) {
    if !handle.is_null() {
        unsafe { drop(Box::from_raw(handle)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_parley_get_default_family() -> *mut c_char {
    let mut ctx = PARLEY_FONT_CONTEXT.lock().unwrap();
    let collection = &mut ctx.collection;

    let first_family = {
        let mut families = collection.generic_families(FontiqueGenericFamily::SansSerif);
        families.next()
    };

    if let Some(id) = first_family {
        if let Some(name) = collection.family_name(id) {
            if let Ok(cstr) = CString::new(name) {
                return cstr.into_raw();
            }
        }
    }

    CString::new("sans-serif").unwrap().into_raw()
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_parley_get_family_names(
    out_handle: *mut *mut VelloStringArrayHandle,
    out_array: *mut VelloStringArray,
) -> VelloStatus {
    if out_handle.is_null() || out_array.is_null() {
        return VelloStatus::NullPointer;
    }

    let mut ctx = PARLEY_FONT_CONTEXT.lock().unwrap();
    let collection = &mut ctx.collection;

    let mut strings = Vec::new();
    for name in collection.family_names() {
        if let Ok(cstr) = CString::new(name) {
            strings.push(cstr);
        }
    }

    let mut pointers = Vec::with_capacity(strings.len());
    for s in &strings {
        pointers.push(s.as_ptr());
    }

    let handle = Box::new(VelloStringArrayHandle {
        _strings: strings,
        pointers,
    });
    let array = VelloStringArray {
        items: handle.pointers.as_ptr(),
        count: handle.pointers.len(),
    };

    unsafe {
        *out_array = array;
        *out_handle = Box::into_raw(handle);
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_parley_match_character(
    codepoint: u32,
    weight: f32,
    stretch: f32,
    style: i32,
    family_name: *const c_char,
    _locale: *const c_char,
    out_handle: *mut *mut VelloParleyFontHandle,
    out_info: *mut VelloParleyFontInfo,
) -> VelloStatus {
    if out_handle.is_null() || out_info.is_null() {
        return VelloStatus::NullPointer;
    }

    unsafe {
        *out_handle = std::ptr::null_mut();
        *out_info = VelloParleyFontInfo {
            family_name: std::ptr::null(),
            data: std::ptr::null(),
            length: 0,
            index: 0,
            weight: 0.0,
            stretch: 1.0,
            style: 0,
            is_monospace: false,
        };
    }

    let family = if family_name.is_null() {
        None
    } else {
        match unsafe { CStr::from_ptr(family_name) }.to_str() {
            Ok(value) if !value.is_empty() => Some(value.to_owned()),
            _ => None,
        }
    };

    let attrs = FontiqueAttributes::new(
        fontique_width_from_ratio(stretch),
        fontique_style_from_i32(style),
        fontique_weight_from_value(weight),
    );

    let mut matched: Option<MatchedFont> = None;

    let mut ctx = PARLEY_FONT_CONTEXT.lock().unwrap();
    {
        let FontContext {
            collection,
            source_cache,
        } = &mut *ctx;
        let mut query = collection.query(source_cache);

        let mut families = Vec::new();
        if let Some(ref name) = family {
            families.push(fontique::QueryFamily::Named(name));
        }
        families.push(fontique::QueryFamily::Generic(
            FontiqueGenericFamily::SansSerif,
        ));
        families.push(fontique::QueryFamily::Generic(FontiqueGenericFamily::Emoji));
        query.set_families(families);
        query.set_attributes(attrs);

        query.matches_with(|font| {
            let font_data = font.blob.as_ref();
            if let Ok(font_ref) = FontRef::from_index(font_data, font.index) {
                if font_ref.charmap().map(codepoint).is_some() {
                    matched = Some(MatchedFont {
                        blob: font.blob.clone(),
                        index: font.index,
                        family_id: font.family.0,
                        width: attrs.width,
                        style: attrs.style,
                        weight: attrs.weight,
                    });
                    return fontique::QueryStatus::Stop;
                }
            }
            fontique::QueryStatus::Continue
        });
    }

    let Some(matched) = matched else {
        return VelloStatus::Unsupported;
    };

    let family_name = ctx
        .collection
        .family_name(matched.family_id)
        .unwrap_or("unknown");

    let handle_ptr = match create_parley_font_handle(
        family_name,
        matched.width,
        matched.style,
        matched.weight,
        matched.blob,
        matched.index,
    ) {
        Ok(handle) => handle,
        Err(status) => return status,
    };

    unsafe {
        *out_handle = handle_ptr;
        *out_info = (*handle_ptr).info;
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_parley_load_typeface(
    family_name: *const c_char,
    weight: f32,
    stretch: f32,
    style: i32,
    out_handle: *mut *mut VelloParleyFontHandle,
    out_info: *mut VelloParleyFontInfo,
) -> VelloStatus {
    if family_name.is_null() || out_handle.is_null() || out_info.is_null() {
        return VelloStatus::NullPointer;
    }

    unsafe {
        *out_handle = std::ptr::null_mut();
        *out_info = VelloParleyFontInfo {
            family_name: std::ptr::null(),
            data: std::ptr::null(),
            length: 0,
            index: 0,
            weight: 0.0,
            stretch: 1.0,
            style: 0,
            is_monospace: false,
        };
    }

    let family = match unsafe { CStr::from_ptr(family_name) }.to_str() {
        Ok(name) if !name.is_empty() => name.to_owned(),
        _ => {
            set_last_error("Invalid family name");
            return VelloStatus::InvalidArgument;
        }
    };

    let attrs = FontiqueAttributes::new(
        fontique_width_from_ratio(stretch),
        fontique_style_from_i32(style),
        fontique_weight_from_value(weight),
    );

    let mut ctx = PARLEY_FONT_CONTEXT.lock().unwrap();

    let family_info = match ctx.collection.family_by_name(&family) {
        Some(info) => info,
        None => return VelloStatus::Unsupported,
    };

    let mut font_index = family_info.match_index(attrs.width, attrs.style, attrs.weight, true);
    if font_index.is_none() {
        font_index = Some(family_info.default_font_index());
    }
    let Some(font_index) = font_index else {
        return VelloStatus::Unsupported;
    };

    let font = match family_info.fonts().get(font_index) {
        Some(font) => font.clone(),
        None => {
            set_last_error("Font index out of range");
            return VelloStatus::InvalidArgument;
        }
    };

    let blob = match font.load(Some(&mut ctx.source_cache)) {
        Some(blob) => blob,
        None => {
            set_last_error("Failed to load font data");
            return VelloStatus::InvalidArgument;
        }
    };

    let handle_ptr = match create_parley_font_handle(
        &family,
        attrs.width,
        attrs.style,
        attrs.weight,
        blob,
        font.index(),
    ) {
        Ok(handle) => handle,
        Err(status) => return status,
    };

    unsafe {
        *out_handle = handle_ptr;
        *out_info = (*handle_ptr).info;
    }

    VelloStatus::Success
}

fn build_bez_path(elements: &[VelloPathElement]) -> Result<BezPath, &'static str> {
    if elements.is_empty() {
        return Err("path is empty");
    }
    let mut path = BezPath::with_capacity(elements.len());
    let mut has_move = false;
    for (idx, elem) in elements.iter().enumerate() {
        if let Err(err) = push_path_element(&mut path, elem, idx, has_move) {
            return Err(err);
        }
        if matches!(elem.verb, VelloPathVerb::MoveTo) {
            has_move = true;
        }
    }

    if !has_move {
        return Err("path must contain a MoveTo");
    }
    Ok(path)
}

fn build_bez_path_v11(elements: &[VelloPathElement]) -> Result<BezPathV11, &'static str> {
    if elements.is_empty() {
        return Err("path is empty");
    }
    let mut path = BezPathV11::with_capacity(elements.len());
    let mut has_move = false;
    for (idx, elem) in elements.iter().enumerate() {
        match elem.verb {
            VelloPathVerb::MoveTo => {
                path.move_to(PointV11::new(elem.x0, elem.y0));
                has_move = true;
            }
            VelloPathVerb::LineTo => {
                if !has_move {
                    return Err("path must start with MoveTo");
                }
                path.line_to(PointV11::new(elem.x0, elem.y0));
            }
            VelloPathVerb::QuadTo => {
                if !has_move {
                    return Err("path must start with MoveTo");
                }
                path.quad_to(
                    PointV11::new(elem.x0, elem.y0),
                    PointV11::new(elem.x1, elem.y1),
                );
            }
            VelloPathVerb::CubicTo => {
                if !has_move {
                    return Err("path must start with MoveTo");
                }
                path.curve_to(
                    PointV11::new(elem.x0, elem.y0),
                    PointV11::new(elem.x1, elem.y1),
                    PointV11::new(elem.x2, elem.y2),
                );
            }
            VelloPathVerb::Close => {
                if idx == 0 {
                    return Err("path cannot begin with Close");
                }
                path.close_path();
            }
        }
    }
    if !has_move {
        return Err("path must contain a MoveTo");
    }
    Ok(path)
}

fn push_path_element(
    path: &mut BezPath,
    elem: &VelloPathElement,
    idx: usize,
    has_move: bool,
) -> Result<(), &'static str> {
    match elem.verb {
        VelloPathVerb::MoveTo => {
            path.move_to((elem.x0, elem.y0));
        }
        VelloPathVerb::LineTo => {
            if !has_move {
                return Err("path must start with MoveTo");
            }
            path.line_to((elem.x0, elem.y0));
        }
        VelloPathVerb::QuadTo => {
            if !has_move {
                return Err("path must start with MoveTo");
            }
            path.quad_to((elem.x0, elem.y0), (elem.x1, elem.y1));
        }
        VelloPathVerb::CubicTo => {
            if !has_move {
                return Err("path must start with MoveTo");
            }
            path.curve_to((elem.x0, elem.y0), (elem.x1, elem.y1), (elem.x2, elem.y2));
        }
        VelloPathVerb::Close => {
            if idx == 0 {
                return Err("path cannot begin with Close");
            }
            path.close_path();
        }
    }
    Ok(())
}

fn vello_path_element_from_path_el(element: PathEl) -> VelloPathElement {
    match element {
        PathEl::MoveTo(p0) => VelloPathElement {
            verb: VelloPathVerb::MoveTo,
            _padding: 0,
            x0: p0.x,
            y0: p0.y,
            x1: 0.0,
            y1: 0.0,
            x2: 0.0,
            y2: 0.0,
        },
        PathEl::LineTo(p0) => VelloPathElement {
            verb: VelloPathVerb::LineTo,
            _padding: 0,
            x0: p0.x,
            y0: p0.y,
            x1: 0.0,
            y1: 0.0,
            x2: 0.0,
            y2: 0.0,
        },
        PathEl::QuadTo(p0, p1) => VelloPathElement {
            verb: VelloPathVerb::QuadTo,
            _padding: 0,
            x0: p0.x,
            y0: p0.y,
            x1: p1.x,
            y1: p1.y,
            x2: 0.0,
            y2: 0.0,
        },
        PathEl::CurveTo(p0, p1, p2) => VelloPathElement {
            verb: VelloPathVerb::CubicTo,
            _padding: 0,
            x0: p0.x,
            y0: p0.y,
            x1: p1.x,
            y1: p1.y,
            x2: p2.x,
            y2: p2.y,
        },
        PathEl::ClosePath => VelloPathElement {
            verb: VelloPathVerb::Close,
            _padding: 0,
            x0: 0.0,
            y0: 0.0,
            x1: 0.0,
            y1: 0.0,
            x2: 0.0,
            y2: 0.0,
        },
    }
}

fn append_bez_path_elements(dest: &mut Vec<VelloPathElement>, path: &BezPath) {
    let elements = path.elements();
    for element in elements {
        dest.push(vello_path_element_from_path_el(*element));
    }
    if let Some(last) = elements.last() {
        if !matches!(last, PathEl::ClosePath) {
            dest.push(VelloPathElement {
                verb: VelloPathVerb::Close,
                _padding: 0,
                x0: 0.0,
                y0: 0.0,
                x1: 0.0,
                y1: 0.0,
                x2: 0.0,
                y2: 0.0,
            });
        }
    }
}

fn vello_path_element_from_path_el_v11(element: PathElV11) -> VelloPathElement {
    match element {
        PathElV11::MoveTo(p0) => VelloPathElement {
            verb: VelloPathVerb::MoveTo,
            _padding: 0,
            x0: p0.x,
            y0: p0.y,
            x1: 0.0,
            y1: 0.0,
            x2: 0.0,
            y2: 0.0,
        },
        PathElV11::LineTo(p0) => VelloPathElement {
            verb: VelloPathVerb::LineTo,
            _padding: 0,
            x0: p0.x,
            y0: p0.y,
            x1: 0.0,
            y1: 0.0,
            x2: 0.0,
            y2: 0.0,
        },
        PathElV11::QuadTo(p0, p1) => VelloPathElement {
            verb: VelloPathVerb::QuadTo,
            _padding: 0,
            x0: p0.x,
            y0: p0.y,
            x1: p1.x,
            y1: p1.y,
            x2: 0.0,
            y2: 0.0,
        },
        PathElV11::CurveTo(p0, p1, p2) => VelloPathElement {
            verb: VelloPathVerb::CubicTo,
            _padding: 0,
            x0: p0.x,
            y0: p0.y,
            x1: p1.x,
            y1: p1.y,
            x2: p2.x,
            y2: p2.y,
        },
        PathElV11::ClosePath => VelloPathElement {
            verb: VelloPathVerb::Close,
            _padding: 0,
            x0: 0.0,
            y0: 0.0,
            x1: 0.0,
            y1: 0.0,
            x2: 0.0,
            y2: 0.0,
        },
    }
}

fn append_bez_path_elements_v11(dest: &mut Vec<VelloPathElement>, path: &BezPathV11) {
    let elements = path.elements();
    for element in elements {
        dest.push(vello_path_element_from_path_el_v11(*element));
    }
    if let Some(last) = elements.last() {
        if !matches!(last, PathElV11::ClosePath) {
            dest.push(VelloPathElement {
                verb: VelloPathVerb::Close,
                _padding: 0,
                x0: 0.0,
                y0: 0.0,
                x1: 0.0,
                y1: 0.0,
                x2: 0.0,
                y2: 0.0,
            });
        }
    }
}

fn outline_to_path_elements(outline: &Outline) -> Vec<VelloPathElement> {
    let mut commands = Vec::new();
    for layer_index in 0..outline.len() {
        let Some(layer) = outline.get(layer_index) else {
            continue;
        };
        let points = layer.points();
        let verbs = layer.verbs();
        let mut point_index = 0usize;
        for verb in verbs {
            match verb {
                Verb::MoveTo => {
                    if let Some(point) = points.get(point_index) {
                        commands.push(VelloPathElement {
                            verb: VelloPathVerb::MoveTo,
                            _padding: 0,
                            x0: point.x as f64,
                            y0: point.y as f64,
                            x1: 0.0,
                            y1: 0.0,
                            x2: 0.0,
                            y2: 0.0,
                        });
                        point_index += 1;
                    }
                }
                Verb::LineTo => {
                    if let Some(point) = points.get(point_index) {
                        commands.push(VelloPathElement {
                            verb: VelloPathVerb::LineTo,
                            _padding: 0,
                            x0: point.x as f64,
                            y0: point.y as f64,
                            x1: 0.0,
                            y1: 0.0,
                            x2: 0.0,
                            y2: 0.0,
                        });
                        point_index += 1;
                    }
                }
                Verb::QuadTo => {
                    if let (Some(ctrl), Some(end)) =
                        (points.get(point_index), points.get(point_index + 1))
                    {
                        commands.push(VelloPathElement {
                            verb: VelloPathVerb::QuadTo,
                            _padding: 0,
                            x0: ctrl.x as f64,
                            y0: ctrl.y as f64,
                            x1: end.x as f64,
                            y1: end.y as f64,
                            x2: 0.0,
                            y2: 0.0,
                        });
                        point_index += 2;
                    } else {
                        point_index = points.len();
                    }
                }
                Verb::CurveTo => {
                    if let (Some(c1), Some(c2), Some(end)) = (
                        points.get(point_index),
                        points.get(point_index + 1),
                        points.get(point_index + 2),
                    ) {
                        commands.push(VelloPathElement {
                            verb: VelloPathVerb::CubicTo,
                            _padding: 0,
                            x0: c1.x as f64,
                            y0: c1.y as f64,
                            x1: c2.x as f64,
                            y1: c2.y as f64,
                            x2: end.x as f64,
                            y2: end.y as f64,
                        });
                        point_index += 3;
                    } else {
                        point_index = points.len();
                    }
                }
                Verb::Close => commands.push(VelloPathElement {
                    verb: VelloPathVerb::Close,
                    _padding: 0,
                    x0: 0.0,
                    y0: 0.0,
                    x1: 0.0,
                    y1: 0.0,
                    x2: 0.0,
                    y2: 0.0,
                }),
            }
        }
    }
    commands
}

fn convert_gradient_stops(stops: &[VelloGradientStop]) -> Result<ColorStops, VelloStatus> {
    if stops.is_empty() {
        return Err(VelloStatus::InvalidArgument);
    }
    let mut converted = Vec::with_capacity(stops.len());
    for stop in stops {
        if !(0.0..=1.0).contains(&stop.offset) || !stop.offset.is_finite() {
            return Err(VelloStatus::InvalidArgument);
        }
        let color: Color = (*stop).color.into();
        converted.push(ColorStop::from((stop.offset, color)));
    }
    Ok(converted.as_slice().into())
}

fn gradient_from_linear(linear: &VelloLinearGradient) -> Result<Gradient, VelloStatus> {
    let stops = unsafe { slice_from_raw(linear.stops, linear.stop_count)? };
    let mut gradient = Gradient::new_linear(
        (linear.start.x, linear.start.y),
        (linear.end.x, linear.end.y),
    );
    gradient.extend = linear.extend.into();
    gradient.stops = convert_gradient_stops(stops)?;
    Ok(gradient)
}

fn gradient_from_radial(radial: &VelloRadialGradient) -> Result<Gradient, VelloStatus> {
    if radial.start_radius < 0.0 || radial.end_radius < 0.0 {
        return Err(VelloStatus::InvalidArgument);
    }
    let stops = unsafe { slice_from_raw(radial.stops, radial.stop_count)? };
    let mut gradient = Gradient::new_two_point_radial(
        (radial.start_center.x, radial.start_center.y),
        radial.start_radius,
        (radial.end_center.x, radial.end_center.y),
        radial.end_radius,
    );
    gradient.extend = radial.extend.into();
    gradient.stops = convert_gradient_stops(stops)?;
    Ok(gradient)
}

fn gradient_from_sweep(sweep: &VelloSweepGradient) -> Result<Gradient, VelloStatus> {
    let stops = unsafe { slice_from_raw(sweep.stops, sweep.stop_count)? };
    let mut gradient = Gradient::new_sweep(
        (sweep.center.x, sweep.center.y),
        sweep.start_angle,
        sweep.end_angle,
    );
    gradient.extend = sweep.extend.into();
    gradient.stops = convert_gradient_stops(stops)?;
    Ok(gradient)
}

fn image_brush_from_params(params: &VelloImageBrushParams) -> Result<ImageBrush, VelloStatus> {
    let Some(handle) = (unsafe { params.image.as_ref() }) else {
        return Err(VelloStatus::NullPointer);
    };
    let mut brush = ImageBrush::new(handle.image.clone());
    brush.sampler.x_extend = params.x_extend.into();
    brush.sampler.y_extend = params.y_extend.into();
    brush.sampler.quality = params.quality.into();
    brush.sampler.alpha = params.alpha;
    Ok(brush)
}

fn brush_from_ffi(brush: &VelloBrush) -> Result<Brush, VelloStatus> {
    match brush.kind {
        VelloBrushKind::Solid => Ok(Brush::Solid(brush.solid.into())),
        VelloBrushKind::LinearGradient => Ok(Brush::Gradient(gradient_from_linear(&brush.linear)?)),
        VelloBrushKind::RadialGradient => Ok(Brush::Gradient(gradient_from_radial(&brush.radial)?)),
        VelloBrushKind::SweepGradient => Ok(Brush::Gradient(gradient_from_sweep(&brush.sweep)?)),
        VelloBrushKind::Image => Ok(Brush::Image(image_brush_from_params(&brush.image)?)),
    }
}

fn blend_mode_from_params(params: &VelloLayerParams) -> BlendMode {
    BlendMode::new(params.mix.into(), params.compose.into())
}

fn renderer_options_from_ffi(options: &VelloRendererOptions) -> RendererOptions {
    let mut support = AaSupport {
        area: options.support_area,
        msaa8: options.support_msaa8,
        msaa16: options.support_msaa16,
    };
    if !support.area && !support.msaa8 && !support.msaa16 {
        support = AaSupport::area_only();
    }
    let init_threads = if options.init_threads <= 0 {
        None
    } else {
        NonZeroUsize::new(options.init_threads as usize)
    };
    let pipeline_cache = if options.pipeline_cache.is_null() {
        None
    } else {
        unsafe {
            options
                .pipeline_cache
                .as_ref()
                .map(|handle| handle.cache.clone())
        }
    };
    RendererOptions {
        use_cpu: options.use_cpu,
        antialiasing_support: support,
        num_init_threads: init_threads,
        pipeline_cache,
    }
}

fn optional_affine(ptr: *const VelloAffine) -> Result<Option<Affine>, VelloStatus> {
    if ptr.is_null() {
        Ok(None)
    } else {
        let affine = unsafe { (*ptr).into() };
        Ok(Some(affine))
    }
}

fn image_format_from_render(format: VelloRenderFormat) -> ImageFormat {
    match format {
        VelloRenderFormat::Rgba8 => ImageFormat::Rgba8,
        VelloRenderFormat::Bgra8 => ImageFormat::Bgra8,
    }
}

fn render_format_from_image(format: ImageFormat) -> VelloRenderFormat {
    match format {
        ImageFormat::Rgba8 => VelloRenderFormat::Rgba8,
        ImageFormat::Bgra8 => VelloRenderFormat::Bgra8,
        _ => VelloRenderFormat::Rgba8,
    }
}

fn convert_bgra_to_rgba(data: &[u8]) -> Vec<u8> {
    let mut converted = data.to_vec();
    for pixel in converted.chunks_exact_mut(4) {
        pixel.swap(0, 2);
    }
    converted
}

fn png_compression_from_level(level: u8) -> Compression {
    match level {
        0..=2 => Compression::Fast,
        3..=6 => Compression::default(),
        _ => Compression::Best,
    }
}

fn resize_image_data(
    src: &[u8],
    src_width: u32,
    src_height: u32,
    src_stride: usize,
    dst_width: u32,
    dst_height: u32,
    mode: VelloImageQualityMode,
) -> Vec<u8> {
    if dst_width == 0 || dst_height == 0 {
        return Vec::new();
    }

    if src_width == dst_width && src_height == dst_height {
        let row_bytes = (dst_width as usize) * 4;
        let mut copy = Vec::with_capacity(row_bytes * dst_height as usize);
        for y in 0..dst_height as usize {
            let start = y * src_stride;
            copy.extend_from_slice(&src[start..start + row_bytes]);
        }
        return copy;
    }

    let dst_row_bytes = (dst_width as usize) * 4;
    let mut result = vec![0u8; dst_row_bytes * dst_height as usize];

    let use_bilinear = matches!(
        mode,
        VelloImageQualityMode::Medium | VelloImageQualityMode::High
    );

    if !use_bilinear {
        // Nearest neighbour
        let x_ratio = src_width as f32 / dst_width as f32;
        let y_ratio = src_height as f32 / dst_height as f32;
        for y in 0..dst_height as usize {
            let src_y = (y_ratio * y as f32)
                .floor()
                .clamp(0.0, src_height as f32 - 1.0) as usize;
            let src_row = &src[src_y * src_stride..];
            let dst_row = &mut result[y * dst_row_bytes..][..dst_row_bytes];
            for x in 0..dst_width as usize {
                let src_x = (x_ratio * x as f32)
                    .floor()
                    .clamp(0.0, src_width as f32 - 1.0) as usize;
                let src_index = src_x * 4;
                let dst_index = x * 4;
                dst_row[dst_index..dst_index + 4]
                    .copy_from_slice(&src_row[src_index..src_index + 4]);
            }
        }
        return result;
    }

    let width_minus = (src_width.saturating_sub(1)) as f32;
    let height_minus = (src_height.saturating_sub(1)) as f32;

    for y in 0..dst_height as usize {
        let fy = if dst_height == 1 {
            0.0
        } else {
            height_minus * (y as f32) / ((dst_height - 1) as f32)
        };
        let y0 = fy.floor() as usize;
        let y1 = (y0 + 1).min(src_height as usize - 1);
        let wy = fy - y0 as f32;

        let row0 = &src[y0 * src_stride..];
        let row1 = &src[y1 * src_stride..];

        for x in 0..dst_width as usize {
            let fx = if dst_width == 1 {
                0.0
            } else {
                width_minus * (x as f32) / ((dst_width - 1) as f32)
            };
            let x0 = fx.floor() as usize;
            let x1 = (x0 + 1).min(src_width as usize - 1);
            let wx = fx - x0 as f32;

            let dst_index = (y * dst_row_bytes) + (x * 4);

            for channel in 0..4 {
                let c00 = row0[x0 * 4 + channel] as f32;
                let c10 = row0[x1 * 4 + channel] as f32;
                let c01 = row1[x0 * 4 + channel] as f32;
                let c11 = row1[x1 * 4 + channel] as f32;

                let top = c00 + (c10 - c00) * wx;
                let bottom = c01 + (c11 - c01) * wx;
                let value = top + (bottom - top) * wy;
                result[dst_index + channel] = value.round().clamp(0.0, 255.0) as u8;
            }
        }
    }

    result
}

fn fill_path_with_brush(
    scene: &mut Scene,
    fill_rule: VelloFillRule,
    transform: VelloAffine,
    brush: &VelloBrush,
    brush_transform: *const VelloAffine,
    elements: &[VelloPathElement],
) -> Result<(), VelloStatus> {
    let path = build_bez_path(elements).map_err(|err| {
        set_last_error(err);
        VelloStatus::InvalidArgument
    })?;
    let brush = brush_from_ffi(brush)?;
    let brush_ref = BrushRef::from(&brush);
    let brush_transform = optional_affine(brush_transform)?;
    scene.fill(
        fill_rule.into(),
        transform.into(),
        brush_ref,
        brush_transform,
        &path,
    );
    Ok(())
}

fn stroke_path_with_brush(
    scene: &mut Scene,
    style: &VelloStrokeStyle,
    transform: VelloAffine,
    brush: &VelloBrush,
    brush_transform: *const VelloAffine,
    elements: &[VelloPathElement],
) -> Result<(), VelloStatus> {
    let path = build_bez_path(elements).map_err(|err| {
        set_last_error(err);
        VelloStatus::InvalidArgument
    })?;
    let stroke = style.to_stroke()?;
    let brush = brush_from_ffi(brush)?;
    let brush_ref = BrushRef::from(&brush);
    let brush_transform = optional_affine(brush_transform)?;
    scene.stroke(&stroke, transform.into(), brush_ref, brush_transform, &path);
    Ok(())
}

#[repr(i32)]
#[derive(Debug, Copy, Clone)]
pub enum VelloLineCap {
    Butt = 0,
    Round = 1,
    Square = 2,
}

impl From<VelloLineCap> for Cap {
    fn from(value: VelloLineCap) -> Self {
        match value {
            VelloLineCap::Butt => Cap::Butt,
            VelloLineCap::Round => Cap::Round,
            VelloLineCap::Square => Cap::Square,
        }
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone)]
pub enum VelloLineJoin {
    Miter = 0,
    Round = 1,
    Bevel = 2,
}

impl From<VelloLineJoin> for Join {
    fn from(value: VelloLineJoin) -> Self {
        match value {
            VelloLineJoin::Miter => Join::Miter,
            VelloLineJoin::Round => Join::Round,
            VelloLineJoin::Bevel => Join::Bevel,
        }
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloStrokeStyle {
    pub width: f64,
    pub miter_limit: f64,
    pub start_cap: VelloLineCap,
    pub end_cap: VelloLineCap,
    pub line_join: VelloLineJoin,
    pub dash_phase: f64,
    pub dash_pattern: *const f64,
    pub dash_length: usize,
}

impl VelloStrokeStyle {
    fn to_stroke(&self) -> Result<Stroke, VelloStatus> {
        let mut stroke = Stroke::new(self.width)
            .with_start_cap(self.start_cap.into())
            .with_end_cap(self.end_cap.into())
            .with_join(self.line_join.into())
            .with_miter_limit(self.miter_limit);
        if self.dash_length > 0 {
            if self.dash_pattern.is_null() {
                return Err(VelloStatus::InvalidArgument);
            }
            let dashes = unsafe { slice::from_raw_parts(self.dash_pattern, self.dash_length) };
            stroke = stroke.with_dashes(self.dash_phase, dashes.to_vec());
        }
        Ok(stroke)
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_path_stroke_to_fill(
    elements: *const VelloPathElement,
    element_count: usize,
    style: VelloStrokeStyle,
    tolerance: f64,
    out_handle: *mut *mut VelloPathCommandListHandle,
) -> VelloStatus {
    if out_handle.is_null() {
        return VelloStatus::NullPointer;
    }

    clear_last_error();

    let elements_slice = match unsafe { slice_from_raw(elements, element_count) } {
        Ok(slice) => slice,
        Err(status) => return status,
    };

    let path = match build_bez_path(elements_slice) {
        Ok(path) => path,
        Err(err) => {
            set_last_error(err);
            return VelloStatus::InvalidArgument;
        }
    };

    let stroke = match style.to_stroke() {
        Ok(stroke) => stroke,
        Err(status) => return status,
    };

    let stroke_opts = StrokeOpts::default();
    let stroked = vello::kurbo::stroke(
        path.elements().iter().copied(),
        &stroke,
        &stroke_opts,
        tolerance,
    );

    let mut commands = Vec::new();
    append_bez_path_elements(&mut commands, &stroked);

    let handle = VelloPathCommandListHandle {
        commands: commands.into_boxed_slice(),
    };

    unsafe {
        *out_handle = Box::into_raw(Box::new(handle));
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_path_boolean_op(
    path_a: *const VelloPathElement,
    path_a_count: usize,
    path_b: *const VelloPathElement,
    path_b_count: usize,
    fill_rule: VelloFillRule,
    op: VelloPathBooleanOp,
    out_handle: *mut *mut VelloPathCommandListHandle,
) -> VelloStatus {
    if out_handle.is_null() {
        return VelloStatus::NullPointer;
    }

    clear_last_error();

    let slice_a = match unsafe { slice_from_raw(path_a, path_a_count) } {
        Ok(slice) => slice,
        Err(status) => return status,
    };
    let slice_b = match unsafe { slice_from_raw(path_b, path_b_count) } {
        Ok(slice) => slice,
        Err(status) => return status,
    };

    let path_a = match build_bez_path_v11(slice_a) {
        Ok(path) => path,
        Err(err) => {
            set_last_error(err);
            return VelloStatus::InvalidArgument;
        }
    };
    let path_b = match build_bez_path_v11(slice_b) {
        Ok(path) => path,
        Err(err) => {
            set_last_error(err);
            return VelloStatus::InvalidArgument;
        }
    };

    let ls_fill_rule: LinesweeperFillRule = fill_rule.into();
    let ls_op: LinesweeperBinaryOp = op.into();

    let contours = match linesweeper::binary_op(&path_a, &path_b, ls_fill_rule, ls_op) {
        Ok(result) => result,
        Err(err) => {
            set_last_error(err.to_string());
            return VelloStatus::InvalidArgument;
        }
    };

    let mut commands = Vec::new();
    let groups = contours.grouped();
    for group in groups {
        for contour_idx in group {
            let contour = &contours[contour_idx];
            append_bez_path_elements_v11(&mut commands, &contour.path);
        }
    }

    let handle = VelloPathCommandListHandle {
        commands: commands.into_boxed_slice(),
    };

    unsafe {
        *out_handle = Box::into_raw(Box::new(handle));
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_path_command_list_get_data(
    handle: *const VelloPathCommandListHandle,
    out_data: *mut VelloPathCommandList,
) -> VelloStatus {
    if handle.is_null() || out_data.is_null() {
        return VelloStatus::NullPointer;
    }

    let handle_ref = unsafe { &*handle };
    unsafe {
        *out_data = VelloPathCommandList {
            commands: handle_ref.commands.as_ptr(),
            command_count: handle_ref.commands.len(),
        };
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_path_command_list_destroy(
    handle: *mut VelloPathCommandListHandle,
) {
    if !handle.is_null() {
        unsafe { drop(Box::from_raw(handle)) };
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone)]
pub enum VelloAaMode {
    Area = 0,
    Msaa8 = 1,
    Msaa16 = 2,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloRenderFormat {
    Rgba8 = 0,
    Bgra8 = 1,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloPresentMode {
    AutoVsync = 0,
    AutoNoVsync = 1,
    Fifo = 2,
    Immediate = 3,
}

impl From<VelloPresentMode> for wgpu::PresentMode {
    fn from(value: VelloPresentMode) -> Self {
        match value {
            VelloPresentMode::AutoVsync => wgpu::PresentMode::AutoVsync,
            VelloPresentMode::AutoNoVsync => wgpu::PresentMode::AutoNoVsync,
            VelloPresentMode::Fifo => wgpu::PresentMode::Fifo,
            VelloPresentMode::Immediate => wgpu::PresentMode::Immediate,
        }
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWindowHandleKind {
    None = 0,
    Win32 = 1,
    AppKit = 2,
    Wayland = 3,
    Xlib = 4,
    SwapChainPanel = 5,
    CoreWindow = 6,
    CoreAnimationLayer = 7,
    AndroidNativeWindow = 8,
    Headless = 100,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWin32WindowHandle {
    pub hwnd: *mut c_void,
    pub hinstance: *mut c_void,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloAppKitWindowHandle {
    pub ns_view: *mut c_void,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWaylandWindowHandle {
    pub surface: *mut c_void,
    pub display: *mut c_void,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloXlibWindowHandle {
    pub window: u64,
    pub display: *mut c_void,
    pub screen: i32,
    pub visual_id: u64,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloSwapChainPanelHandle {
    pub panel: *mut c_void,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloCoreWindowHandle {
    pub core_window: *mut c_void,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloCoreAnimationLayerHandle {
    pub layer: *mut c_void,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloAndroidNativeWindowHandle {
    pub window: *mut c_void,
}

#[repr(C)]
#[derive(Copy, Clone)]
pub union VelloWindowHandlePayload {
    pub win32: VelloWin32WindowHandle,
    pub appkit: VelloAppKitWindowHandle,
    pub wayland: VelloWaylandWindowHandle,
    pub xlib: VelloXlibWindowHandle,
    pub swap_chain_panel: VelloSwapChainPanelHandle,
    pub core_window: VelloCoreWindowHandle,
    pub core_animation_layer: VelloCoreAnimationLayerHandle,
    pub android_native_window: VelloAndroidNativeWindowHandle,
    pub none: usize,
}

#[repr(C)]
#[derive(Copy, Clone)]
pub struct VelloWindowHandle {
    pub kind: VelloWindowHandleKind,
    pub payload: VelloWindowHandlePayload,
}

impl Default for VelloWindowHandle {
    fn default() -> Self {
        Self {
            kind: VelloWindowHandleKind::None,
            payload: VelloWindowHandlePayload { none: 0 },
        }
    }
}

#[repr(C)]
#[derive(Copy, Clone)]
pub struct VelloSurfaceDescriptor {
    pub width: u32,
    pub height: u32,
    pub present_mode: VelloPresentMode,
    pub handle: VelloWindowHandle,
}

enum SurfaceTargetHandles {
    Raw {
        window: RawWindowHandle,
        display: RawDisplayHandle,
    },
    SwapChainPanel(*mut c_void),
    #[cfg(any(target_os = "macos", target_os = "ios"))]
    CoreAnimationLayer(*mut c_void),
}

#[allow(trivial_numeric_casts)]
fn to_c_ulong(value: u64) -> Result<c_ulong, VelloStatus> {
    if mem::size_of::<c_ulong>() == 4 {
        let narrowed = u32::try_from(value).map_err(|_| VelloStatus::InvalidArgument)?;
        Ok(narrowed as c_ulong)
    } else {
        Ok(value as c_ulong)
    }
}

#[cfg(target_os = "windows")]
fn core_window_to_raw_handles(
    core_window: *mut c_void,
) -> Result<SurfaceTargetHandles, VelloStatus> {
    use windows::Win32::Foundation::HWND;
    use windows::Win32::System::WinRT::ICoreWindowInterop;
    use windows::core::{IUnknown, Interface};

    let ptr = NonNull::new(core_window).ok_or(VelloStatus::InvalidArgument)?;

    unsafe {
        let inspectable = IUnknown::from_raw_borrowed(&ptr.as_ptr())
            .ok_or(VelloStatus::InvalidArgument)?
            .clone();
        let interop: ICoreWindowInterop = inspectable
            .cast()
            .map_err(|_| VelloStatus::InvalidArgument)?;
        let hwnd: HWND = interop
            .WindowHandle()
            .map_err(|_| VelloStatus::InvalidArgument)?;
        if hwnd.0.is_null() {
            return Err(VelloStatus::InvalidArgument);
        }

        let hwnd = NonZeroIsize::new(hwnd.0 as isize).ok_or(VelloStatus::InvalidArgument)?;
        let win32 = Win32WindowHandle::new(hwnd);

        Ok(SurfaceTargetHandles::Raw {
            window: RawWindowHandle::Win32(win32),
            display: RawDisplayHandle::Windows(WindowsDisplayHandle::new()),
        })
    }
}

#[cfg(not(target_os = "windows"))]
fn core_window_to_raw_handles(
    _core_window: *mut c_void,
) -> Result<SurfaceTargetHandles, VelloStatus> {
    Err(VelloStatus::Unsupported)
}

impl TryFrom<&VelloWindowHandle> for SurfaceTargetHandles {
    type Error = VelloStatus;

    fn try_from(handle: &VelloWindowHandle) -> Result<Self, Self::Error> {
        match handle.kind {
            VelloWindowHandleKind::None => Err(VelloStatus::InvalidArgument),
            VelloWindowHandleKind::Headless => Err(VelloStatus::Unsupported),
            VelloWindowHandleKind::Win32 => {
                let payload = unsafe { handle.payload.win32 };
                let hwnd =
                    NonZeroIsize::new(payload.hwnd as isize).ok_or(VelloStatus::InvalidArgument)?;
                let mut win32 = Win32WindowHandle::new(hwnd);
                if let Some(hinstance) = NonZeroIsize::new(payload.hinstance as isize) {
                    win32.hinstance = Some(hinstance);
                }
                Ok(Self::Raw {
                    window: RawWindowHandle::Win32(win32),
                    display: RawDisplayHandle::Windows(WindowsDisplayHandle::new()),
                })
            }
            VelloWindowHandleKind::AppKit => {
                let payload = unsafe { handle.payload.appkit };
                let ns_view = NonNull::new(payload.ns_view).ok_or(VelloStatus::InvalidArgument)?;
                Ok(Self::Raw {
                    window: RawWindowHandle::AppKit(AppKitWindowHandle::new(ns_view)),
                    display: RawDisplayHandle::AppKit(AppKitDisplayHandle::new()),
                })
            }
            VelloWindowHandleKind::Wayland => {
                let payload = unsafe { handle.payload.wayland };
                let surface = NonNull::new(payload.surface).ok_or(VelloStatus::InvalidArgument)?;
                let display = NonNull::new(payload.display).ok_or(VelloStatus::InvalidArgument)?;
                Ok(Self::Raw {
                    window: RawWindowHandle::Wayland(WaylandWindowHandle::new(surface)),
                    display: RawDisplayHandle::Wayland(WaylandDisplayHandle::new(display)),
                })
            }
            VelloWindowHandleKind::Xlib => {
                let payload = unsafe { handle.payload.xlib };
                let display = NonNull::new(payload.display);
                let xlib_display = XlibDisplayHandle::new(display, payload.screen);
                let window_id = to_c_ulong(payload.window)?;
                let mut window = XlibWindowHandle::new(window_id);
                window.visual_id = if payload.visual_id == 0 {
                    0
                } else {
                    to_c_ulong(payload.visual_id)?
                };
                Ok(Self::Raw {
                    window: RawWindowHandle::Xlib(window),
                    display: RawDisplayHandle::Xlib(xlib_display),
                })
            }
            #[cfg(target_os = "windows")]
            VelloWindowHandleKind::SwapChainPanel => {
                let payload = unsafe { handle.payload.swap_chain_panel };
                let panel = NonNull::new(payload.panel).ok_or(VelloStatus::InvalidArgument)?;
                Ok(Self::SwapChainPanel(panel.as_ptr()))
            }
            #[cfg(not(target_os = "windows"))]
            VelloWindowHandleKind::SwapChainPanel => Err(VelloStatus::Unsupported),
            VelloWindowHandleKind::CoreWindow => {
                let payload = unsafe { handle.payload.core_window };
                core_window_to_raw_handles(payload.core_window)
            }
            #[cfg(any(target_os = "macos", target_os = "ios"))]
            VelloWindowHandleKind::CoreAnimationLayer => {
                let payload = unsafe { handle.payload.core_animation_layer };
                let layer = NonNull::new(payload.layer).ok_or(VelloStatus::InvalidArgument)?;
                Ok(Self::CoreAnimationLayer(layer.as_ptr()))
            }
            #[cfg(not(any(target_os = "macos", target_os = "ios")))]
            VelloWindowHandleKind::CoreAnimationLayer => Err(VelloStatus::Unsupported),
            #[cfg(target_os = "android")]
            VelloWindowHandleKind::AndroidNativeWindow => {
                let payload = unsafe { handle.payload.android_native_window };
                let window = NonNull::new(payload.window).ok_or(VelloStatus::InvalidArgument)?;
                Ok(Self::Raw {
                    window: RawWindowHandle::AndroidNdk(AndroidNdkWindowHandle::new(window)),
                    display: RawDisplayHandle::Android(AndroidDisplayHandle::new()),
                })
            }
            #[cfg(not(target_os = "android"))]
            VelloWindowHandleKind::AndroidNativeWindow => Err(VelloStatus::Unsupported),
        }
    }
}

struct HeadlessSurface {
    device: Device,
    queue: Queue,
    texture: wgpu::Texture,
    view: wgpu::TextureView,
    width: u32,
    height: u32,
}

impl HeadlessSurface {
    fn new(width: u32, height: u32) -> Result<Self, VelloStatus> {
        if width == 0 || height == 0 {
            return Err(VelloStatus::InvalidArgument);
        }

        let backends = wgpu::Backends::from_env().unwrap_or_default();
        let flags = wgpu::InstanceFlags::from_build_config().with_env();
        let backend_options = wgpu::BackendOptions::from_env_or_default();
        let instance = Instance::new(&wgpu::InstanceDescriptor {
            backends,
            flags,
            memory_budget_thresholds: wgpu::MemoryBudgetThresholds::default(),
            backend_options,
        });

        let adapter = pollster::block_on(wgpu::util::initialize_adapter_from_env_or_default(
            &instance, None,
        ))
        .map_err(|_| VelloStatus::DeviceCreationFailed)?;

        let adapter_features = adapter.features();
        let mut required_features = wgpu::Features::empty();
        if adapter_features.contains(wgpu::Features::TEXTURE_ADAPTER_SPECIFIC_FORMAT_FEATURES) {
            required_features |= wgpu::Features::TEXTURE_ADAPTER_SPECIFIC_FORMAT_FEATURES;
        }
        let required_limits = adapter.limits();

        let descriptor = wgpu::DeviceDescriptor {
            label: Some("vello_surface_headless_device"),
            required_features,
            required_limits,
            memory_hints: wgpu::MemoryHints::default(),
            trace: Default::default(),
        };

        let (device, queue) = pollster::block_on(adapter.request_device(&descriptor))
            .map_err(|_| VelloStatus::DeviceCreationFailed)?;

        let texture = device.create_texture(&wgpu::TextureDescriptor {
            label: Some("vello_surface_headless_texture"),
            size: wgpu::Extent3d {
                width,
                height,
                depth_or_array_layers: 1,
            },
            mip_level_count: 1,
            sample_count: 1,
            dimension: wgpu::TextureDimension::D2,
            format: wgpu::TextureFormat::Rgba8Unorm,
            usage: wgpu::TextureUsages::RENDER_ATTACHMENT
                | wgpu::TextureUsages::COPY_SRC
                | wgpu::TextureUsages::TEXTURE_BINDING
                | wgpu::TextureUsages::STORAGE_BINDING,
            view_formats: &[],
        });
        let view = texture.create_view(&wgpu::TextureViewDescriptor::default());

        Ok(Self {
            device,
            queue,
            texture,
            view,
            width,
            height,
        })
    }

    fn resize(&mut self, width: u32, height: u32) -> Result<(), VelloStatus> {
        if width == 0 || height == 0 {
            return Err(VelloStatus::InvalidArgument);
        }
        self.texture = self.device.create_texture(&wgpu::TextureDescriptor {
            label: Some("vello_surface_headless_texture"),
            size: wgpu::Extent3d {
                width,
                height,
                depth_or_array_layers: 1,
            },
            mip_level_count: 1,
            sample_count: 1,
            dimension: wgpu::TextureDimension::D2,
            format: wgpu::TextureFormat::Rgba8Unorm,
            usage: wgpu::TextureUsages::RENDER_ATTACHMENT
                | wgpu::TextureUsages::COPY_SRC
                | wgpu::TextureUsages::TEXTURE_BINDING
                | wgpu::TextureUsages::STORAGE_BINDING,
            view_formats: &[],
        });
        self.view = self
            .texture
            .create_view(&wgpu::TextureViewDescriptor::default());
        self.width = width;
        self.height = height;
        Ok(())
    }
}

enum SurfaceBackend {
    Window {
        context: NonNull<VelloRenderContextHandle>,
        surface: RenderSurface<'static>,
    },
    Headless(HeadlessSurface),
}

impl SurfaceBackend {
    fn dimensions(&self) -> (u32, u32) {
        match self {
            SurfaceBackend::Window { surface, .. } => (surface.config.width, surface.config.height),
            SurfaceBackend::Headless(headless) => (headless.width, headless.height),
        }
    }

    fn device_queue(&self) -> (&Device, &Queue) {
        match self {
            SurfaceBackend::Window { context, surface } => {
                let ctx = unsafe { context.as_ref() };
                let device_handle = &ctx.inner.devices[surface.dev_id];
                (&device_handle.device, &device_handle.queue)
            }
            SurfaceBackend::Headless(headless) => (&headless.device, &headless.queue),
        }
    }

    fn resize(&mut self, width: u32, height: u32) -> Result<(), VelloStatus> {
        match self {
            SurfaceBackend::Window { context, surface } => {
                if width == 0 || height == 0 {
                    return Err(VelloStatus::InvalidArgument);
                }
                let ctx = unsafe { context.as_ref() };
                ctx.inner.resize_surface(surface, width, height);
                Ok(())
            }
            SurfaceBackend::Headless(headless) => headless.resize(width, height),
        }
    }
}

pub struct VelloRenderContextHandle {
    inner: RenderContext,
}

enum SurfaceDeviceBinding {
    Window {
        context: NonNull<VelloRenderContextHandle>,
        device_index: usize,
    },
    Headless,
}

pub struct VelloRenderSurfaceHandle {
    backend: SurfaceBackend,
}

pub struct VelloSurfaceRendererHandle {
    renderer: Renderer,
    binding: SurfaceDeviceBinding,
}

fn resize_surface_if_needed(
    backend: &mut SurfaceBackend,
    width: u32,
    height: u32,
) -> Result<(), VelloStatus> {
    let (current_width, current_height) = backend.dimensions();
    if current_width == width && current_height == height {
        return Ok(());
    }
    backend.resize(width, height)
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::ptr;
    use std::slice;
    use vello::kurbo::{Affine, Rect};
    use vello::peniko::{Color, Fill};

    #[test]
    fn headless_surface_render_smoke() {
        let mut descriptor = VelloSurfaceDescriptor {
            width: 160,
            height: 120,
            present_mode: VelloPresentMode::AutoVsync,
            handle: VelloWindowHandle::default(),
        };
        descriptor.handle.kind = VelloWindowHandleKind::Headless;

        let surface = unsafe { vello_render_surface_create(ptr::null_mut(), descriptor) };
        assert!(!surface.is_null(), "surface creation failed");

        let renderer = unsafe {
            vello_surface_renderer_create(
                surface,
                VelloRendererOptions {
                    use_cpu: false,
                    support_area: true,
                    support_msaa8: true,
                    support_msaa16: false,
                    init_threads: 0,
                    pipeline_cache: std::ptr::null_mut(),
                },
            )
        };
        assert!(!renderer.is_null(), "renderer creation failed");

        let mut scene = VelloSceneHandle {
            inner: Scene::new(),
        };
        scene.inner.reset();
        let rect = Rect::new(5.0, 5.0, 90.0, 90.0);
        scene.inner.fill(
            Fill::NonZero,
            Affine::IDENTITY,
            Color::from_rgb8(180, 60, 60),
            None,
            &rect,
        );

        let params = VelloRenderParams {
            width: descriptor.width,
            height: descriptor.height,
            base_color: VelloColor {
                r: 0.0,
                g: 0.0,
                b: 0.0,
                a: 1.0,
            },
            antialiasing: VelloAaMode::Area,
            format: VelloRenderFormat::Rgba8,
        };

        let status =
            unsafe { vello_surface_renderer_render(renderer, surface, &mut scene, params) };
        assert_eq!(status, VelloStatus::Success);

        unsafe {
            vello_surface_renderer_destroy(renderer);
            vello_render_surface_destroy(surface);
        }
    }

    fn load_font_handle(data: &[u8]) -> *mut VelloFontHandle {
        let handle = unsafe { vello_font_create(data.as_ptr(), data.len(), 0) };
        assert!(!handle.is_null(), "font handle should not be null");
        handle
    }

    fn destroy_font_handle(handle: *mut VelloFontHandle) {
        unsafe { vello_font_destroy(handle) };
    }

    #[test]
    fn font_glyph_index_matches_skrifa() {
        let data = include_bytes!(
            "../../../samples/AvaloniaVelloExamples/Assets/vello/roboto/Roboto-Regular.ttf"
        );
        let handle = load_font_handle(data);

        let mut glyph = 0u16;
        let status = unsafe { vello_font_get_glyph_index(handle, 'A' as u32, &mut glyph) };
        assert_eq!(status, VelloStatus::Success);
        assert_ne!(glyph, 0);

        let font_ref = FontRef::from_index(data, 0).expect("valid font");
        let expected = font_ref
            .charmap()
            .map('A' as u32)
            .map(|gid| gid.to_u32() as u16)
            .unwrap_or(0);
        assert_eq!(glyph, expected);

        let status = unsafe { vello_font_get_glyph_index(handle, 0x10FFFF, &mut glyph) };
        assert_eq!(status, VelloStatus::Success);
        assert_eq!(glyph, 0, "missing codepoint should map to zero glyph");

        destroy_font_handle(handle);
    }

    #[test]
    fn font_glyph_metrics_match_skrifa() {
        let data = include_bytes!(
            "../../../samples/AvaloniaVelloExamples/Assets/vello/roboto/Roboto-Regular.ttf"
        );
        let handle = load_font_handle(data);
        let font_ref = FontRef::from_index(data, 0).expect("valid font");

        let glyph = font_ref
            .charmap()
            .map('H' as u32)
            .map(|gid| gid.to_u32() as u16)
            .expect("glyph id");

        let mut metrics = VelloGlyphMetrics::default();
        let status = unsafe { vello_font_get_glyph_metrics(handle, glyph, 24.0, &mut metrics) };
        assert_eq!(status, VelloStatus::Success);

        let skrifa_metrics = SkrifaGlyphMetrics::new(
            &font_ref,
            SkrifaSize::new(24.0),
            SkrifaLocationRef::default(),
        );
        let glyph_id = SkrifaGlyphId::new(glyph as u32);

        let expected_advance = skrifa_metrics.advance_width(glyph_id).unwrap_or(0.0);
        let bounds = skrifa_metrics.bounds(glyph_id);
        let expected_width = bounds.map(|b| b.x_max - b.x_min).unwrap_or(0.0);
        let expected_height = bounds.map(|b| b.y_max - b.y_min).unwrap_or(0.0);
        let expected_y_bearing = bounds.map(|b| b.y_max).unwrap_or(0.0);
        let mut expected_x_bearing = bounds
            .map(|b| b.x_min)
            .unwrap_or_else(|| skrifa_metrics.left_side_bearing(glyph_id).unwrap_or(0.0));
        if expected_width == 0.0 {
            expected_x_bearing = skrifa_metrics
                .left_side_bearing(glyph_id)
                .unwrap_or(expected_x_bearing);
        }

        let epsilon = 0.01;
        assert!((metrics.advance - expected_advance).abs() < epsilon);
        assert!((metrics.width - expected_width).abs() < epsilon);
        assert!((metrics.height - expected_height).abs() < epsilon);
        assert!((metrics.x_bearing - expected_x_bearing).abs() < epsilon);
        assert!((metrics.y_bearing - expected_y_bearing).abs() < epsilon);

        destroy_font_handle(handle);
    }

    #[test]
    fn font_variation_axes_expose_expected_data() {
        let data = include_bytes!(
            "../../../extern/fontations/font-test-data/test_data/ttf/vazirmatn_var_trimmed.ttf"
        );
        let handle = load_font_handle(data);
        let font_ref = FontRef::from_index(data, 0).expect("valid font");

        let mut axes_handle: *mut VelloVariationAxisHandle = std::ptr::null_mut();
        let mut array = VelloVariationAxisArray {
            axes: std::ptr::null(),
            count: 0,
        };

        let status = unsafe { vello_font_get_variation_axes(handle, &mut axes_handle, &mut array) };
        assert_eq!(status, VelloStatus::Success);

        if array.count == 0 {
            unsafe { vello_font_variation_axes_destroy(axes_handle) };
            destroy_font_handle(handle);
            panic!("expected variation axes for variable font");
        }

        let axes = unsafe { slice::from_raw_parts(array.axes, array.count) };
        let wght_tag = u32::from_be_bytes(*b"wght");
        let axis = axes
            .iter()
            .find(|axis| axis.tag == wght_tag)
            .expect("wght axis present");

        let mut expected = None;
        let axes_collection = font_ref.axes();
        for index in 0..axes_collection.len() {
            if let Some(entry) = axes_collection.get(index) {
                if entry.tag().to_be_bytes() == *b"wght" {
                    expected = Some((entry.min_value(), entry.default_value(), entry.max_value()));
                    break;
                }
            }
        }
        let (expected_min, expected_default, expected_max) =
            expected.expect("expected axis metadata");

        let epsilon = 0.01;
        assert!((axis.min_value - expected_min).abs() < epsilon);
        assert!((axis.default_value - expected_default).abs() < epsilon);
        assert!((axis.max_value - expected_max).abs() < epsilon);

        unsafe { vello_font_variation_axes_destroy(axes_handle) };
        destroy_font_handle(handle);
    }

    #[test]
    fn font_ot_metric_and_variations_match_skrifa() {
        let data = include_bytes!(
            "../../../extern/fontations/font-test-data/test_data/ttf/vazirmatn_var_trimmed.ttf"
        );
        let handle = load_font_handle(data);
        let font_ref = FontRef::from_index(data, 0).expect("valid font");

        let wght_tag = u32::from_be_bytes(*b"wght");
        let axes = [VelloVariationAxisValue {
            tag: wght_tag,
            value: 900.0,
        }];

        let location = build_variation_location(&font_ref, axes.as_ptr(), axes.len());
        let coords_storage = location.as_ref().map(|loc| loc.coords().to_vec());
        let coords: &[NormalizedCoord] = coords_storage.as_deref().unwrap_or(&[]);

        let location_ref = location
            .as_ref()
            .map(|loc| SkrifaLocationRef::from(loc))
            .unwrap_or_default();
        let metrics =
            skrifa::metrics::Metrics::new(&font_ref, SkrifaSize::unscaled(), location_ref);
        let os2 = font_ref.os2().ok();
        let hhea = font_ref.hhea().ok();
        let vhea = font_ref.vhea().ok();
        let mvar = font_ref.mvar().ok();

        let asc_tag = u32::from_be_bytes(*b"hasc");
        let skr_tag = SkrifaTag::new(b"hasc");
        let (raw_value, orientation) = compute_ot_metric(
            &metrics,
            os2.as_ref(),
            hhea.as_ref(),
            vhea.as_ref(),
            mvar.as_ref(),
            coords,
            skr_tag,
        )
        .expect("metric present");

        let upem = metrics.units_per_em as i32;
        let mut position = 0i32;
        let status = unsafe {
            vello_font_get_ot_metric(
                handle,
                asc_tag,
                upem,
                upem,
                axes.as_ptr(),
                axes.len(),
                &mut position,
            )
        };
        assert_eq!(status, VelloStatus::Success);

        let scale_factor = match orientation {
            MetricOrientation::X => upem as f32 / metrics.units_per_em as f32,
            MetricOrientation::Y => upem as f32 / metrics.units_per_em as f32,
        };
        let expected_position = (raw_value * scale_factor).round() as i32;
        assert_eq!(position, expected_position);

        let mut delta = 0.0f32;
        let status = unsafe {
            vello_font_get_ot_variation(handle, asc_tag, axes.as_ptr(), axes.len(), &mut delta)
        };
        assert_eq!(status, VelloStatus::Success);

        let expected_delta = mvar_delta(mvar.as_ref(), skr_tag, coords);
        let epsilon = 0.01;
        assert!((delta - expected_delta).abs() < epsilon);

        let mut delta_x = 0i32;
        let status = unsafe {
            vello_font_get_ot_variation_x(
                handle,
                asc_tag,
                upem,
                axes.as_ptr(),
                axes.len(),
                &mut delta_x,
            )
        };
        assert_eq!(status, VelloStatus::Success);
        let expected_delta_x =
            (expected_delta * (upem as f32 / metrics.units_per_em as f32)).round() as i32;
        assert_eq!(delta_x, expected_delta_x);

        let mut delta_y = 0i32;
        let status = unsafe {
            vello_font_get_ot_variation_y(
                handle,
                asc_tag,
                upem,
                axes.as_ptr(),
                axes.len(),
                &mut delta_y,
            )
        };
        assert_eq!(status, VelloStatus::Success);
        let expected_delta_y =
            (expected_delta * (upem as f32 / metrics.units_per_em as f32)).round() as i32;
        assert_eq!(delta_y, expected_delta_y);

        destroy_font_handle(handle);
    }

    #[test]
    fn font_table_enumeration_and_reference_succeeds() {
        let data = include_bytes!(
            "../../../samples/AvaloniaVelloExamples/Assets/vello/roboto/Roboto-Regular.ttf"
        );
        let handle = load_font_handle(data);
        let font_ref = FontRef::from_index(data, 0).expect("valid font");

        let mut tag_handle: *mut VelloFontTableTagHandle = std::ptr::null_mut();
        let mut tag_array = VelloFontTableTagArray {
            tags: std::ptr::null(),
            count: 0,
        };
        let status = unsafe { vello_font_get_table_tags(handle, &mut tag_handle, &mut tag_array) };
        assert_eq!(status, VelloStatus::Success);
        assert!(tag_array.count > 0);

        let tags = unsafe { slice::from_raw_parts(tag_array.tags, tag_array.count) };
        let head_tag = u32::from_be_bytes(*b"head");
        assert!(tags.iter().any(|tag| *tag == head_tag));

        let expected_count = font_ref.table_directory.table_records().len();
        assert_eq!(tags.len(), expected_count);

        let mut table_handle: *mut VelloFontTableDataHandle = std::ptr::null_mut();
        let mut table = VelloFontTableData {
            data: std::ptr::null(),
            length: 0,
        };
        let status =
            unsafe { vello_font_reference_table(handle, head_tag, &mut table_handle, &mut table) };
        assert_eq!(status, VelloStatus::Success);
        assert!(!table.data.is_null());
        assert!(table.length > 0);

        // Ensure table data matches expected length from directory.
        let record = font_ref
            .table_directory
            .table_records()
            .iter()
            .find(|record| record.tag().to_be_bytes() == *b"head")
            .expect("head record present");
        assert_eq!(table.length as u32, record.length());

        unsafe {
            vello_font_table_data_destroy(table_handle);
            vello_font_table_tags_destroy(tag_handle);
        }
        destroy_font_handle(handle);
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn vello_render_context_create() -> *mut VelloRenderContextHandle {
    clear_last_error();
    let context = VelloRenderContextHandle {
        inner: RenderContext::new(),
    };
    Box::into_raw(Box::new(context))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_render_context_destroy(context: *mut VelloRenderContextHandle) {
    if !context.is_null() {
        unsafe {
            drop(Box::from_raw(context));
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_render_surface_create(
    context: *mut VelloRenderContextHandle,
    descriptor: VelloSurfaceDescriptor,
) -> *mut VelloRenderSurfaceHandle {
    clear_last_error();

    if descriptor.width == 0 || descriptor.height == 0 {
        set_last_error("Surface dimensions must be positive");
        return std::ptr::null_mut();
    }

    let present_mode = descriptor.present_mode;

    let backend = if descriptor.handle.kind == VelloWindowHandleKind::Headless {
        match HeadlessSurface::new(descriptor.width, descriptor.height) {
            Ok(surface) => SurfaceBackend::Headless(surface),
            Err(VelloStatus::InvalidArgument) => {
                set_last_error("Surface dimensions must be positive");
                return std::ptr::null_mut();
            }
            Err(_) => {
                set_last_error("Failed to create headless surface");
                return std::ptr::null_mut();
            }
        }
    } else {
        if context.is_null() {
            set_last_error("Context pointer must not be null for window surfaces");
            return std::ptr::null_mut();
        }

        let ctx = unsafe { &mut *context };
        let handles = match SurfaceTargetHandles::try_from(&descriptor.handle) {
            Ok(handles) => handles,
            Err(status) => {
                match status {
                    VelloStatus::Unsupported => set_last_error("Window handle type not supported"),
                    VelloStatus::InvalidArgument => set_last_error("Invalid window handle"),
                    _ => set_last_error("Failed to parse window handle"),
                }
                return std::ptr::null_mut();
            }
        };

        let target = match handles {
            SurfaceTargetHandles::Raw { window, display } => SurfaceTargetUnsafe::RawHandle {
                raw_display_handle: display,
                raw_window_handle: window,
            },
            #[cfg(any(target_os = "macos", target_os = "ios"))]
            SurfaceTargetHandles::CoreAnimationLayer(layer) => {
                SurfaceTargetUnsafe::CoreAnimationLayer(layer)
            }
            #[cfg(target_os = "windows")]
            SurfaceTargetHandles::SwapChainPanel(panel) => {
                SurfaceTargetUnsafe::SwapChainPanel(panel)
            }
            #[cfg(not(target_os = "windows"))]
            SurfaceTargetHandles::SwapChainPanel(_) => {
                unreachable!("SwapChainPanel handles are not available on this platform")
            }
        };

        let surface_raw = match unsafe { ctx.inner.instance.create_surface_unsafe(target) } {
            Ok(surface) => surface,
            Err(err) => {
                set_last_error(format!("Failed to create native surface: {err}"));
                return std::ptr::null_mut();
            }
        };

        let surface = match pollster::block_on(ctx.inner.create_render_surface(
            surface_raw,
            descriptor.width,
            descriptor.height,
            present_mode.into(),
        )) {
            Ok(surface) => surface,
            Err(err) => {
                set_last_error(format!("Failed to configure surface: {err}"));
                return std::ptr::null_mut();
            }
        };

        SurfaceBackend::Window {
            context: NonNull::new(context).expect("context pointer is not null"),
            surface,
        }
    };

    let handle = VelloRenderSurfaceHandle { backend };
    Box::into_raw(Box::new(handle))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_render_surface_destroy(surface: *mut VelloRenderSurfaceHandle) {
    if !surface.is_null() {
        unsafe {
            drop(Box::from_raw(surface));
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_render_surface_resize(
    surface: *mut VelloRenderSurfaceHandle,
    width: u32,
    height: u32,
) -> VelloStatus {
    clear_last_error();

    if surface.is_null() {
        return VelloStatus::NullPointer;
    }

    let surface = unsafe { &mut *surface };
    match resize_surface_if_needed(&mut surface.backend, width, height) {
        Ok(()) => VelloStatus::Success,
        Err(status) => {
            if status == VelloStatus::InvalidArgument {
                set_last_error("Surface dimensions must be positive");
            }
            status
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_surface_renderer_create(
    surface: *mut VelloRenderSurfaceHandle,
    options: VelloRendererOptions,
) -> *mut VelloSurfaceRendererHandle {
    clear_last_error();

    if surface.is_null() {
        set_last_error("Surface pointer must not be null");
        return std::ptr::null_mut();
    }

    let surface = unsafe { &mut *surface };
    let renderer_options = renderer_options_from_ffi(&options);

    let (device, _queue) = surface.backend.device_queue();
    let renderer = match Renderer::new(device, renderer_options) {
        Ok(renderer) => renderer,
        Err(err) => {
            set_last_error(format!("Failed to create renderer: {err}"));
            return std::ptr::null_mut();
        }
    };

    let binding = match &surface.backend {
        SurfaceBackend::Window { context, surface } => SurfaceDeviceBinding::Window {
            context: *context,
            device_index: surface.dev_id,
        },
        SurfaceBackend::Headless(_) => SurfaceDeviceBinding::Headless,
    };

    Box::into_raw(Box::new(VelloSurfaceRendererHandle { renderer, binding }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_surface_renderer_destroy(renderer: *mut VelloSurfaceRendererHandle) {
    if !renderer.is_null() {
        unsafe {
            drop(Box::from_raw(renderer));
        }
    }
}

fn render_params_for_surface(params: &VelloRenderParams, width: u32, height: u32) -> RenderParams {
    RenderParams {
        base_color: params.base_color.into(),
        width,
        height,
        antialiasing_method: params.antialiasing.into(),
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_surface_renderer_render(
    renderer: *mut VelloSurfaceRendererHandle,
    surface: *mut VelloRenderSurfaceHandle,
    scene: *mut VelloSceneHandle,
    params: VelloRenderParams,
) -> VelloStatus {
    clear_last_error();

    if renderer.is_null() || surface.is_null() || scene.is_null() {
        return VelloStatus::NullPointer;
    }

    let renderer = unsafe { &mut *renderer };
    let surface = unsafe { &mut *surface };
    let Some(scene) = (unsafe { scene.as_ref() }) else {
        return VelloStatus::NullPointer;
    };

    match (&renderer.binding, &mut surface.backend) {
        (
            SurfaceDeviceBinding::Window {
                context: renderer_ctx,
                device_index,
            },
            SurfaceBackend::Window {
                context,
                surface: wnd_surface,
            },
        ) => {
            if renderer_ctx.as_ptr() != context.as_ptr() {
                set_last_error("Renderer and surface belong to different contexts");
                return VelloStatus::InvalidArgument;
            }
            if *device_index != wnd_surface.dev_id {
                set_last_error("Renderer device does not match surface device");
                return VelloStatus::InvalidArgument;
            }

            let ctx = unsafe { context.as_ref() };
            let device_handle = &ctx.inner.devices[wnd_surface.dev_id];
            let device = &device_handle.device;
            let queue = &device_handle.queue;

            let width = wnd_surface.config.width;
            let height = wnd_surface.config.height;
            let render_params = render_params_for_surface(&params, width, height);

            if let Err(err) = renderer.renderer.render_to_texture(
                device,
                queue,
                &scene.inner,
                &wnd_surface.target_view,
                &render_params,
            ) {
                set_last_error(format!("Render failed: {err}"));
                return VelloStatus::RenderError;
            }

            let mut attempts = 0;
            loop {
                match wnd_surface.surface.get_current_texture() {
                    Ok(surface_texture) => {
                        let mut encoder =
                            device.create_command_encoder(&wgpu::CommandEncoderDescriptor {
                                label: Some("vello_surface_present"),
                            });
                        wnd_surface.blitter.copy(
                            device,
                            &mut encoder,
                            &wnd_surface.target_view,
                            &surface_texture
                                .texture
                                .create_view(&wgpu::TextureViewDescriptor::default()),
                        );
                        queue.submit(Some(encoder.finish()));
                        surface_texture.present();
                        return VelloStatus::Success;
                    }
                    Err(SurfaceError::Lost) | Err(SurfaceError::Outdated) => {
                        attempts += 1;
                        if attempts > 2 {
                            set_last_error("Surface became invalid while rendering");
                            return VelloStatus::RenderError;
                        }
                        ctx.inner.resize_surface(wnd_surface, width, height);
                        continue;
                    }
                    Err(SurfaceError::Timeout) => {
                        attempts += 1;
                        if attempts > 3 {
                            set_last_error("Timed out acquiring surface texture");
                            return VelloStatus::RenderError;
                        }
                        continue;
                    }
                    Err(SurfaceError::OutOfMemory) => {
                        set_last_error("Surface ran out of memory");
                        return VelloStatus::DeviceCreationFailed;
                    }
                    Err(SurfaceError::Other) => {
                        set_last_error("Surface reported an unknown error");
                        return VelloStatus::RenderError;
                    }
                }
            }
        }
        (SurfaceDeviceBinding::Headless, SurfaceBackend::Headless(headless)) => {
            let width = headless.width;
            let height = headless.height;
            let render_params = render_params_for_surface(&params, width, height);
            if let Err(err) = renderer.renderer.render_to_texture(
                &headless.device,
                &headless.queue,
                &scene.inner,
                &headless.view,
                &render_params,
            ) {
                set_last_error(format!("Render failed: {err}"));
                return VelloStatus::RenderError;
            }
            headless.queue.submit(std::iter::empty());
            VelloStatus::Success
        }
        _ => {
            set_last_error("Renderer and surface have incompatible backends");
            VelloStatus::InvalidArgument
        }
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloImageAlphaMode {
    Straight = 0,
    Premultiplied = 1,
}

impl From<VelloImageAlphaMode> for ImageAlphaType {
    fn from(value: VelloImageAlphaMode) -> Self {
        match value {
            VelloImageAlphaMode::Straight => ImageAlphaType::Alpha,
            VelloImageAlphaMode::Premultiplied => ImageAlphaType::AlphaPremultiplied,
        }
    }
}

impl From<ImageAlphaType> for VelloImageAlphaMode {
    fn from(value: ImageAlphaType) -> Self {
        match value {
            ImageAlphaType::Alpha => VelloImageAlphaMode::Straight,
            ImageAlphaType::AlphaPremultiplied => VelloImageAlphaMode::Premultiplied,
        }
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloExtendMode {
    Pad = 0,
    Repeat = 1,
    Reflect = 2,
}

impl From<VelloExtendMode> for Extend {
    fn from(value: VelloExtendMode) -> Self {
        match value {
            VelloExtendMode::Pad => Extend::Pad,
            VelloExtendMode::Repeat => Extend::Repeat,
            VelloExtendMode::Reflect => Extend::Reflect,
        }
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloImageQualityMode {
    Low = 0,
    Medium = 1,
    High = 2,
}

impl From<VelloImageQualityMode> for ImageQuality {
    fn from(value: VelloImageQualityMode) -> Self {
        match value {
            VelloImageQualityMode::Low => ImageQuality::Low,
            VelloImageQualityMode::Medium => ImageQuality::Medium,
            VelloImageQualityMode::High => ImageQuality::High,
        }
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloBrushKind {
    Solid = 0,
    LinearGradient = 1,
    RadialGradient = 2,
    SweepGradient = 3,
    Image = 4,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloGradientStop {
    pub offset: f32,
    pub color: VelloColor,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloLinearGradient {
    pub start: VelloPoint,
    pub end: VelloPoint,
    pub extend: VelloExtendMode,
    pub stops: *const VelloGradientStop,
    pub stop_count: usize,
}

impl Default for VelloLinearGradient {
    fn default() -> Self {
        Self {
            start: VelloPoint { x: 0.0, y: 0.0 },
            end: VelloPoint { x: 0.0, y: 0.0 },
            extend: VelloExtendMode::Pad,
            stops: std::ptr::null(),
            stop_count: 0,
        }
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloRadialGradient {
    pub start_center: VelloPoint,
    pub start_radius: f32,
    pub end_center: VelloPoint,
    pub end_radius: f32,
    pub extend: VelloExtendMode,
    pub stops: *const VelloGradientStop,
    pub stop_count: usize,
}

impl Default for VelloRadialGradient {
    fn default() -> Self {
        Self {
            start_center: VelloPoint { x: 0.0, y: 0.0 },
            start_radius: 0.0,
            end_center: VelloPoint { x: 0.0, y: 0.0 },
            end_radius: 0.0,
            extend: VelloExtendMode::Pad,
            stops: std::ptr::null(),
            stop_count: 0,
        }
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloSweepGradient {
    pub center: VelloPoint,
    pub start_angle: f32,
    pub end_angle: f32,
    pub extend: VelloExtendMode,
    pub stops: *const VelloGradientStop,
    pub stop_count: usize,
}

impl Default for VelloSweepGradient {
    fn default() -> Self {
        Self {
            center: VelloPoint { x: 0.0, y: 0.0 },
            start_angle: 0.0,
            end_angle: 0.0,
            extend: VelloExtendMode::Pad,
            stops: std::ptr::null(),
            stop_count: 0,
        }
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloImageBrushParams {
    pub image: *const VelloImageHandle,
    pub x_extend: VelloExtendMode,
    pub y_extend: VelloExtendMode,
    pub quality: VelloImageQualityMode,
    pub alpha: f32,
}

impl Default for VelloImageBrushParams {
    fn default() -> Self {
        Self {
            image: std::ptr::null(),
            x_extend: VelloExtendMode::Pad,
            y_extend: VelloExtendMode::Pad,
            quality: VelloImageQualityMode::Medium,
            alpha: 1.0,
        }
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloBrush {
    pub kind: VelloBrushKind,
    pub solid: VelloColor,
    pub linear: VelloLinearGradient,
    pub radial: VelloRadialGradient,
    pub sweep: VelloSweepGradient,
    pub image: VelloImageBrushParams,
}

impl Default for VelloBrush {
    fn default() -> Self {
        Self {
            kind: VelloBrushKind::Solid,
            solid: VelloColor {
                r: 0.0,
                g: 0.0,
                b: 0.0,
                a: 0.0,
            },
            linear: VelloLinearGradient::default(),
            radial: VelloRadialGradient::default(),
            sweep: VelloSweepGradient::default(),
            image: VelloImageBrushParams::default(),
        }
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloBlendMix {
    Normal = 0,
    Multiply = 1,
    Screen = 2,
    Overlay = 3,
    Darken = 4,
    Lighten = 5,
    ColorDodge = 6,
    ColorBurn = 7,
    HardLight = 8,
    SoftLight = 9,
    Difference = 10,
    Exclusion = 11,
    Hue = 12,
    Saturation = 13,
    Color = 14,
    Luminosity = 15,
    Clip = 128,
}

impl From<VelloBlendMix> for peniko::Mix {
    fn from(value: VelloBlendMix) -> Self {
        match value {
            VelloBlendMix::Normal => peniko::Mix::Normal,
            VelloBlendMix::Multiply => peniko::Mix::Multiply,
            VelloBlendMix::Screen => peniko::Mix::Screen,
            VelloBlendMix::Overlay => peniko::Mix::Overlay,
            VelloBlendMix::Darken => peniko::Mix::Darken,
            VelloBlendMix::Lighten => peniko::Mix::Lighten,
            VelloBlendMix::ColorDodge => peniko::Mix::ColorDodge,
            VelloBlendMix::ColorBurn => peniko::Mix::ColorBurn,
            VelloBlendMix::HardLight => peniko::Mix::HardLight,
            VelloBlendMix::SoftLight => peniko::Mix::SoftLight,
            VelloBlendMix::Difference => peniko::Mix::Difference,
            VelloBlendMix::Exclusion => peniko::Mix::Exclusion,
            VelloBlendMix::Hue => peniko::Mix::Hue,
            VelloBlendMix::Saturation => peniko::Mix::Saturation,
            VelloBlendMix::Color => peniko::Mix::Color,
            VelloBlendMix::Luminosity => peniko::Mix::Luminosity,
            VelloBlendMix::Clip => unreachable!("Clip mix is handled via push_clip_layer"),
        }
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloBlendCompose {
    Clear = 0,
    Copy = 1,
    Dest = 2,
    SrcOver = 3,
    DestOver = 4,
    SrcIn = 5,
    DestIn = 6,
    SrcOut = 7,
    DestOut = 8,
    SrcAtop = 9,
    DestAtop = 10,
    Xor = 11,
    Plus = 12,
    PlusLighter = 13,
}

impl From<VelloBlendCompose> for peniko::Compose {
    fn from(value: VelloBlendCompose) -> Self {
        match value {
            VelloBlendCompose::Clear => peniko::Compose::Clear,
            VelloBlendCompose::Copy => peniko::Compose::Copy,
            VelloBlendCompose::Dest => peniko::Compose::Dest,
            VelloBlendCompose::SrcOver => peniko::Compose::SrcOver,
            VelloBlendCompose::DestOver => peniko::Compose::DestOver,
            VelloBlendCompose::SrcIn => peniko::Compose::SrcIn,
            VelloBlendCompose::DestIn => peniko::Compose::DestIn,
            VelloBlendCompose::SrcOut => peniko::Compose::SrcOut,
            VelloBlendCompose::DestOut => peniko::Compose::DestOut,
            VelloBlendCompose::SrcAtop => peniko::Compose::SrcAtop,
            VelloBlendCompose::DestAtop => peniko::Compose::DestAtop,
            VelloBlendCompose::Xor => peniko::Compose::Xor,
            VelloBlendCompose::Plus => peniko::Compose::Plus,
            VelloBlendCompose::PlusLighter => peniko::Compose::PlusLighter,
        }
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloLayerParams {
    pub mix: VelloBlendMix,
    pub compose: VelloBlendCompose,
    pub alpha: f32,
    pub transform: VelloAffine,
    pub clip_elements: *const VelloPathElement,
    pub clip_element_count: usize,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloRendererOptions {
    pub use_cpu: bool,
    pub support_area: bool,
    pub support_msaa8: bool,
    pub support_msaa16: bool,
    pub init_threads: i32,
    pub pipeline_cache: *mut VelloWgpuPipelineCacheHandle,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloGlyph {
    pub id: u32,
    pub x: f32,
    pub y: f32,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloGlyphRunStyle {
    Fill = 0,
    Stroke = 1,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloGlyphRunOptions {
    pub transform: VelloAffine,
    pub glyph_transform: *const VelloAffine,
    pub font_size: f32,
    pub hint: bool,
    pub style: VelloGlyphRunStyle,
    pub brush: VelloBrush,
    pub brush_alpha: f32,
    pub stroke_style: VelloStrokeStyle,
}

impl From<VelloAaMode> for AaConfig {
    fn from(value: VelloAaMode) -> Self {
        match value {
            VelloAaMode::Area => AaConfig::Area,
            VelloAaMode::Msaa8 => AaConfig::Msaa8,
            VelloAaMode::Msaa16 => AaConfig::Msaa16,
        }
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloRenderParams {
    pub width: u32,
    pub height: u32,
    pub base_color: VelloColor,
    pub antialiasing: VelloAaMode,
    pub format: VelloRenderFormat,
}

struct RenderTarget {
    texture: wgpu::Texture,
    view: wgpu::TextureView,
    readback: Buffer,
    width: u32,
    height: u32,
    padded_bytes_per_row: usize,
    unpadded_bytes_per_row: usize,
}

struct ProfilerResultsRaw {
    slices: Box<[VelloGpuProfilerSlice]>,
    labels: Box<[u8]>,
}

#[repr(C)]
pub struct VelloImageHandle {
    image: ImageData,
    stride: usize,
}

#[repr(C)]
pub struct VelloFontHandle {
    font: FontData,
}

#[repr(C)]
pub struct VelloFontTableTagArray {
    pub tags: *const u32,
    pub count: usize,
}

pub struct VelloFontTableTagHandle {
    _tags: Box<[u32]>,
}

#[repr(C)]
pub struct VelloFontTableData {
    pub data: *const u8,
    pub length: usize,
}

pub struct VelloFontTableDataHandle {
    _blob: Blob<u8>,
}

#[repr(C)]
pub struct VelloBlobHandle {
    blob: Blob<u8>,
}

#[repr(C)]
pub struct VelloGlyphOutlineHandle {
    commands: Box<[VelloPathElement]>,
    bounds: VelloRect,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloGlyphOutlineData {
    pub commands: *const VelloPathElement,
    pub command_count: usize,
    pub bounds: VelloRect,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloImageInfo {
    pub width: u32,
    pub height: u32,
    pub format: VelloRenderFormat,
    pub alpha: VelloImageAlphaMode,
    pub stride: usize,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloBlobData {
    pub data: *const u8,
    pub length: usize,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, Default)]
pub struct VelloGlyphMetrics {
    pub advance: f32,
    pub x_bearing: f32,
    pub y_bearing: f32,
    pub width: f32,
    pub height: f32,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, Default)]
pub struct VelloFontMetricsData {
    pub units_per_em: u16,
    pub glyph_count: u16,
    pub ascent: f32,
    pub descent: f32,
    pub leading: f32,
    pub underline_position: f32,
    pub underline_thickness: f32,
    pub strikeout_position: f32,
    pub strikeout_thickness: f32,
    pub is_monospace: bool,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloParleyFontInfo {
    pub family_name: *const c_char,
    pub data: *const u8,
    pub length: usize,
    pub index: u32,
    pub weight: f32,
    pub stretch: f32,
    pub style: i32,
    pub is_monospace: bool,
}

pub struct VelloParleyFontHandle {
    info: VelloParleyFontInfo,
    data: Vec<u8>,
    name: CString,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloStringArray {
    pub items: *const *const c_char,
    pub count: usize,
}

pub struct VelloStringArrayHandle {
    _strings: Vec<CString>,
    pointers: Vec<*const c_char>,
}

struct MatchedFont {
    blob: Blob<u8>,
    index: u32,
    family_id: fontique::FamilyId,
    width: FontiqueWidth,
    style: FontiqueStyle,
    weight: FontiqueWeight,
}

fn fontique_style_from_i32(style: i32) -> FontiqueStyle {
    match style {
        1 => FontiqueStyle::Italic,
        2 => FontiqueStyle::Oblique(None),
        _ => FontiqueStyle::Normal,
    }
}

fn fontique_style_to_i32(style: &FontiqueStyle) -> i32 {
    match style {
        FontiqueStyle::Normal => 0,
        FontiqueStyle::Italic => 1,
        FontiqueStyle::Oblique(_) => 2,
    }
}

fn fontique_width_from_ratio(ratio: f32) -> FontiqueWidth {
    let ratio = if ratio.is_finite() && ratio > 0.0 {
        ratio
    } else {
        1.0
    };
    FontiqueWidth::from_ratio(ratio)
}

fn fontique_weight_from_value(weight: f32) -> FontiqueWeight {
    let clamped = weight.clamp(1.0, 1000.0);
    FontiqueWeight::new(clamped)
}

fn create_parley_font_handle(
    family_name: &str,
    width: FontiqueWidth,
    style: FontiqueStyle,
    weight: FontiqueWeight,
    blob: Blob<u8>,
    index: u32,
) -> Result<*mut VelloParleyFontHandle, VelloStatus> {
    let data = blob.as_ref();
    let mut data_vec = Vec::with_capacity(data.len());
    data_vec.extend_from_slice(data);

    let font_ref = FontRef::from_index(&data_vec, index).map_err(|err| {
        set_last_error(format!("Failed to read font: {err}"));
        VelloStatus::InvalidArgument
    })?;

    let metrics = skrifa::metrics::Metrics::new(
        &font_ref,
        SkrifaSize::new(1.0),
        SkrifaLocationRef::default(),
    );

    let name = CString::new(family_name).map_err(|_| {
        set_last_error("Font family name contains interior null byte");
        VelloStatus::InvalidArgument
    })?;

    let mut handle = Box::new(VelloParleyFontHandle {
        info: VelloParleyFontInfo {
            family_name: std::ptr::null(),
            data: std::ptr::null(),
            length: 0,
            index,
            weight: weight.value(),
            stretch: width.ratio(),
            style: fontique_style_to_i32(&style),
            is_monospace: metrics.is_monospace,
        },
        data: data_vec,
        name,
    });

    handle.info.family_name = handle.name.as_ptr();
    handle.info.data = handle.data.as_ptr();
    handle.info.length = handle.data.len();

    Ok(Box::into_raw(handle))
}

#[repr(C)]
#[derive(Debug, Copy, Clone, Default)]
pub struct VelloShapedGlyph {
    pub glyph_id: u32,
    pub cluster: u32,
    pub x_advance: f32,
    pub x_offset: f32,
    pub y_offset: f32,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, Default)]
pub struct VelloOpenTypeFeature {
    pub tag: u32,
    pub value: u32,
    pub start: u32,
    pub end: u32,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, Default)]
pub struct VelloVariationAxisValue {
    pub tag: u32,
    pub value: f32,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, Default)]
pub struct VelloTextShapeOptions {
    pub font_size: f32,
    pub direction: i32,
    pub script_tag: u32,
    pub language: *const u8,
    pub language_length: usize,
    pub features: *const VelloOpenTypeFeature,
    pub feature_count: usize,
    pub variation_axes: *const VelloVariationAxisValue,
    pub variation_axis_count: usize,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, Default)]
pub struct VelloShapedRun {
    pub glyphs: *const VelloShapedGlyph,
    pub glyph_count: usize,
    pub advance: f32,
}

#[allow(dead_code)]
pub struct VelloShapedRunHandle {
    glyphs: Box<[VelloShapedGlyph]>,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, Default)]
pub struct VelloScriptSegment {
    pub start: u32,
    pub length: u32,
    pub script_tag: u32,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, Default)]
pub struct VelloScriptSegmentArray {
    pub segments: *const VelloScriptSegment,
    pub count: usize,
}

#[allow(dead_code)]
pub struct VelloScriptSegmentHandle {
    segments: Box<[VelloScriptSegment]>,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, Default)]
pub struct VelloVariationAxis {
    pub tag: u32,
    pub min_value: f32,
    pub default_value: f32,
    pub max_value: f32,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, Default)]
pub struct VelloVariationAxisArray {
    pub axes: *const VelloVariationAxis,
    pub count: usize,
}

#[allow(dead_code)]
pub struct VelloVariationAxisHandle {
    axes: Box<[VelloVariationAxis]>,
}

#[repr(C)]
pub struct VelloSvgHandle {
    scene: Scene,
    resolution: Vec2,
}

#[repr(C)]
pub struct VelloVelatoCompositionHandle {
    composition: VelatoComposition,
}

#[repr(C)]
pub struct VelloVelatoRendererHandle {
    renderer: RefCell<VelatoRenderer>,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloVelatoCompositionInfo {
    pub start_frame: f64,
    pub end_frame: f64,
    pub frame_rate: f64,
    pub width: u32,
    pub height: u32,
}

#[repr(C)]
pub struct VelloWgpuInstanceHandle {
    instance: Instance,
}

#[repr(C)]
pub struct VelloWgpuAdapterHandle {
    adapter: Adapter,
}

#[repr(C)]
pub struct VelloWgpuDeviceHandle {
    device: Device,
    queue: Queue,
}
#[repr(C)]
pub struct VelloSharedTextureDesc {
    pub width: u32,
    pub height: u32,
    pub label: *const c_char,
    pub use_keyed_mutex: bool,
}

#[repr(C)]
pub struct VelloSharedTextureHandle {
    pub texture: *mut c_void,
    pub shared_handle: *mut c_void,
    pub keyed_mutex: *mut c_void,
    pub wgpu_texture: *mut VelloWgpuTextureHandle,
    pub adapter_luid_low: u32,
    pub adapter_luid_high: i32,
    pub width: u32,
    pub height: u32,
    pub reserved: *mut c_void,
}

#[repr(C)]
pub struct VelloWgpuQueueHandle {
    queue: Queue,
}

#[repr(C)]
pub struct VelloWgpuSurfaceHandle {
    surface: wgpu::Surface<'static>,
}

#[repr(C)]
pub struct VelloWgpuSurfaceTextureHandle {
    texture: SurfaceTexture,
}

#[repr(C)]
pub struct VelloWgpuTextureViewHandle {
    view: TextureView,
}

#[repr(C)]
pub struct VelloWgpuRendererHandle {
    device: Device,
    queue: Queue,
    renderer: Renderer,
}

#[repr(C)]
pub struct VelloWgpuPipelineCacheHandle {
    cache: PipelineCache,
}

#[repr(C)]
pub struct VelloWgpuShaderModuleHandle {
    module: wgpu::ShaderModule,
}

#[repr(C)]
pub struct VelloWgpuBufferHandle {
    buffer: wgpu::Buffer,
    size: u64,
}

#[repr(C)]
pub struct VelloWgpuSamplerHandle {
    sampler: wgpu::Sampler,
}

#[repr(C)]
pub struct VelloWgpuBindGroupLayoutHandle {
    layout: wgpu::BindGroupLayout,
}

#[repr(C)]
pub struct VelloWgpuPipelineLayoutHandle {
    layout: wgpu::PipelineLayout,
}

#[repr(C)]
pub struct VelloWgpuBindGroupHandle {
    bind_group: wgpu::BindGroup,
}

#[repr(C)]
pub struct VelloWgpuRenderPipelineHandle {
    pipeline: wgpu::RenderPipeline,
}

#[repr(C)]
pub struct VelloWgpuTextureHandle {
    texture: wgpu::Texture,
}

#[repr(C)]
pub struct VelloWgpuCommandEncoderHandle {
    encoder: Option<wgpu::CommandEncoder>,
    pass_active: bool,
}

#[repr(C)]
pub struct VelloWgpuCommandBufferHandle {
    buffer: Option<wgpu::CommandBuffer>,
}

#[allow(dead_code)]
struct VelloWgpuRenderPassInner {
    pass: *mut ManuallyDrop<wgpu::RenderPass<'static>>,
    encoder: *mut VelloWgpuCommandEncoderHandle,
    color_views: Vec<&'static wgpu::TextureView>,
    resolve_views: Vec<Option<&'static wgpu::TextureView>>,
    depth_stencil_view: Option<&'static wgpu::TextureView>,
}

#[repr(C)]
pub struct VelloWgpuRenderPassHandle {
    inner: *mut VelloWgpuRenderPassInner,
}

#[repr(C)]
pub struct VelloGpuProfilerSlice {
    pub label_offset: usize,
    pub label_length: usize,
    pub depth: u32,
    pub has_time: u8,
    pub time_start_ms: f64,
    pub time_end_ms: f64,
}

#[repr(C)]
pub struct VelloGpuProfilerResults {
    pub handle: *mut c_void,
    pub slices: *const VelloGpuProfilerSlice,
    pub slice_count: usize,
    pub labels: *const u8,
    pub labels_len: usize,
    pub total_gpu_time_ms: f64,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWgpuBackendType {
    Noop = 0,
    Vulkan = 1,
    Metal = 2,
    Dx12 = 3,
    Gl = 4,
    BrowserWebGpu = 5,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWgpuDeviceType {
    Other = 0,
    IntegratedGpu = 1,
    DiscreteGpu = 2,
    VirtualGpu = 3,
    Cpu = 4,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuAdapterInfo {
    pub vendor: u32,
    pub device: u32,
    pub backend: VelloWgpuBackendType,
    pub device_type: VelloWgpuDeviceType,
}

#[repr(u32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWgpuPowerPreference {
    None = 0,
    LowPower = 1,
    HighPerformance = 2,
}

#[repr(u32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWgpuDx12Compiler {
    Default = 0,
    Fxc = 1,
    Dxc = 2,
}

#[repr(u32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWgpuLimitsPreset {
    Default = 0,
    DownlevelWebGl2 = 1,
    DownlevelDefault = 2,
    AdapterDefault = 3,
}

#[repr(u32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWgpuCompositeAlphaMode {
    Auto = 0,
    Opaque = 1,
    Premultiplied = 2,
    PostMultiplied = 3,
    Inherit = 4,
}

#[repr(u32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWgpuTextureFormat {
    Rgba8Unorm = 0,
    Rgba8UnormSrgb = 1,
    Bgra8Unorm = 2,
    Bgra8UnormSrgb = 3,
    Rgba16Float = 4,
    R8Uint = 5,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuInstanceDescriptor {
    pub backends: u32,
    pub flags: u32,
    pub dx12_shader_compiler: VelloWgpuDx12Compiler,
}

impl Default for VelloWgpuInstanceDescriptor {
    fn default() -> Self {
        Self {
            backends: 0,
            flags: 0,
            dx12_shader_compiler: VelloWgpuDx12Compiler::Default,
        }
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuRequestAdapterOptions {
    pub power_preference: VelloWgpuPowerPreference,
    pub force_fallback_adapter: bool,
    pub compatible_surface: *const VelloWgpuSurfaceHandle,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuDeviceDescriptor {
    pub label: *const c_char,
    pub required_features: u64,
    pub limits: VelloWgpuLimitsPreset,
}

impl Default for VelloWgpuDeviceDescriptor {
    fn default() -> Self {
        Self {
            label: std::ptr::null(),
            required_features: 0,
            limits: VelloWgpuLimitsPreset::Default,
        }
    }
}

#[repr(C)]
pub struct VelloWgpuSurfaceConfiguration {
    pub usage: u32,
    pub format: VelloWgpuTextureFormat,
    pub width: u32,
    pub height: u32,
    pub present_mode: VelloPresentMode,
    pub alpha_mode: VelloWgpuCompositeAlphaMode,
    pub view_format_count: usize,
    pub view_formats: *const VelloWgpuTextureFormat,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuTextureViewDescriptor {
    pub label: *const c_char,
    pub format: VelloWgpuTextureFormat,
    pub dimension: u32,
    pub aspect: u32,
    pub base_mip_level: u32,
    pub mip_level_count: u32,
    pub base_array_layer: u32,
    pub array_layer_count: u32,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub struct VelloWgpuExtent3d {
    pub width: u32,
    pub height: u32,
    pub depth_or_array_layers: u32,
}

#[repr(u32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWgpuTextureDimension {
    D1 = 0,
    D2 = 1,
    D3 = 2,
}

#[repr(u32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWgpuAddressMode {
    ClampToEdge = 0,
    Repeat = 1,
    MirrorRepeat = 2,
    ClampToBorder = 3,
}

#[repr(u32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWgpuFilterMode {
    Nearest = 0,
    Linear = 1,
}

#[repr(u32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWgpuCompareFunction {
    Undefined = 0,
    Never = 1,
    Less = 2,
    LessEqual = 3,
    Equal = 4,
    Greater = 5,
    NotEqual = 6,
    GreaterEqual = 7,
    Always = 8,
}

#[repr(u32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWgpuStencilOperation {
    Keep = 0,
    Zero = 1,
    Replace = 2,
    Invert = 3,
    IncrementClamp = 4,
    DecrementClamp = 5,
    IncrementWrap = 6,
    DecrementWrap = 7,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuStencilFaceState {
    pub compare: VelloWgpuCompareFunction,
    pub fail_op: VelloWgpuStencilOperation,
    pub depth_fail_op: VelloWgpuStencilOperation,
    pub pass_op: VelloWgpuStencilOperation,
}

#[repr(u32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWgpuLoadOp {
    Load = 0,
    Clear = 1,
}

#[repr(u32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWgpuStoreOp {
    Store = 0,
    Discard = 1,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuColor {
    pub r: f64,
    pub g: f64,
    pub b: f64,
    pub a: f64,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuPipelineCacheDescriptor {
    pub label: *const c_char,
    pub data: *const u8,
    pub data_len: usize,
    pub fallback: bool,
}

#[repr(u32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWgpuShaderSourceKind {
    Wgsl = 0,
    Spirv = 1,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloBytes {
    pub data: *const u8,
    pub length: usize,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloU32Slice {
    pub data: *const u32,
    pub length: usize,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuShaderModuleDescriptor {
    pub label: *const c_char,
    pub source_kind: VelloWgpuShaderSourceKind,
    pub source_wgsl: VelloBytes,
    pub source_spirv: VelloU32Slice,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuBufferDescriptor {
    pub label: *const c_char,
    pub usage: u32,
    pub size: u64,
    pub mapped_at_creation: bool,
    pub initial_data: VelloBytes,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuTextureDescriptor {
    pub label: *const c_char,
    pub size: VelloWgpuExtent3d,
    pub mip_level_count: u32,
    pub sample_count: u32,
    pub dimension: VelloWgpuTextureDimension,
    pub format: VelloWgpuTextureFormat,
    pub usage: u32,
    pub view_format_count: usize,
    pub view_formats: *const VelloWgpuTextureFormat,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuSamplerDescriptor {
    pub label: *const c_char,
    pub address_mode_u: VelloWgpuAddressMode,
    pub address_mode_v: VelloWgpuAddressMode,
    pub address_mode_w: VelloWgpuAddressMode,
    pub mag_filter: VelloWgpuFilterMode,
    pub min_filter: VelloWgpuFilterMode,
    pub mip_filter: VelloWgpuFilterMode,
    pub lod_min_clamp: f32,
    pub lod_max_clamp: f32,
    pub compare: VelloWgpuCompareFunction,
    pub anisotropy_clamp: u16,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuBindGroupLayoutEntry {
    pub binding: u32,
    pub visibility: u32,
    pub ty: u32,
    pub has_dynamic_offset: bool,
    pub min_binding_size: u64,
    pub buffer_type: u32,
    pub texture_view_dimension: u32,
    pub texture_sample_type: u32,
    pub texture_multisampled: bool,
    pub storage_texture_access: u32,
    pub storage_texture_format: VelloWgpuTextureFormat,
    pub sampler_type: u32,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuBindGroupLayoutDescriptor {
    pub label: *const c_char,
    pub entry_count: usize,
    pub entries: *const VelloWgpuBindGroupLayoutEntry,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuBindGroupEntry {
    pub binding: u32,
    pub buffer: *const VelloWgpuBufferHandle,
    pub offset: u64,
    pub size: u64,
    pub sampler: *const VelloWgpuSamplerHandle,
    pub texture_view: *const VelloWgpuTextureViewHandle,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuBindGroupDescriptor {
    pub label: *const c_char,
    pub layout: *const VelloWgpuBindGroupLayoutHandle,
    pub entry_count: usize,
    pub entries: *const VelloWgpuBindGroupEntry,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuPipelineLayoutDescriptor {
    pub label: *const c_char,
    pub bind_group_layout_count: usize,
    pub bind_group_layouts: *const *const VelloWgpuBindGroupLayoutHandle,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuVertexAttribute {
    pub format: u32,
    pub offset: u64,
    pub shader_location: u32,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuVertexBufferLayout {
    pub array_stride: u64,
    pub step_mode: u32,
    pub attribute_count: usize,
    pub attributes: *const VelloWgpuVertexAttribute,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuVertexState {
    pub module: *const VelloWgpuShaderModuleHandle,
    pub entry_point: VelloBytes,
    pub buffer_count: usize,
    pub buffers: *const VelloWgpuVertexBufferLayout,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuBlendComponent {
    pub src_factor: u32,
    pub dst_factor: u32,
    pub operation: u32,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuBlendState {
    pub color: VelloWgpuBlendComponent,
    pub alpha: VelloWgpuBlendComponent,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuColorTargetState {
    pub format: VelloWgpuTextureFormat,
    pub blend: *const VelloWgpuBlendState,
    pub write_mask: u32,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuFragmentState {
    pub module: *const VelloWgpuShaderModuleHandle,
    pub entry_point: VelloBytes,
    pub target_count: usize,
    pub targets: *const VelloWgpuColorTargetState,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuPrimitiveState {
    pub topology: u32,
    pub strip_index_format: u32,
    pub front_face: u32,
    pub cull_mode: u32,
    pub unclipped_depth: bool,
    pub polygon_mode: u32,
    pub conservative: bool,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuDepthStencilState {
    pub format: VelloWgpuTextureFormat,
    pub depth_write_enabled: bool,
    pub depth_compare: VelloWgpuCompareFunction,
    pub stencil_front: VelloWgpuStencilFaceState,
    pub stencil_back: VelloWgpuStencilFaceState,
    pub stencil_read_mask: u32,
    pub stencil_write_mask: u32,
    pub bias_constant: i32,
    pub bias_slope_scale: f32,
    pub bias_clamp: f32,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuMultisampleState {
    pub count: u32,
    pub mask: u32,
    pub alpha_to_coverage_enabled: bool,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuRenderPipelineDescriptor {
    pub label: *const c_char,
    pub layout: *const VelloWgpuPipelineLayoutHandle,
    pub vertex: VelloWgpuVertexState,
    pub primitive: VelloWgpuPrimitiveState,
    pub depth_stencil: *const VelloWgpuDepthStencilState,
    pub multisample: VelloWgpuMultisampleState,
    pub fragment: *const VelloWgpuFragmentState,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuCommandEncoderDescriptor {
    pub label: *const c_char,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuRenderPassColorAttachment {
    pub view: *const VelloWgpuTextureViewHandle,
    pub resolve_target: *const VelloWgpuTextureViewHandle,
    pub load: VelloWgpuLoadOp,
    pub store: VelloWgpuStoreOp,
    pub clear_color: VelloWgpuColor,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuRenderPassDepthStencilAttachment {
    pub view: *const VelloWgpuTextureViewHandle,
    pub depth_load: VelloWgpuLoadOp,
    pub depth_store: VelloWgpuStoreOp,
    pub depth_clear: f32,
    pub stencil_load: VelloWgpuLoadOp,
    pub stencil_store: VelloWgpuStoreOp,
    pub stencil_clear: u32,
    pub depth_read_only: bool,
    pub stencil_read_only: bool,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuRenderPassDescriptor {
    pub label: *const c_char,
    pub color_attachment_count: usize,
    pub color_attachments: *const VelloWgpuRenderPassColorAttachment,
    pub depth_stencil: *const VelloWgpuRenderPassDepthStencilAttachment,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub struct VelloWgpuOrigin3d {
    pub x: u32,
    pub y: u32,
    pub z: u32,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuImageCopyTexture {
    pub texture: *const VelloWgpuTextureHandle,
    pub mip_level: u32,
    pub origin: VelloWgpuOrigin3d,
    pub aspect: u32,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuTextureDataLayout {
    pub offset: u64,
    pub bytes_per_row: u32,
    pub rows_per_image: u32,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuCommandBufferDescriptor {
    pub label: *const c_char,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWgpuIndexBufferBinding {
    pub buffer: *const VelloWgpuBufferHandle,
    pub offset: u64,
    pub format: u32,
}

impl RenderTarget {
    fn extent(&self) -> wgpu::Extent3d {
        wgpu::Extent3d {
            width: self.width,
            height: self.height,
            depth_or_array_layers: 1,
        }
    }
}

struct RendererContext {
    device: Device,
    queue: Queue,
    renderer: Renderer,
    target: RenderTarget,
}

fn align_to(value: usize, alignment: usize) -> Option<usize> {
    if alignment == 0 {
        return Some(value);
    }
    let remainder = value % alignment;
    if remainder == 0 {
        Some(value)
    } else {
        value.checked_add(alignment - remainder)
    }
}

impl RendererContext {
    fn new(width: u32, height: u32) -> Result<Self, VelloStatus> {
        Self::new_with_options(width, height, RendererOptions::default())
    }

    fn new_with_options(
        width: u32,
        height: u32,
        renderer_options: RendererOptions,
    ) -> Result<Self, VelloStatus> {
        if width == 0 || height == 0 {
            return Err(VelloStatus::InvalidArgument);
        }
        let backends = wgpu::Backends::from_env().unwrap_or_default();
        let flags = wgpu::InstanceFlags::from_build_config().with_env();
        let backend_options = wgpu::BackendOptions::from_env_or_default();
        let instance = wgpu::Instance::new(&wgpu::InstanceDescriptor {
            backends,
            flags,
            memory_budget_thresholds: wgpu::MemoryBudgetThresholds::default(),
            backend_options,
        });
        let adapter = pollster::block_on(wgpu::util::initialize_adapter_from_env_or_default(
            &instance, None,
        ))
        .map_err(|_| VelloStatus::DeviceCreationFailed)?;
        let adapter_features = adapter.features();
        let mut required_features = wgpu::Features::empty();
        if adapter_features.contains(wgpu::Features::TEXTURE_ADAPTER_SPECIFIC_FORMAT_FEATURES) {
            required_features |= wgpu::Features::TEXTURE_ADAPTER_SPECIFIC_FORMAT_FEATURES;
        }
        let required_limits = adapter.limits();
        let descriptor = wgpu::DeviceDescriptor {
            label: Some("vello_ffi_device"),
            required_features,
            required_limits,
            memory_hints: wgpu::MemoryHints::default(),
            trace: Default::default(),
        };
        let (device, queue) = pollster::block_on(adapter.request_device(&descriptor))
            .map_err(|_| VelloStatus::DeviceCreationFailed)?;
        let renderer = Renderer::new(&device, renderer_options)
            .map_err(|_| VelloStatus::DeviceCreationFailed)?;
        let target = create_render_target(&device, width, height)?;
        Ok(Self {
            device,
            queue,
            renderer,
            target,
        })
    }

    fn ensure_target(&mut self, width: u32, height: u32) -> Result<(), VelloStatus> {
        if width == self.target.width && height == self.target.height {
            return Ok(());
        }
        self.target = create_render_target(&self.device, width, height)?;
        Ok(())
    }

    fn render_into(
        &mut self,
        scene: &Scene,
        params: &VelloRenderParams,
        out_ptr: *mut u8,
        out_stride: usize,
        out_size: usize,
    ) -> Result<(), VelloStatus> {
        if out_ptr.is_null() {
            return Err(VelloStatus::NullPointer);
        }
        if params.width == 0 || params.height == 0 {
            return Err(VelloStatus::InvalidArgument);
        }
        self.ensure_target(params.width, params.height)?;
        if out_stride < self.target.unpadded_bytes_per_row {
            return Err(VelloStatus::InvalidArgument);
        }
        let required_size = out_stride
            .checked_mul(params.height as usize)
            .ok_or(VelloStatus::InvalidArgument)?;
        if out_size < required_size {
            return Err(VelloStatus::InvalidArgument);
        }

        if self.target.padded_bytes_per_row > u32::MAX as usize {
            return Err(VelloStatus::Unsupported);
        }

        let render_params = RenderParams {
            base_color: params.base_color.into(),
            width: params.width,
            height: params.height,
            antialiasing_method: params.antialiasing.into(),
        };

        self.renderer
            .render_to_texture(
                &self.device,
                &self.queue,
                scene,
                &self.target.view,
                &render_params,
            )
            .map_err(|_| VelloStatus::RenderError)?;

        let mut encoder = self
            .device
            .create_command_encoder(&wgpu::CommandEncoderDescriptor {
                label: Some("vello_ffi_copy_encoder"),
            });
        let bytes_per_row = self.target.padded_bytes_per_row as u32;
        encoder.copy_texture_to_buffer(
            wgpu::TexelCopyTextureInfo {
                texture: &self.target.texture,
                mip_level: 0,
                origin: wgpu::Origin3d::ZERO,
                aspect: wgpu::TextureAspect::All,
            },
            wgpu::TexelCopyBufferInfo {
                buffer: &self.target.readback,
                layout: wgpu::TexelCopyBufferLayout {
                    offset: 0,
                    bytes_per_row: Some(bytes_per_row),
                    rows_per_image: Some(params.height),
                },
            },
            self.target.extent(),
        );
        self.queue.submit(Some(encoder.finish()));
        let buffer_slice = self.target.readback.slice(..);
        let (sender, receiver) = oneshot_channel();
        buffer_slice.map_async(wgpu::MapMode::Read, move |result| {
            sender.send(result).ok();
        });
        self.device
            .poll(wgpu::PollType::Wait)
            .map_err(|_| VelloStatus::MapFailed)?;
        match pollster::block_on(receiver.receive()) {
            Some(Ok(())) => {}
            _ => return Err(VelloStatus::MapFailed),
        }
        let mapped = buffer_slice.get_mapped_range();
        let output = unsafe { slice::from_raw_parts_mut(out_ptr, out_size) };
        let row_size = self.target.unpadded_bytes_per_row;
        if row_size % 4 != 0 {
            self.target.readback.unmap();
            return Err(VelloStatus::Unsupported);
        }
        let padded = self.target.padded_bytes_per_row;
        for y in 0..params.height as usize {
            let src_offset = y * padded;
            let dst_offset = y * out_stride;
            let src = &mapped[src_offset..src_offset + row_size];
            let dst = &mut output[dst_offset..dst_offset + row_size];
            match params.format {
                VelloRenderFormat::Rgba8 => {
                    dst.copy_from_slice(src);
                }
                VelloRenderFormat::Bgra8 => {
                    for (rgba, bgra) in src.chunks_exact(4).zip(dst.chunks_exact_mut(4)) {
                        bgra[0] = rgba[2];
                        bgra[1] = rgba[1];
                        bgra[2] = rgba[0];
                        bgra[3] = rgba[3];
                    }
                }
            }
        }
        drop(mapped);
        self.target.readback.unmap();
        Ok(())
    }
}

fn create_render_target(
    device: &Device,
    width: u32,
    height: u32,
) -> Result<RenderTarget, VelloStatus> {
    if width == 0 || height == 0 {
        return Err(VelloStatus::InvalidArgument);
    }
    let unpadded = (width as usize)
        .checked_mul(4)
        .ok_or(VelloStatus::InvalidArgument)?;
    let padded = align_to(unpadded, wgpu::COPY_BYTES_PER_ROW_ALIGNMENT as usize)
        .ok_or(VelloStatus::InvalidArgument)?;
    if padded > u32::MAX as usize {
        return Err(VelloStatus::Unsupported);
    }
    let texture = device.create_texture(&wgpu::TextureDescriptor {
        label: Some("vello_ffi_target_texture"),
        size: wgpu::Extent3d {
            width,
            height,
            depth_or_array_layers: 1,
        },
        mip_level_count: 1,
        sample_count: 1,
        dimension: wgpu::TextureDimension::D2,
        format: wgpu::TextureFormat::Rgba8Unorm,
        usage: wgpu::TextureUsages::RENDER_ATTACHMENT
            | wgpu::TextureUsages::COPY_SRC
            | wgpu::TextureUsages::STORAGE_BINDING
            | wgpu::TextureUsages::TEXTURE_BINDING,
        view_formats: &[],
    });
    let view = texture.create_view(&wgpu::TextureViewDescriptor {
        label: Some("vello_ffi_target_view"),
        ..Default::default()
    });
    let buffer_size = padded
        .checked_mul(height as usize)
        .ok_or(VelloStatus::InvalidArgument)?;
    let readback = device.create_buffer(&wgpu::BufferDescriptor {
        label: Some("vello_ffi_readback"),
        size: buffer_size as u64,
        usage: wgpu::BufferUsages::COPY_DST | wgpu::BufferUsages::MAP_READ,
        mapped_at_creation: false,
    });
    Ok(RenderTarget {
        texture,
        view,
        readback,
        width,
        height,
        padded_bytes_per_row: padded,
        unpadded_bytes_per_row: unpadded,
    })
}

fn flatten_profiler_results(
    results: &[wgpu_profiler::GpuTimerQueryResult],
    depth: u32,
    slices: &mut Vec<VelloGpuProfilerSlice>,
    labels: &mut Vec<u8>,
) {
    for result in results {
        let label_bytes = result.label.as_bytes();
        let label_offset = labels.len();
        labels.extend_from_slice(label_bytes);
        labels.push(0);

        let (has_time, start_ms, end_ms) = if let Some(range) = &result.time {
            (1, range.start * 1_000.0, range.end * 1_000.0)
        } else {
            (0, 0.0, 0.0)
        };

        slices.push(VelloGpuProfilerSlice {
            label_offset,
            label_length: label_bytes.len(),
            depth,
            has_time,
            time_start_ms: start_ms,
            time_end_ms: end_ms,
        });

        flatten_profiler_results(&result.nested_queries, depth + 1, slices, labels);
    }
}

#[repr(C)]
pub struct VelloRendererHandle {
    inner: RendererContext,
}

#[repr(C)]
pub struct VelloSceneHandle {
    inner: Scene,
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_renderer_create(
    width: u32,
    height: u32,
) -> *mut VelloRendererHandle {
    clear_last_error();
    match RendererContext::new(width, height) {
        Ok(inner) => Box::into_raw(Box::new(VelloRendererHandle { inner })),
        Err(status) => {
            set_last_error(format!("Failed to create renderer: {:?}", status));
            std::ptr::null_mut()
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_renderer_create_with_options(
    width: u32,
    height: u32,
    options: VelloRendererOptions,
) -> *mut VelloRendererHandle {
    clear_last_error();
    let renderer_options = renderer_options_from_ffi(&options);
    match RendererContext::new_with_options(width, height, renderer_options) {
        Ok(inner) => Box::into_raw(Box::new(VelloRendererHandle { inner })),
        Err(status) => {
            set_last_error(format!("Failed to create renderer: {:?}", status));
            std::ptr::null_mut()
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_renderer_destroy(renderer: *mut VelloRendererHandle) {
    if !renderer.is_null() {
        unsafe {
            drop(Box::from_raw(renderer));
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_renderer_render(
    renderer: *mut VelloRendererHandle,
    scene: *const VelloSceneHandle,
    params: VelloRenderParams,
    buffer: *mut u8,
    stride: usize,
    buffer_size: usize,
) -> VelloStatus {
    clear_last_error();
    let Some(renderer) = (unsafe { renderer.as_mut() }) else {
        return VelloStatus::NullPointer;
    };
    let Some(scene) = (unsafe { scene.as_ref() }) else {
        return VelloStatus::NullPointer;
    };
    match renderer
        .inner
        .render_into(&scene.inner, &params, buffer, stride, buffer_size)
    {
        Ok(()) => VelloStatus::Success,
        Err(status) => {
            set_last_error(format!("Render failed: {:?}", status));
            status
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_renderer_resize(
    renderer: *mut VelloRendererHandle,
    width: u32,
    height: u32,
) -> VelloStatus {
    clear_last_error();
    let Some(renderer) = (unsafe { renderer.as_mut() }) else {
        return VelloStatus::NullPointer;
    };
    match renderer.inner.ensure_target(width, height) {
        Ok(()) => VelloStatus::Success,
        Err(status) => {
            set_last_error(format!("Resize failed: {:?}", status));
            status
        }
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn vello_scene_create() -> *mut VelloSceneHandle {
    clear_last_error();
    Box::into_raw(Box::new(VelloSceneHandle {
        inner: Scene::new(),
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_scene_destroy(scene: *mut VelloSceneHandle) {
    if !scene.is_null() {
        unsafe {
            drop(Box::from_raw(scene));
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_scene_reset(scene: *mut VelloSceneHandle) {
    if let Some(scene) = unsafe { scene.as_mut() } {
        scene.inner.reset();
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_scene_fill_path(
    scene: *mut VelloSceneHandle,
    fill_rule: VelloFillRule,
    transform: VelloAffine,
    color: VelloColor,
    elements: *const VelloPathElement,
    element_count: usize,
) -> VelloStatus {
    clear_last_error();
    let Some(scene) = (unsafe { scene.as_mut() }) else {
        return VelloStatus::NullPointer;
    };
    if elements.is_null() {
        return VelloStatus::NullPointer;
    }
    let slice = match unsafe { slice_from_raw(elements, element_count) } {
        Ok(slice) => slice,
        Err(status) => return status,
    };
    let mut brush = VelloBrush::default();
    brush.kind = VelloBrushKind::Solid;
    brush.solid = color;
    match fill_path_with_brush(
        &mut scene.inner,
        fill_rule,
        transform,
        &brush,
        std::ptr::null(),
        slice,
    ) {
        Ok(()) => VelloStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_path_contains_point(
    elements: *const VelloPathElement,
    element_count: usize,
    fill_rule: VelloFillRule,
    point: VelloPoint,
    out_contains: *mut bool,
) -> VelloStatus {
    clear_last_error();
    if elements.is_null() || out_contains.is_null() {
        return VelloStatus::NullPointer;
    }
    let slice = match unsafe { slice_from_raw(elements, element_count) } {
        Ok(slice) => slice,
        Err(status) => return status,
    };
    if slice.is_empty() {
        unsafe { *out_contains = false };
        return VelloStatus::Success;
    }
    let path = match build_bez_path(slice) {
        Ok(path) => path,
        Err(err) => {
            set_last_error(err);
            return VelloStatus::InvalidArgument;
        }
    };
    let pt = Point::new(point.x, point.y);
    let contains = match fill_rule {
        VelloFillRule::NonZero => path.contains(pt),
        VelloFillRule::EvenOdd => path.winding(pt).abs() % 2 == 1,
    };
    unsafe {
        *out_contains = contains;
    }
    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_scene_stroke_path(
    scene: *mut VelloSceneHandle,
    style: VelloStrokeStyle,
    transform: VelloAffine,
    color: VelloColor,
    elements: *const VelloPathElement,
    element_count: usize,
) -> VelloStatus {
    clear_last_error();
    let Some(scene) = (unsafe { scene.as_mut() }) else {
        return VelloStatus::NullPointer;
    };
    if elements.is_null() {
        return VelloStatus::NullPointer;
    }
    let slice = match unsafe { slice_from_raw(elements, element_count) } {
        Ok(slice) => slice,
        Err(status) => return status,
    };
    let mut brush = VelloBrush::default();
    brush.kind = VelloBrushKind::Solid;
    brush.solid = color;
    match stroke_path_with_brush(
        &mut scene.inner,
        &style,
        transform,
        &brush,
        std::ptr::null(),
        slice,
    ) {
        Ok(()) => VelloStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_scene_fill_path_brush(
    scene: *mut VelloSceneHandle,
    fill_rule: VelloFillRule,
    transform: VelloAffine,
    brush: VelloBrush,
    brush_transform: *const VelloAffine,
    elements: *const VelloPathElement,
    element_count: usize,
) -> VelloStatus {
    clear_last_error();
    let Some(scene) = (unsafe { scene.as_mut() }) else {
        return VelloStatus::NullPointer;
    };
    let slice = match unsafe { slice_from_raw(elements, element_count) } {
        Ok(slice) => slice,
        Err(status) => return status,
    };
    match fill_path_with_brush(
        &mut scene.inner,
        fill_rule,
        transform,
        &brush,
        brush_transform,
        slice,
    ) {
        Ok(()) => VelloStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_scene_stroke_path_brush(
    scene: *mut VelloSceneHandle,
    style: VelloStrokeStyle,
    transform: VelloAffine,
    brush: VelloBrush,
    brush_transform: *const VelloAffine,
    elements: *const VelloPathElement,
    element_count: usize,
) -> VelloStatus {
    clear_last_error();
    let Some(scene) = (unsafe { scene.as_mut() }) else {
        return VelloStatus::NullPointer;
    };
    let slice = match unsafe { slice_from_raw(elements, element_count) } {
        Ok(slice) => slice,
        Err(status) => return status,
    };
    match stroke_path_with_brush(
        &mut scene.inner,
        &style,
        transform,
        &brush,
        brush_transform,
        slice,
    ) {
        Ok(()) => VelloStatus::Success,
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_scene_push_layer(
    scene: *mut VelloSceneHandle,
    params: VelloLayerParams,
) -> VelloStatus {
    clear_last_error();
    let Some(scene) = (unsafe { scene.as_mut() }) else {
        return VelloStatus::NullPointer;
    };
    let clip = match unsafe { slice_from_raw(params.clip_elements, params.clip_element_count) } {
        Ok(slice) => slice,
        Err(status) => return status,
    };
    let path = match build_bez_path(clip) {
        Ok(path) => path,
        Err(err) => {
            set_last_error(err);
            return VelloStatus::InvalidArgument;
        }
    };
    if params.mix == VelloBlendMix::Clip {
        scene.inner.push_clip_layer(params.transform.into(), &path);
    } else {
        let blend_mode = blend_mode_from_params(&params);
        scene
            .inner
            .push_layer(blend_mode, params.alpha, params.transform.into(), &path);
    }
    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_scene_push_luminance_mask_layer(
    scene: *mut VelloSceneHandle,
    alpha: f32,
    transform: VelloAffine,
    elements: *const VelloPathElement,
    element_count: usize,
) -> VelloStatus {
    clear_last_error();
    let Some(scene) = (unsafe { scene.as_mut() }) else {
        return VelloStatus::NullPointer;
    };
    let clip = match unsafe { slice_from_raw(elements, element_count) } {
        Ok(slice) => slice,
        Err(status) => return status,
    };
    let path = match build_bez_path(clip) {
        Ok(path) => path,
        Err(err) => {
            set_last_error(err);
            return VelloStatus::InvalidArgument;
        }
    };
    scene
        .inner
        .push_luminance_mask_layer(alpha, transform.into(), &path);
    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_scene_pop_layer(scene: *mut VelloSceneHandle) {
    if let Some(scene) = unsafe { scene.as_mut() } {
        scene.inner.pop_layer();
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_scene_draw_blurred_rounded_rect(
    scene: *mut VelloSceneHandle,
    transform: VelloAffine,
    rect: VelloRect,
    color: VelloColor,
    radius: f64,
    std_dev: f64,
) -> VelloStatus {
    clear_last_error();
    let Some(scene) = (unsafe { scene.as_mut() }) else {
        return VelloStatus::NullPointer;
    };
    if !(rect.width.is_finite() && rect.height.is_finite()) || rect.width < 0.0 || rect.height < 0.0
    {
        return VelloStatus::InvalidArgument;
    }
    let brush: Color = color.into();
    scene.inner.draw_blurred_rounded_rect(
        transform.into(),
        Rect::new(rect.x, rect.y, rect.x + rect.width, rect.y + rect.height),
        brush,
        radius,
        std_dev,
    );
    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_image_create(
    format: VelloRenderFormat,
    alpha: VelloImageAlphaMode,
    width: u32,
    height: u32,
    pixels: *const u8,
    stride: usize,
) -> *mut VelloImageHandle {
    clear_last_error();
    if width == 0 || height == 0 {
        set_last_error("Image dimensions must be non-zero");
        return std::ptr::null_mut();
    }
    if pixels.is_null() {
        set_last_error("Pixel data pointer is null");
        return std::ptr::null_mut();
    }
    let bytes_per_row = match (width as usize).checked_mul(4) {
        Some(value) => value,
        None => {
            set_last_error("Image dimensions overflow");
            return std::ptr::null_mut();
        }
    };
    let stride = if stride == 0 { bytes_per_row } else { stride };
    if stride < bytes_per_row {
        set_last_error("Stride is smaller than row size");
        return std::ptr::null_mut();
    }
    let total_size = match stride.checked_mul(height as usize) {
        Some(value) => value,
        None => {
            set_last_error("Image size overflow");
            return std::ptr::null_mut();
        }
    };
    let src = unsafe { slice::from_raw_parts(pixels, total_size) };
    let mut buffer = vec![0u8; bytes_per_row * height as usize];
    for y in 0..height as usize {
        let src_offset = y * stride;
        let dst_offset = y * bytes_per_row;
        buffer[dst_offset..dst_offset + bytes_per_row]
            .copy_from_slice(&src[src_offset..src_offset + bytes_per_row]);
    }
    let blob = Blob::from(buffer);
    let image = ImageData {
        data: blob,
        format: image_format_from_render(format),
        alpha_type: alpha.into(),
        width,
        height,
    };
    Box::into_raw(Box::new(VelloImageHandle {
        image,
        stride: bytes_per_row,
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_image_encode_png(
    image: *const VelloImageHandle,
    compression: u8,
    out_blob: *mut *mut VelloBlobHandle,
) -> VelloStatus {
    if image.is_null() || out_blob.is_null() {
        return VelloStatus::NullPointer;
    }

    clear_last_error();

    let image_ref = unsafe { &*image };
    let width = image_ref.image.width;
    let height = image_ref.image.height;
    if width == 0 || height == 0 {
        set_last_error("Image has zero dimensions");
        return VelloStatus::InvalidArgument;
    }

    let data = image_ref.image.data.as_ref();
    let stride = image_ref.stride;
    let row_bytes = (width as usize) * 4;
    if stride < row_bytes || data.len() < stride * height as usize {
        set_last_error("Image stride is invalid");
        return VelloStatus::InvalidArgument;
    }

    let mut contiguous = Vec::with_capacity(row_bytes * height as usize);
    if stride == row_bytes {
        contiguous.extend_from_slice(&data[..row_bytes * height as usize]);
    } else {
        for y in 0..height as usize {
            let start = y * stride;
            contiguous.extend_from_slice(&data[start..start + row_bytes]);
        }
    }

    let rgba = match image_ref.image.format {
        ImageFormat::Rgba8 => contiguous,
        ImageFormat::Bgra8 => convert_bgra_to_rgba(&contiguous),
        _ => {
            set_last_error("Unsupported image format for PNG encoding");
            return VelloStatus::Unsupported;
        }
    };

    let mut buffer = Vec::new();
    {
        let mut encoder = Encoder::new(&mut buffer, width, height);
        encoder.set_depth(BitDepth::Eight);
        encoder.set_color(ColorType::Rgba);
        encoder.set_compression(png_compression_from_level(compression));
        let mut writer = match encoder.write_header() {
            Ok(writer) => writer,
            Err(err) => {
                set_last_error(format!("PNG encode failed: {err}"));
                return VelloStatus::RenderError;
            }
        };
        if let Err(err) = writer.write_image_data(&rgba) {
            set_last_error(format!("PNG encode failed: {err}"));
            return VelloStatus::RenderError;
        }
    }

    unsafe {
        *out_blob = Box::into_raw(Box::new(VelloBlobHandle {
            blob: Blob::from(buffer),
        }));
    }

    VelloStatus::Success
}

fn create_rgba_image_handle(
    rgba: Vec<u8>,
    width: u32,
    height: u32,
    alpha_type: ImageAlphaType,
) -> Result<*mut VelloImageHandle, VelloStatus> {
    let expected = match (width as usize)
        .checked_mul(height as usize)
        .and_then(|value| value.checked_mul(4))
    {
        Some(value) => value,
        None => {
            set_last_error("Image dimensions overflow");
            return Err(VelloStatus::InvalidArgument);
        }
    };

    if rgba.len() != expected {
        set_last_error("Image buffer size does not match dimensions");
        return Err(VelloStatus::RenderError);
    }

    let stride = width as usize * 4;
    let image = ImageData {
        data: Blob::from(rgba),
        format: ImageFormat::Rgba8,
        alpha_type,
        width,
        height,
    };

    Ok(Box::into_raw(Box::new(VelloImageHandle { image, stride })))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_image_destroy(image: *mut VelloImageHandle) {
    if !image.is_null() {
        unsafe { drop(Box::from_raw(image)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_image_decode_png(
    data: *const u8,
    length: usize,
    out_image: *mut *mut VelloImageHandle,
) -> VelloStatus {
    if data.is_null() || out_image.is_null() {
        return VelloStatus::NullPointer;
    }
    if length == 0 {
        return VelloStatus::InvalidArgument;
    }

    clear_last_error();

    let bytes = unsafe { slice::from_raw_parts(data, length) };
    let cursor = Cursor::new(bytes);
    let decoder = Decoder::new(cursor);
    let mut reader = match decoder.read_info() {
        Ok(reader) => reader,
        Err(err) => {
            set_last_error(format!("PNG decode failed: {err}"));
            return VelloStatus::RenderError;
        }
    };

    if reader.info().bit_depth != BitDepth::Eight {
        set_last_error("Only 8-bit PNG images are supported");
        return VelloStatus::Unsupported;
    }

    let mut buffer = vec![0u8; reader.output_buffer_size()];
    let frame_info = match reader.next_frame(&mut buffer) {
        Ok(info) => info,
        Err(err) => {
            set_last_error(format!("PNG frame decode failed: {err}"));
            return VelloStatus::RenderError;
        }
    };

    let width = frame_info.width;
    let height = frame_info.height;
    if width == 0 || height == 0 {
        set_last_error("Decoded PNG has zero dimensions");
        return VelloStatus::InvalidArgument;
    }

    let pixels = &buffer[..frame_info.buffer_size()];
    let mut rgba = Vec::with_capacity(width as usize * height as usize * 4);
    let mut alpha_type = ImageAlphaType::Alpha;

    match frame_info.color_type {
        ColorType::Rgba => {
            rgba.extend_from_slice(pixels);
            alpha_type = ImageAlphaType::Alpha;
        }
        ColorType::Rgb => {
            for chunk in pixels.chunks_exact(3) {
                rgba.extend_from_slice(&[chunk[0], chunk[1], chunk[2], 0xFF]);
            }
        }
        ColorType::Grayscale => {
            for &value in pixels {
                rgba.extend_from_slice(&[value, value, value, 0xFF]);
            }
        }
        ColorType::GrayscaleAlpha => {
            alpha_type = ImageAlphaType::Alpha;
            for chunk in pixels.chunks_exact(2) {
                let gray = chunk[0];
                let alpha = chunk[1];
                rgba.extend_from_slice(&[gray, gray, gray, alpha]);
            }
        }
        ColorType::Indexed => {
            let palette = match reader.info().palette.as_ref() {
                Some(palette) => palette,
                None => {
                    set_last_error("Indexed PNG images are missing a palette");
                    return VelloStatus::Unsupported;
                }
            };

            if palette.len() % 3 != 0 {
                set_last_error("Indexed PNG palette has invalid length");
                return VelloStatus::RenderError;
            }

            let entry_count = palette.len() / 3;
            let transparency = reader.info().trns.as_ref();

            for &index in pixels {
                let idx = index as usize;
                if idx >= entry_count {
                    set_last_error("Indexed PNG palette entry is out of range");
                    return VelloStatus::RenderError;
                }

                let base = idx * 3;
                let alpha = transparency
                    .and_then(|values| values.get(idx))
                    .copied()
                    .unwrap_or(0xFF);

                rgba.extend_from_slice(&[
                    palette[base],
                    palette[base + 1],
                    palette[base + 2],
                    alpha,
                ]);
            }
        }
    }

    if rgba.len() != width as usize * height as usize * 4 {
        set_last_error("PNG decoder produced unexpected output size");
        return VelloStatus::RenderError;
    }

    let handle = match create_rgba_image_handle(rgba, width, height, alpha_type) {
        Ok(handle) => handle,
        Err(status) => return status,
    };

    unsafe {
        *out_image = handle;
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_image_decode_ico(
    data: *const u8,
    length: usize,
    out_image: *mut *mut VelloImageHandle,
) -> VelloStatus {
    if data.is_null() || out_image.is_null() {
        return VelloStatus::NullPointer;
    }
    if length == 0 {
        return VelloStatus::InvalidArgument;
    }

    clear_last_error();

    let bytes = unsafe { slice::from_raw_parts(data, length) };
    let cursor = Cursor::new(bytes);

    let decoder = match IcoDecoder::new(cursor) {
        Ok(decoder) => decoder,
        Err(err) => {
            set_last_error(format!("ICO decode failed: {err}"));
            return VelloStatus::RenderError;
        }
    };

    let (width, height) = decoder.dimensions();
    if width == 0 || height == 0 {
        set_last_error("Decoded ICO has zero dimensions");
        return VelloStatus::InvalidArgument;
    }

    if decoder.color_type() != ImageColorType::Rgba8 {
        set_last_error("ICO decode failed: unsupported pixel format (expected RGBA8)");
        return VelloStatus::Unsupported;
    }

    let total_bytes = decoder.total_bytes();
    let required_len = match usize::try_from(total_bytes) {
        Ok(len) => len,
        Err(_) => {
            set_last_error("ICO decode failed: image is too large");
            return VelloStatus::InvalidArgument;
        }
    };

    let mut rgba = vec![0u8; required_len];
    if let Err(err) = decoder.read_image(&mut rgba) {
        set_last_error(format!("ICO frame decode failed: {err}"));
        return VelloStatus::RenderError;
    }

    let expected_len = match (width as usize)
        .checked_mul(height as usize)
        .and_then(|value| value.checked_mul(4))
    {
        Some(len) => len,
        None => {
            set_last_error("ICO decode failed: image dimensions overflow");
            return VelloStatus::InvalidArgument;
        }
    };

    if rgba.len() != expected_len {
        set_last_error("ICO decode failed: unexpected buffer size");
        return VelloStatus::RenderError;
    }

    let alpha_type = ImageAlphaType::Alpha;

    let handle = match create_rgba_image_handle(rgba, width, height, alpha_type) {
        Ok(handle) => handle,
        Err(status) => return status,
    };

    unsafe {
        *out_image = handle;
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_image_get_info(
    image: *const VelloImageHandle,
    out_info: *mut VelloImageInfo,
) -> VelloStatus {
    if image.is_null() || out_info.is_null() {
        return VelloStatus::NullPointer;
    }

    let image_ref = unsafe { &*image };
    unsafe {
        *out_info = VelloImageInfo {
            width: image_ref.image.width,
            height: image_ref.image.height,
            format: render_format_from_image(image_ref.image.format),
            alpha: image_ref.image.alpha_type.into(),
            stride: image_ref.stride,
        };
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_image_map_pixels(
    image: *const VelloImageHandle,
    out_pixels: *mut *const u8,
    out_length: *mut usize,
) -> VelloStatus {
    if image.is_null() || out_pixels.is_null() || out_length.is_null() {
        return VelloStatus::NullPointer;
    }

    let image_ref = unsafe { &*image };
    let data = image_ref.image.data.as_ref();
    unsafe {
        *out_pixels = data.as_ptr();
        *out_length = data.len();
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_image_unmap_pixels(_image: *const VelloImageHandle) -> VelloStatus {
    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_image_resize(
    image: *const VelloImageHandle,
    width: u32,
    height: u32,
    quality: VelloImageQualityMode,
    out_image: *mut *mut VelloImageHandle,
) -> VelloStatus {
    if image.is_null() || out_image.is_null() {
        return VelloStatus::NullPointer;
    }
    if width == 0 || height == 0 {
        return VelloStatus::InvalidArgument;
    }

    clear_last_error();

    let image_ref = unsafe { &*image };
    let src = image_ref.image.data.as_ref();
    let resized = resize_image_data(
        src,
        image_ref.image.width,
        image_ref.image.height,
        image_ref.stride,
        width,
        height,
        quality,
    );

    if resized.is_empty() {
        set_last_error("Failed to resize image");
        return VelloStatus::RenderError;
    }

    let blob = Blob::from(resized);
    let new_image = ImageData {
        data: blob,
        format: image_ref.image.format,
        alpha_type: image_ref.image.alpha_type,
        width,
        height,
    };

    unsafe {
        *out_image = Box::into_raw(Box::new(VelloImageHandle {
            image: new_image,
            stride: (width as usize) * 4,
        }));
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_blob_get_data(
    blob: *const VelloBlobHandle,
    out_data: *mut VelloBlobData,
) -> VelloStatus {
    if blob.is_null() || out_data.is_null() {
        return VelloStatus::NullPointer;
    }

    let blob_ref = unsafe { &*blob };
    let data = blob_ref.blob.as_ref();
    unsafe {
        *out_data = VelloBlobData {
            data: data.as_ptr(),
            length: data.len(),
        };
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_blob_destroy(blob: *mut VelloBlobHandle) {
    if !blob.is_null() {
        unsafe { drop(Box::from_raw(blob)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_scene_draw_image(
    scene: *mut VelloSceneHandle,
    brush: VelloImageBrushParams,
    transform: VelloAffine,
) -> VelloStatus {
    clear_last_error();
    let Some(scene) = (unsafe { scene.as_mut() }) else {
        return VelloStatus::NullPointer;
    };
    let brush = match image_brush_from_params(&brush) {
        Ok(brush) => brush,
        Err(status) => return status,
    };
    scene.inner.draw_image(brush.as_ref(), transform.into());
    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_scene_append_scene(
    scene: *mut VelloSceneHandle,
    source: *const VelloSceneHandle,
    transform: VelloAffine,
) -> VelloStatus {
    clear_last_error();

    if scene.is_null() || source.is_null() {
        return VelloStatus::NullPointer;
    }

    if std::ptr::eq(scene, source as *mut VelloSceneHandle) {
        set_last_error("Cannot append a scene to itself");
        return VelloStatus::InvalidArgument;
    }

    let scene = unsafe { &mut *scene };
    let source = unsafe { &*source };

    scene.inner.append(&source.inner, Some(transform.into()));
    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_font_create(
    data: *const u8,
    length: usize,
    index: u32,
) -> *mut VelloFontHandle {
    clear_last_error();
    if length == 0 {
        set_last_error("Font data length must be non-zero");
        return std::ptr::null_mut();
    }
    if data.is_null() {
        set_last_error("Font data pointer is null");
        return std::ptr::null_mut();
    }
    let slice = unsafe { slice::from_raw_parts(data, length) };
    let blob = Blob::from(slice.to_vec());
    let font = FontData::new(blob, index);
    Box::into_raw(Box::new(VelloFontHandle { font }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_font_destroy(font: *mut VelloFontHandle) {
    if !font.is_null() {
        unsafe { drop(Box::from_raw(font)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_font_get_glyph_index(
    font: *const VelloFontHandle,
    codepoint: u32,
    out_glyph: *mut u16,
) -> VelloStatus {
    if font.is_null() || out_glyph.is_null() {
        return VelloStatus::NullPointer;
    }

    let font_handle = unsafe { &*font };
    let data = font_handle.font.data.data();

    let font_ref = match FontRef::from_index(data, font_handle.font.index) {
        Ok(font_ref) => font_ref,
        Err(err) => {
            set_last_error(format!("Failed to read font: {err}"));
            return VelloStatus::InvalidArgument;
        }
    };

    let charmap = font_ref.charmap();
    let glyph = charmap
        .map(codepoint)
        .map(|gid| gid.to_u32() as u16)
        .unwrap_or(0);

    unsafe {
        *out_glyph = glyph;
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_font_get_glyph_count(
    font: *const VelloFontHandle,
    out_count: *mut u32,
) -> VelloStatus {
    if font.is_null() || out_count.is_null() {
        return VelloStatus::NullPointer;
    }

    let font_handle = unsafe { &*font };
    let data = font_handle.font.data.data();

    let font_ref = match FontRef::from_index(data, font_handle.font.index) {
        Ok(font_ref) => font_ref,
        Err(err) => {
            set_last_error(format!("Failed to read font: {err}"));
            return VelloStatus::InvalidArgument;
        }
    };

    let count = font_ref
        .maxp()
        .ok()
        .map(|maxp| maxp.num_glyphs() as u32)
        .unwrap_or(0);

    unsafe {
        *out_count = count;
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_font_count_faces(
    data: *const u8,
    length: usize,
    out_count: *mut u32,
) -> VelloStatus {
    clear_last_error();
    if out_count.is_null() {
        return VelloStatus::NullPointer;
    }

    unsafe {
        *out_count = 0;
    }

    if data.is_null() || length == 0 {
        return VelloStatus::Success;
    }

    let slice = unsafe { slice::from_raw_parts(data, length) };
    let file_ref = match raw::FileRef::new(slice) {
        Ok(file_ref) => file_ref,
        Err(err) => {
            set_last_error(format!("Failed to parse font data: {err}"));
            return VelloStatus::InvalidArgument;
        }
    };

    let count = match file_ref {
        raw::FileRef::Font(_) => 1,
        raw::FileRef::Collection(collection) => match u32::try_from(collection.len()) {
            Ok(value) => value,
            Err(_) => u32::MAX,
        },
    };

    unsafe {
        *out_count = count;
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_font_get_table_tags(
    font: *const VelloFontHandle,
    out_handle: *mut *mut VelloFontTableTagHandle,
    out_array: *mut VelloFontTableTagArray,
) -> VelloStatus {
    if font.is_null() || out_handle.is_null() || out_array.is_null() {
        return VelloStatus::NullPointer;
    }

    unsafe {
        *out_handle = std::ptr::null_mut();
        *out_array = VelloFontTableTagArray {
            tags: std::ptr::null(),
            count: 0,
        };
    }

    let font_handle = unsafe { &*font };
    let data = font_handle.font.data.data();

    let font_ref = match FontRef::from_index(data, font_handle.font.index) {
        Ok(font_ref) => font_ref,
        Err(err) => {
            set_last_error(format!("Failed to read font: {err}"));
            return VelloStatus::InvalidArgument;
        }
    };

    let records = font_ref.table_directory.table_records();
    let mut tags = Vec::with_capacity(records.len());
    for record in records.iter() {
        let raw = record.tag().to_be_bytes();
        tags.push(u32::from_be_bytes(raw));
    }

    if tags.is_empty() {
        return VelloStatus::Success;
    }

    let boxed = tags.into_boxed_slice();
    let array = VelloFontTableTagArray {
        tags: boxed.as_ptr(),
        count: boxed.len(),
    };

    unsafe {
        *out_array = array;
        *out_handle = Box::into_raw(Box::new(VelloFontTableTagHandle { _tags: boxed }));
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_font_table_tags_destroy(handle: *mut VelloFontTableTagHandle) {
    if !handle.is_null() {
        unsafe { drop(Box::from_raw(handle)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_font_reference_table(
    font: *const VelloFontHandle,
    tag: u32,
    out_handle: *mut *mut VelloFontTableDataHandle,
    out_data: *mut VelloFontTableData,
) -> VelloStatus {
    if font.is_null() || out_handle.is_null() || out_data.is_null() {
        return VelloStatus::NullPointer;
    }

    unsafe {
        *out_handle = std::ptr::null_mut();
        *out_data = VelloFontTableData {
            data: std::ptr::null(),
            length: 0,
        };
    }

    let font_handle = unsafe { &*font };
    let data = font_handle.font.data.data();

    let font_ref = match FontRef::from_index(data, font_handle.font.index) {
        Ok(font_ref) => font_ref,
        Err(err) => {
            set_last_error(format!("Failed to read font: {err}"));
            return VelloStatus::InvalidArgument;
        }
    };

    let tag = SkrifaTag::from_be_bytes(tag.to_be_bytes());
    let Some(table) = font_ref.table_data(tag) else {
        return VelloStatus::Success;
    };

    let bytes = table.as_bytes();
    let length = bytes.len();
    let blob = font_handle.font.data.clone();

    let data_ptr = if length == 0 {
        std::ptr::null()
    } else {
        bytes.as_ptr()
    };

    let handle = Box::new(VelloFontTableDataHandle { _blob: blob });
    unsafe {
        *out_data = VelloFontTableData {
            data: data_ptr,
            length,
        };
        *out_handle = Box::into_raw(handle);
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_font_table_data_destroy(handle: *mut VelloFontTableDataHandle) {
    if !handle.is_null() {
        unsafe { drop(Box::from_raw(handle)) };
    }
}

#[derive(Copy, Clone)]
enum MetricOrientation {
    X,
    Y,
}

const TAG_HORIZONTAL_ASCENDER: SkrifaTag = SkrifaTag::new(b"hasc");
const TAG_HORIZONTAL_DESCENDER: SkrifaTag = SkrifaTag::new(b"hdsc");
const TAG_HORIZONTAL_LINE_GAP: SkrifaTag = SkrifaTag::new(b"hlgp");
const TAG_HORIZONTAL_CLIPPING_ASCENT: SkrifaTag = SkrifaTag::new(b"hcla");
const TAG_HORIZONTAL_CLIPPING_DESCENT: SkrifaTag = SkrifaTag::new(b"hcld");
const TAG_VERTICAL_ASCENDER: SkrifaTag = SkrifaTag::new(b"vasc");
const TAG_VERTICAL_DESCENDER: SkrifaTag = SkrifaTag::new(b"vdsc");
const TAG_VERTICAL_LINE_GAP: SkrifaTag = SkrifaTag::new(b"vlgp");
const TAG_HORIZONTAL_CARET_RISE: SkrifaTag = SkrifaTag::new(b"hcrs");
const TAG_HORIZONTAL_CARET_RUN: SkrifaTag = SkrifaTag::new(b"hcrn");
const TAG_HORIZONTAL_CARET_OFFSET: SkrifaTag = SkrifaTag::new(b"hcof");
const TAG_VERTICAL_CARET_RISE: SkrifaTag = SkrifaTag::new(b"vcrs");
const TAG_VERTICAL_CARET_RUN: SkrifaTag = SkrifaTag::new(b"vcrn");
const TAG_VERTICAL_CARET_OFFSET: SkrifaTag = SkrifaTag::new(b"vcof");
const TAG_X_HEIGHT: SkrifaTag = SkrifaTag::new(b"xhgt");
const TAG_CAP_HEIGHT: SkrifaTag = SkrifaTag::new(b"cpht");
const TAG_SUBSCRIPT_EM_X_SIZE: SkrifaTag = SkrifaTag::new(b"sbxs");
const TAG_SUBSCRIPT_EM_Y_SIZE: SkrifaTag = SkrifaTag::new(b"sbys");
const TAG_SUBSCRIPT_EM_X_OFFSET: SkrifaTag = SkrifaTag::new(b"sbxo");
const TAG_SUBSCRIPT_EM_Y_OFFSET: SkrifaTag = SkrifaTag::new(b"sbyo");
const TAG_SUPERSCRIPT_EM_X_SIZE: SkrifaTag = SkrifaTag::new(b"spxs");
const TAG_SUPERSCRIPT_EM_Y_SIZE: SkrifaTag = SkrifaTag::new(b"spys");
const TAG_SUPERSCRIPT_EM_X_OFFSET: SkrifaTag = SkrifaTag::new(b"spxo");
const TAG_SUPERSCRIPT_EM_Y_OFFSET: SkrifaTag = SkrifaTag::new(b"spyo");
const TAG_STRIKEOUT_SIZE: SkrifaTag = SkrifaTag::new(b"strs");
const TAG_STRIKEOUT_OFFSET: SkrifaTag = SkrifaTag::new(b"stro");
const TAG_UNDERLINE_SIZE: SkrifaTag = SkrifaTag::new(b"unds");
const TAG_UNDERLINE_OFFSET: SkrifaTag = SkrifaTag::new(b"undo");

fn build_variation_location<'a>(
    font_ref: &FontRef<'a>,
    variations: *const VelloVariationAxisValue,
    variation_count: usize,
) -> Option<Location> {
    if variations.is_null() || variation_count == 0 {
        return None;
    }

    let axes = font_ref.axes();
    if axes.is_empty() {
        return None;
    }

    let slice = unsafe { slice::from_raw_parts(variations, variation_count) };
    if slice.is_empty() {
        return None;
    }

    let filtered: Vec<_> = axes
        .filter(slice.iter().map(|value| {
            let tag_bytes = value.tag.to_be_bytes();
            VariationSetting::new(SkrifaTag::new(&tag_bytes), value.value)
        }))
        .collect();

    if filtered.is_empty() {
        return None;
    }

    Some(axes.location(filtered.iter().copied()))
}

fn mvar_delta<'a>(
    mvar: Option<&raw::tables::mvar::Mvar<'a>>,
    tag: SkrifaTag,
    coords: &[NormalizedCoord],
) -> f32 {
    if coords.is_empty() {
        return 0.0;
    }

    if let Some(mvar) = mvar {
        match mvar.metric_delta(tag, coords) {
            Ok(delta) => delta.to_f64() as f32,
            Err(ReadError::MetricIsMissing(_)) | Err(ReadError::NullOffset) => 0.0,
            Err(_) => 0.0,
        }
    } else {
        0.0
    }
}

fn compute_ot_metric<'a>(
    metrics: &skrifa::metrics::Metrics,
    os2: Option<&raw::tables::os2::Os2<'a>>,
    hhea: Option<&raw::tables::hhea::Hhea<'a>>,
    vhea: Option<&raw::tables::vhea::Vhea<'a>>,
    mvar: Option<&raw::tables::mvar::Mvar<'a>>,
    coords: &[NormalizedCoord],
    tag: SkrifaTag,
) -> Option<(f32, MetricOrientation)> {
    match tag {
        TAG_HORIZONTAL_ASCENDER => {
            let value = metrics.ascent.abs();
            Some((value, MetricOrientation::Y))
        }
        TAG_HORIZONTAL_DESCENDER => {
            let value = if metrics.descent == 0.0 {
                0.0
            } else {
                -metrics.descent.abs()
            };
            Some((value, MetricOrientation::Y))
        }
        TAG_HORIZONTAL_LINE_GAP => Some((metrics.leading, MetricOrientation::Y)),
        TAG_HORIZONTAL_CLIPPING_ASCENT => {
            let os2 = os2?;
            let mut value = os2.us_win_ascent() as f32;
            value += mvar_delta(mvar, MvarTag::HCLA, coords);
            Some((value, MetricOrientation::Y))
        }
        TAG_HORIZONTAL_CLIPPING_DESCENT => {
            let os2 = os2?;
            let mut value = os2.us_win_descent() as f32;
            value += mvar_delta(mvar, MvarTag::HCLD, coords);
            Some((value, MetricOrientation::Y))
        }
        TAG_HORIZONTAL_CARET_RISE => {
            let hhea = hhea?;
            let mut value = hhea.caret_slope_rise() as f32;
            value += mvar_delta(mvar, MvarTag::HCRS, coords);
            Some((value, MetricOrientation::Y))
        }
        TAG_HORIZONTAL_CARET_RUN => {
            let hhea = hhea?;
            let mut value = hhea.caret_slope_run() as f32;
            value += mvar_delta(mvar, MvarTag::HCRN, coords);
            Some((value, MetricOrientation::X))
        }
        TAG_HORIZONTAL_CARET_OFFSET => {
            let hhea = hhea?;
            let mut value = hhea.caret_offset() as f32;
            value += mvar_delta(mvar, MvarTag::HCOF, coords);
            Some((value, MetricOrientation::X))
        }
        TAG_VERTICAL_ASCENDER => {
            let vhea = vhea?;
            let mut value = vhea.ascender().to_i16() as f32;
            value += mvar_delta(mvar, MvarTag::VASC, coords);
            Some((value.abs(), MetricOrientation::X))
        }
        TAG_VERTICAL_DESCENDER => {
            let vhea = vhea?;
            let mut value = vhea.descender().to_i16() as f32;
            value += mvar_delta(mvar, MvarTag::VDSC, coords);
            if value == 0.0 {
                Some((0.0, MetricOrientation::X))
            } else {
                Some((-value.abs(), MetricOrientation::X))
            }
        }
        TAG_VERTICAL_LINE_GAP => {
            let vhea = vhea?;
            let mut value = vhea.line_gap().to_i16() as f32;
            value += mvar_delta(mvar, MvarTag::VLGP, coords);
            Some((value, MetricOrientation::X))
        }
        TAG_VERTICAL_CARET_RISE => {
            let vhea = vhea?;
            let mut value = vhea.caret_slope_rise() as f32;
            value += mvar_delta(mvar, MvarTag::VCRS, coords);
            Some((value, MetricOrientation::X))
        }
        TAG_VERTICAL_CARET_RUN => {
            let vhea = vhea?;
            let mut value = vhea.caret_slope_run() as f32;
            value += mvar_delta(mvar, MvarTag::VCRN, coords);
            Some((value, MetricOrientation::Y))
        }
        TAG_VERTICAL_CARET_OFFSET => {
            let vhea = vhea?;
            let mut value = vhea.caret_offset() as f32;
            value += mvar_delta(mvar, MvarTag::VCOF, coords);
            Some((value, MetricOrientation::Y))
        }
        TAG_X_HEIGHT => metrics.x_height.map(|value| (value, MetricOrientation::Y)),
        TAG_CAP_HEIGHT => metrics
            .cap_height
            .map(|value| (value, MetricOrientation::Y)),
        TAG_SUBSCRIPT_EM_X_SIZE => {
            let os2 = os2?;
            let mut value = os2.y_subscript_x_size() as f32;
            value += mvar_delta(mvar, MvarTag::SBXS, coords);
            Some((value, MetricOrientation::X))
        }
        TAG_SUBSCRIPT_EM_Y_SIZE => {
            let os2 = os2?;
            let mut value = os2.y_subscript_y_size() as f32;
            value += mvar_delta(mvar, MvarTag::SBYS, coords);
            Some((value, MetricOrientation::Y))
        }
        TAG_SUBSCRIPT_EM_X_OFFSET => {
            let os2 = os2?;
            let mut value = os2.y_subscript_x_offset() as f32;
            value += mvar_delta(mvar, MvarTag::SBXO, coords);
            Some((value, MetricOrientation::X))
        }
        TAG_SUBSCRIPT_EM_Y_OFFSET => {
            let os2 = os2?;
            let mut value = os2.y_subscript_y_offset() as f32;
            value += mvar_delta(mvar, MvarTag::SBYO, coords);
            Some((value, MetricOrientation::Y))
        }
        TAG_SUPERSCRIPT_EM_X_SIZE => {
            let os2 = os2?;
            let mut value = os2.y_superscript_x_size() as f32;
            value += mvar_delta(mvar, MvarTag::SPXS, coords);
            Some((value, MetricOrientation::X))
        }
        TAG_SUPERSCRIPT_EM_Y_SIZE => {
            let os2 = os2?;
            let mut value = os2.y_superscript_y_size() as f32;
            value += mvar_delta(mvar, MvarTag::SPYS, coords);
            Some((value, MetricOrientation::Y))
        }
        TAG_SUPERSCRIPT_EM_X_OFFSET => {
            let os2 = os2?;
            let mut value = os2.y_superscript_x_offset() as f32;
            value += mvar_delta(mvar, MvarTag::SPXO, coords);
            Some((value, MetricOrientation::X))
        }
        TAG_SUPERSCRIPT_EM_Y_OFFSET => {
            let os2 = os2?;
            let mut value = os2.y_superscript_y_offset() as f32;
            value += mvar_delta(mvar, MvarTag::SPYO, coords);
            Some((value, MetricOrientation::Y))
        }
        TAG_STRIKEOUT_SIZE => metrics
            .strikeout
            .map(|decoration| (decoration.thickness, MetricOrientation::Y)),
        TAG_STRIKEOUT_OFFSET => metrics
            .strikeout
            .map(|decoration| (decoration.offset, MetricOrientation::Y)),
        TAG_UNDERLINE_SIZE => metrics
            .underline
            .map(|decoration| (decoration.thickness, MetricOrientation::Y)),
        TAG_UNDERLINE_OFFSET => metrics
            .underline
            .map(|decoration| (decoration.offset, MetricOrientation::Y)),
        _ => None,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_font_get_ot_metric(
    font: *const VelloFontHandle,
    metrics_tag: u32,
    x_scale: i32,
    y_scale: i32,
    variations: *const VelloVariationAxisValue,
    variation_count: usize,
    out_position: *mut i32,
) -> VelloStatus {
    if font.is_null() || out_position.is_null() {
        return VelloStatus::NullPointer;
    }

    let font_handle = unsafe { &*font };
    let data = font_handle.font.data.data();

    let font_ref = match FontRef::from_index(data, font_handle.font.index) {
        Ok(font_ref) => font_ref,
        Err(err) => {
            set_last_error(format!("Failed to read font: {err}"));
            return VelloStatus::InvalidArgument;
        }
    };

    let tag = SkrifaTag::new(&metrics_tag.to_be_bytes());
    let location = build_variation_location(&font_ref, variations, variation_count);
    let location_ref = location
        .as_ref()
        .map(|loc| SkrifaLocationRef::from(loc))
        .unwrap_or_default();
    let coords: &[NormalizedCoord] = location.as_ref().map_or(&[], |loc| loc.coords());

    let metrics = skrifa::metrics::Metrics::new(&font_ref, SkrifaSize::unscaled(), location_ref);
    let os2 = font_ref.os2().ok();
    let hhea = font_ref.hhea().ok();
    let vhea = font_ref.vhea().ok();
    let mvar = font_ref.mvar().ok();

    let Some((value, orientation)) = compute_ot_metric(
        &metrics,
        os2.as_ref(),
        hhea.as_ref(),
        vhea.as_ref(),
        mvar.as_ref(),
        coords,
        tag,
    ) else {
        return VelloStatus::Unsupported;
    };

    let upem = metrics.units_per_em;
    let (scale_x, scale_y) = if upem == 0 {
        (0.0f32, 0.0f32)
    } else {
        (x_scale as f32 / upem as f32, y_scale as f32 / upem as f32)
    };

    let scaled = match orientation {
        MetricOrientation::X => value * scale_x,
        MetricOrientation::Y => value * scale_y,
    };

    let rounded = scaled.round();
    let clamped = rounded.clamp(i32::MIN as f32, i32::MAX as f32);
    unsafe {
        *out_position = clamped as i32;
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_font_get_ot_variation(
    font: *const VelloFontHandle,
    metrics_tag: u32,
    variations: *const VelloVariationAxisValue,
    variation_count: usize,
    out_delta: *mut f32,
) -> VelloStatus {
    if font.is_null() || out_delta.is_null() {
        return VelloStatus::NullPointer;
    }

    let font_handle = unsafe { &*font };
    let data = font_handle.font.data.data();

    let font_ref = match FontRef::from_index(data, font_handle.font.index) {
        Ok(font_ref) => font_ref,
        Err(err) => {
            set_last_error(format!("Failed to read font: {err}"));
            return VelloStatus::InvalidArgument;
        }
    };

    let tag = SkrifaTag::new(&metrics_tag.to_be_bytes());
    let location = build_variation_location(&font_ref, variations, variation_count);
    let location_ref = location
        .as_ref()
        .map(|loc| SkrifaLocationRef::from(loc))
        .unwrap_or_default();
    let coords: &[NormalizedCoord] = location.as_ref().map_or(&[], |loc| loc.coords());
    let mvar = font_ref.mvar().ok();

    let delta = mvar_delta(mvar.as_ref(), tag, coords);

    unsafe {
        *out_delta = delta;
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_font_get_ot_variation_x(
    font: *const VelloFontHandle,
    metrics_tag: u32,
    x_scale: i32,
    variations: *const VelloVariationAxisValue,
    variation_count: usize,
    out_delta: *mut i32,
) -> VelloStatus {
    if font.is_null() || out_delta.is_null() {
        return VelloStatus::NullPointer;
    }

    let font_handle = unsafe { &*font };
    let data = font_handle.font.data.data();

    let font_ref = match FontRef::from_index(data, font_handle.font.index) {
        Ok(font_ref) => font_ref,
        Err(err) => {
            set_last_error(format!("Failed to read font: {err}"));
            return VelloStatus::InvalidArgument;
        }
    };

    let tag = SkrifaTag::new(&metrics_tag.to_be_bytes());
    let location = build_variation_location(&font_ref, variations, variation_count);
    let location_ref = location
        .as_ref()
        .map(|loc| SkrifaLocationRef::from(loc))
        .unwrap_or_default();
    let coords: &[NormalizedCoord] = location.as_ref().map_or(&[], |loc| loc.coords());
    let mvar = font_ref.mvar().ok();

    let delta = mvar_delta(mvar.as_ref(), tag, coords);
    let upem = font_ref
        .head()
        .map(|head| head.units_per_em() as i32)
        .unwrap_or(0);

    let scale = if upem == 0 {
        0.0f32
    } else {
        x_scale as f32 / upem as f32
    };

    let value = (delta * scale).round();
    let clamped = value.clamp(i32::MIN as f32, i32::MAX as f32);

    unsafe {
        *out_delta = clamped as i32;
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_font_get_ot_variation_y(
    font: *const VelloFontHandle,
    metrics_tag: u32,
    y_scale: i32,
    variations: *const VelloVariationAxisValue,
    variation_count: usize,
    out_delta: *mut i32,
) -> VelloStatus {
    if font.is_null() || out_delta.is_null() {
        return VelloStatus::NullPointer;
    }

    let font_handle = unsafe { &*font };
    let data = font_handle.font.data.data();

    let font_ref = match FontRef::from_index(data, font_handle.font.index) {
        Ok(font_ref) => font_ref,
        Err(err) => {
            set_last_error(format!("Failed to read font: {err}"));
            return VelloStatus::InvalidArgument;
        }
    };

    let tag = SkrifaTag::new(&metrics_tag.to_be_bytes());
    let location = build_variation_location(&font_ref, variations, variation_count);
    let location_ref = location
        .as_ref()
        .map(|loc| SkrifaLocationRef::from(loc))
        .unwrap_or_default();
    let coords: &[NormalizedCoord] = location.as_ref().map_or(&[], |loc| loc.coords());
    let mvar = font_ref.mvar().ok();

    let delta = mvar_delta(mvar.as_ref(), tag, coords);
    let upem = font_ref
        .head()
        .map(|head| head.units_per_em() as i32)
        .unwrap_or(0);

    let scale = if upem == 0 {
        0.0f32
    } else {
        y_scale as f32 / upem as f32
    };

    let value = (delta * scale).round();
    let clamped = value.clamp(i32::MIN as f32, i32::MAX as f32);

    unsafe {
        *out_delta = clamped as i32;
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_font_get_metrics(
    font: *const VelloFontHandle,
    font_size: f32,
    out_metrics: *mut VelloFontMetricsData,
) -> VelloStatus {
    if font.is_null() || out_metrics.is_null() {
        return VelloStatus::NullPointer;
    }

    let font_handle = unsafe { &*font };
    let data = font_handle.font.data.data();

    let font_ref = match FontRef::from_index(data, font_handle.font.index) {
        Ok(font_ref) => font_ref,
        Err(err) => {
            set_last_error(format!("Failed to read font: {err}"));
            return VelloStatus::InvalidArgument;
        }
    };

    let size = SkrifaSize::new(font_size.max(0.0));
    let metrics = skrifa::metrics::Metrics::new(&font_ref, size, SkrifaLocationRef::default());

    let underline = metrics.underline.unwrap_or_default();
    let strikeout = metrics.strikeout.unwrap_or_default();

    unsafe {
        *out_metrics = VelloFontMetricsData {
            units_per_em: metrics.units_per_em,
            glyph_count: metrics.glyph_count,
            ascent: metrics.ascent,
            descent: metrics.descent,
            leading: metrics.leading,
            underline_position: underline.offset,
            underline_thickness: underline.thickness,
            strikeout_position: strikeout.offset,
            strikeout_thickness: strikeout.thickness,
            is_monospace: metrics.is_monospace,
        };
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_font_get_glyph_metrics(
    font: *const VelloFontHandle,
    glyph_id: u16,
    font_size: f32,
    out_metrics: *mut VelloGlyphMetrics,
) -> VelloStatus {
    if font.is_null() || out_metrics.is_null() {
        return VelloStatus::NullPointer;
    }

    let font_handle = unsafe { &*font };
    let data = font_handle.font.data.data();

    let font_ref = match FontRef::from_index(data, font_handle.font.index) {
        Ok(font_ref) => font_ref,
        Err(err) => {
            set_last_error(format!("Failed to read font: {err}"));
            return VelloStatus::InvalidArgument;
        }
    };

    let size = SkrifaSize::new(font_size.max(0.0));
    let glyph_metrics = SkrifaGlyphMetrics::new(&font_ref, size, SkrifaLocationRef::default());
    let glyph_id = SkrifaGlyphId::new(u32::from(glyph_id));

    let advance = glyph_metrics.advance_width(glyph_id).unwrap_or(0.0);
    let bounds = glyph_metrics.bounds(glyph_id);

    let mut x_bearing = bounds
        .map(|b| b.x_min)
        .unwrap_or_else(|| glyph_metrics.left_side_bearing(glyph_id).unwrap_or(0.0));
    let width = bounds.map(|b| b.x_max - b.x_min).unwrap_or(0.0);
    let height = bounds.map(|b| b.y_max - b.y_min).unwrap_or(0.0);
    let y_bearing = bounds.map(|b| b.y_max).unwrap_or(0.0);

    // For glyphs with empty bounds, fall back to left side bearing if available.
    if width == 0.0 {
        x_bearing = glyph_metrics
            .left_side_bearing(glyph_id)
            .unwrap_or(x_bearing);
    }

    unsafe {
        *out_metrics = VelloGlyphMetrics {
            advance,
            x_bearing,
            y_bearing,
            width,
            height,
        };
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_font_get_variation_axes(
    font: *const VelloFontHandle,
    out_handle: *mut *mut VelloVariationAxisHandle,
    out_array: *mut VelloVariationAxisArray,
) -> VelloStatus {
    if font.is_null() || out_handle.is_null() || out_array.is_null() {
        return VelloStatus::NullPointer;
    }

    unsafe {
        *out_handle = std::ptr::null_mut();
        *out_array = VelloVariationAxisArray {
            axes: std::ptr::null(),
            count: 0,
        };
    }

    let font_handle = unsafe { &*font };
    let data = font_handle.font.data.data();

    let font_ref = match FontRef::from_index(data, font_handle.font.index) {
        Ok(font_ref) => font_ref,
        Err(err) => {
            set_last_error(format!("Failed to read font: {err}"));
            return VelloStatus::InvalidArgument;
        }
    };

    let axes = font_ref.axes();
    if axes.is_empty() {
        return VelloStatus::Success;
    }

    let mut collected = Vec::with_capacity(axes.len());
    for index in 0..axes.len() {
        if let Some(axis) = axes.get(index) {
            let tag_bytes = axis.tag().to_be_bytes();
            collected.push(VelloVariationAxis {
                tag: u32::from_be_bytes(tag_bytes),
                min_value: axis.min_value(),
                default_value: axis.default_value(),
                max_value: axis.max_value(),
            });
        }
    }

    if collected.is_empty() {
        return VelloStatus::Success;
    }

    let boxed = collected.into_boxed_slice();
    let array = VelloVariationAxisArray {
        axes: boxed.as_ptr(),
        count: boxed.len(),
    };

    unsafe {
        *out_array = array;
        *out_handle = Box::into_raw(Box::new(VelloVariationAxisHandle { axes: boxed }));
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_font_variation_axes_destroy(handle: *mut VelloVariationAxisHandle) {
    if !handle.is_null() {
        unsafe { drop(Box::from_raw(handle)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_font_get_glyph_outline(
    font: *const VelloFontHandle,
    glyph_id: u16,
    font_size: f32,
    _tolerance: f32,
    out_handle: *mut *mut VelloGlyphOutlineHandle,
) -> VelloStatus {
    if font.is_null() || out_handle.is_null() {
        return VelloStatus::NullPointer;
    }

    clear_last_error();

    let font_handle = unsafe { &*font };
    let data = font_handle.font.data.data();

    let index = match usize::try_from(font_handle.font.index) {
        Ok(value) => value,
        Err(_) => {
            set_last_error("Font index exceeds platform limits");
            return VelloStatus::InvalidArgument;
        }
    };

    let swash_font = match SwashFontRef::from_index(data, index) {
        Some(font) => font,
        None => {
            set_last_error("Failed to read font tables for outline extraction");
            return VelloStatus::InvalidArgument;
        }
    };

    let mut scale_context = ScaleContext::new();
    let mut scaler = scale_context
        .builder(swash_font)
        .size(font_size.max(0.0))
        .hint(false)
        .build();

    let mut outline = Outline::new();
    if !scaler.scale_outline_into(SwashGlyphId::from(glyph_id), &mut outline) || outline.is_empty()
    {
        set_last_error("Glyph outline unavailable");
        return VelloStatus::Unsupported;
    }

    let commands = outline_to_path_elements(&outline);
    if commands.is_empty() {
        set_last_error("Glyph outline produced no path commands");
        return VelloStatus::Unsupported;
    }

    let bounds = outline.bounds();
    let rect = VelloRect {
        x: bounds.min.x as f64,
        y: bounds.min.y as f64,
        width: (bounds.max.x - bounds.min.x) as f64,
        height: (bounds.max.y - bounds.min.y) as f64,
    };

    let handle = VelloGlyphOutlineHandle {
        commands: commands.into_boxed_slice(),
        bounds: rect,
    };

    unsafe {
        *out_handle = Box::into_raw(Box::new(handle));
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_glyph_outline_get_data(
    outline: *const VelloGlyphOutlineHandle,
    out_data: *mut VelloGlyphOutlineData,
) -> VelloStatus {
    if outline.is_null() || out_data.is_null() {
        return VelloStatus::NullPointer;
    }

    let outline_ref = unsafe { &*outline };
    unsafe {
        *out_data = VelloGlyphOutlineData {
            commands: outline_ref.commands.as_ptr(),
            command_count: outline_ref.commands.len(),
            bounds: outline_ref.bounds,
        };
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_glyph_outline_destroy(outline: *mut VelloGlyphOutlineHandle) {
    if !outline.is_null() {
        unsafe { drop(Box::from_raw(outline)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_text_shape_utf16(
    font: *const VelloFontHandle,
    text: *const u16,
    length: usize,
    options: *const VelloTextShapeOptions,
    out_run: *mut VelloShapedRun,
    out_handle: *mut *mut VelloShapedRunHandle,
) -> VelloStatus {
    if font.is_null() || text.is_null() || out_run.is_null() || out_handle.is_null() {
        return VelloStatus::NullPointer;
    }

    if options.is_null() {
        return VelloStatus::NullPointer;
    }

    let options = unsafe { &*options };
    let font_size = options.font_size;
    let direction = options.direction;

    // Currently unused advanced options; kept to avoid warnings until implemented.
    let _ = options.script_tag;
    let _ = options.language;
    let _ = options.language_length;
    let _ = options.features;
    let _ = options.feature_count;
    let _ = options.variation_axes;
    let _ = options.variation_axis_count;

    let slice = unsafe { slice::from_raw_parts(text, length) };
    let text_owned = String::from_utf16_lossy(slice);

    let font_handle = unsafe { &*font };
    let data = font_handle.font.data.data();

    let font_ref = match HrFontRef::from_index(data, font_handle.font.index) {
        Ok(font_ref) => font_ref,
        Err(err) => {
            set_last_error(format!("Failed to read font: {err}"));
            return VelloStatus::InvalidArgument;
        }
    };

    let shaper_data = HrShaperData::new(&font_ref);
    let shaper = shaper_data
        .shaper(&font_ref)
        .point_size(Some(font_size))
        .build();

    let units_per_em = shaper.units_per_em() as f32;
    let scale = if units_per_em.abs() > f32::EPSILON {
        font_size / units_per_em
    } else {
        1.0
    };

    let mut buffer = HrUnicodeBuffer::new();
    buffer.push_str(&text_owned);
    buffer.set_direction(if direction != 0 {
        HrDirection::RightToLeft
    } else {
        HrDirection::LeftToRight
    });
    buffer.guess_segment_properties();

    let shaped = shaper.shape(buffer, &[]);

    let positions = shaped.glyph_positions();
    let infos = shaped.glyph_infos();

    let mut glyphs = Vec::with_capacity(positions.len());
    let mut advance = 0.0f32;

    for (pos, info) in positions.iter().zip(infos.iter()) {
        let x_adv = pos.x_advance as f32 * scale;
        let x_off = pos.x_offset as f32 * scale;
        let y_off = pos.y_offset as f32 * scale;
        advance += x_adv;
        glyphs.push(VelloShapedGlyph {
            glyph_id: info.glyph_id,
            cluster: info.cluster,
            x_advance: x_adv,
            x_offset: x_off,
            y_offset: y_off,
        });
    }

    let boxed = glyphs.into_boxed_slice();
    let run = VelloShapedRun {
        glyphs: boxed.as_ptr(),
        glyph_count: boxed.len(),
        advance,
    };

    unsafe {
        *out_run = run;
        *out_handle = Box::into_raw(Box::new(VelloShapedRunHandle { glyphs: boxed }));
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_text_segment_utf16(
    text: *const u16,
    length: usize,
    out_handle: *mut *mut VelloScriptSegmentHandle,
    out_array: *mut VelloScriptSegmentArray,
) -> VelloStatus {
    if out_handle.is_null() || out_array.is_null() {
        return VelloStatus::NullPointer;
    }

    unsafe {
        *out_handle = std::ptr::null_mut();
        *out_array = VelloScriptSegmentArray {
            segments: std::ptr::null(),
            count: 0,
        };
    }

    if text.is_null() || length == 0 {
        return VelloStatus::Success;
    }

    VelloStatus::Unsupported
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_text_shape_destroy(handle: *mut VelloShapedRunHandle) {
    if !handle.is_null() {
        unsafe { drop(Box::from_raw(handle)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_text_segments_destroy(handle: *mut VelloScriptSegmentHandle) {
    if !handle.is_null() {
        unsafe { drop(Box::from_raw(handle)) };
    }
}

fn load_svg_from_str(contents: &str) -> Result<VelloSvgHandle, String> {
    let options = usvg::Options::default();
    let tree = usvg::Tree::from_str(contents, &options).map_err(|err| err.to_string())?;

    let mut scene = Scene::new();
    vello_svg::append_tree(&mut scene, &tree);

    let size = tree.size();
    let resolution = Vec2::new(size.width() as f64, size.height() as f64);

    Ok(VelloSvgHandle { scene, resolution })
}

fn load_velato_composition_from_slice(
    bytes: &[u8],
) -> Result<VelloVelatoCompositionHandle, String> {
    let composition = VelatoComposition::from_slice(bytes).map_err(|err| err.to_string())?;
    Ok(VelloVelatoCompositionHandle { composition })
}

fn wgpu_backends_from_bits(bits: u32) -> Result<wgpu::Backends, VelloStatus> {
    if bits == 0 {
        return Ok(wgpu::Backends::PRIMARY);
    }
    let mut backends = wgpu::Backends::empty();
    if bits & 0x1 != 0 {
        backends |= wgpu::Backends::VULKAN;
    }
    if bits & 0x2 != 0 {
        backends |= wgpu::Backends::GL;
    }
    if bits & 0x4 != 0 {
        backends |= wgpu::Backends::METAL;
    }
    if bits & 0x8 != 0 {
        backends |= wgpu::Backends::DX12;
    }
    if bits & 0x10 != 0 {
        backends |= wgpu::Backends::BROWSER_WEBGPU;
    }
    if backends.is_empty() {
        Err(VelloStatus::InvalidArgument)
    } else {
        Ok(backends)
    }
}

fn dx12_compiler_from_ffi(value: VelloWgpuDx12Compiler) -> Dx12Compiler {
    match value {
        VelloWgpuDx12Compiler::Default => Dx12Compiler::default(),
        VelloWgpuDx12Compiler::Fxc => Dx12Compiler::Fxc,
        VelloWgpuDx12Compiler::Dxc => Dx12Compiler::StaticDxc,
    }
}

fn power_preference_from_ffi(value: VelloWgpuPowerPreference) -> Option<PowerPreference> {
    match value {
        VelloWgpuPowerPreference::None => None,
        VelloWgpuPowerPreference::LowPower => Some(PowerPreference::LowPower),
        VelloWgpuPowerPreference::HighPerformance => Some(PowerPreference::HighPerformance),
    }
}

const FEATURE_MAPPINGS: &[(Features, u64)] = &[
    (Features::TEXTURE_ADAPTER_SPECIFIC_FORMAT_FEATURES, 1 << 0),
    (Features::TIMESTAMP_QUERY, 1 << 1),
    (Features::PIPELINE_STATISTICS_QUERY, 1 << 2),
    (Features::PUSH_CONSTANTS, 1 << 3),
    (Features::TEXTURE_COMPRESSION_BC, 1 << 4),
    (Features::TEXTURE_COMPRESSION_ETC2, 1 << 5),
    (Features::TEXTURE_COMPRESSION_ASTC, 1 << 6),
    (Features::INDIRECT_FIRST_INSTANCE, 1 << 7),
    (Features::MAPPABLE_PRIMARY_BUFFERS, 1 << 8),
    (Features::POLYGON_MODE_LINE, 1 << 19),
    (Features::CLEAR_TEXTURE, 1 << 23),
    (Features::PIPELINE_CACHE, 1 << 41),
];

fn features_from_bits(bits: u64) -> Result<Features, VelloStatus> {
    if bits == 0 {
        return Ok(Features::empty());
    }
    let mut remaining = bits;
    let mut features = Features::empty();
    for (feature, mask) in FEATURE_MAPPINGS {
        if bits & mask != 0 {
            features |= *feature;
            remaining &= !mask;
        }
    }
    if remaining != 0 {
        Err(VelloStatus::Unsupported)
    } else {
        Ok(features)
    }
}

fn features_to_bits(features: Features) -> u64 {
    let mut bits = 0u64;
    for (feature, mask) in FEATURE_MAPPINGS {
        if features.contains(*feature) {
            bits |= *mask;
        }
    }
    bits
}

fn limits_from_preset(preset: VelloWgpuLimitsPreset, adapter: &Adapter) -> Limits {
    match preset {
        VelloWgpuLimitsPreset::Default => Limits::default(),
        VelloWgpuLimitsPreset::DownlevelWebGl2 => Limits::downlevel_webgl2_defaults(),
        VelloWgpuLimitsPreset::DownlevelDefault => Limits::downlevel_defaults(),
        VelloWgpuLimitsPreset::AdapterDefault => adapter.limits(),
    }
}

fn texture_format_from_ffi(format: VelloWgpuTextureFormat) -> TextureFormat {
    match format {
        VelloWgpuTextureFormat::Rgba8Unorm => TextureFormat::Rgba8Unorm,
        VelloWgpuTextureFormat::Rgba8UnormSrgb => TextureFormat::Rgba8UnormSrgb,
        VelloWgpuTextureFormat::Bgra8Unorm => TextureFormat::Bgra8Unorm,
        VelloWgpuTextureFormat::Bgra8UnormSrgb => TextureFormat::Bgra8UnormSrgb,
        VelloWgpuTextureFormat::Rgba16Float => TextureFormat::Rgba16Float,
        VelloWgpuTextureFormat::R8Uint => TextureFormat::R8Uint,
    }
}

fn extent3d_from_ffi(extent: VelloWgpuExtent3d) -> wgpu::Extent3d {
    wgpu::Extent3d {
        width: extent.width,
        height: extent.height,
        depth_or_array_layers: extent.depth_or_array_layers,
    }
}

fn texture_dimension_from_ffi(dimension: VelloWgpuTextureDimension) -> wgpu::TextureDimension {
    match dimension {
        VelloWgpuTextureDimension::D1 => wgpu::TextureDimension::D1,
        VelloWgpuTextureDimension::D2 => wgpu::TextureDimension::D2,
        VelloWgpuTextureDimension::D3 => wgpu::TextureDimension::D3,
    }
}

fn address_mode_from_ffi(mode: VelloWgpuAddressMode) -> wgpu::AddressMode {
    match mode {
        VelloWgpuAddressMode::ClampToEdge => wgpu::AddressMode::ClampToEdge,
        VelloWgpuAddressMode::Repeat => wgpu::AddressMode::Repeat,
        VelloWgpuAddressMode::MirrorRepeat => wgpu::AddressMode::MirrorRepeat,
        VelloWgpuAddressMode::ClampToBorder => wgpu::AddressMode::ClampToBorder,
    }
}

fn filter_mode_from_ffi(mode: VelloWgpuFilterMode) -> wgpu::FilterMode {
    match mode {
        VelloWgpuFilterMode::Nearest => wgpu::FilterMode::Nearest,
        VelloWgpuFilterMode::Linear => wgpu::FilterMode::Linear,
    }
}

fn compare_function_from_ffi(func: VelloWgpuCompareFunction) -> Option<wgpu::CompareFunction> {
    match func {
        VelloWgpuCompareFunction::Undefined => None,
        VelloWgpuCompareFunction::Never => Some(wgpu::CompareFunction::Never),
        VelloWgpuCompareFunction::Less => Some(wgpu::CompareFunction::Less),
        VelloWgpuCompareFunction::LessEqual => Some(wgpu::CompareFunction::LessEqual),
        VelloWgpuCompareFunction::Equal => Some(wgpu::CompareFunction::Equal),
        VelloWgpuCompareFunction::Greater => Some(wgpu::CompareFunction::Greater),
        VelloWgpuCompareFunction::NotEqual => Some(wgpu::CompareFunction::NotEqual),
        VelloWgpuCompareFunction::GreaterEqual => Some(wgpu::CompareFunction::GreaterEqual),
        VelloWgpuCompareFunction::Always => Some(wgpu::CompareFunction::Always),
    }
}

fn compare_function_required(
    func: VelloWgpuCompareFunction,
) -> Result<wgpu::CompareFunction, VelloStatus> {
    compare_function_from_ffi(func).ok_or(VelloStatus::InvalidArgument)
}

fn stencil_operation_from_ffi(op: VelloWgpuStencilOperation) -> wgpu::StencilOperation {
    match op {
        VelloWgpuStencilOperation::Keep => wgpu::StencilOperation::Keep,
        VelloWgpuStencilOperation::Zero => wgpu::StencilOperation::Zero,
        VelloWgpuStencilOperation::Replace => wgpu::StencilOperation::Replace,
        VelloWgpuStencilOperation::Invert => wgpu::StencilOperation::Invert,
        VelloWgpuStencilOperation::IncrementClamp => wgpu::StencilOperation::IncrementClamp,
        VelloWgpuStencilOperation::DecrementClamp => wgpu::StencilOperation::DecrementClamp,
        VelloWgpuStencilOperation::IncrementWrap => wgpu::StencilOperation::IncrementWrap,
        VelloWgpuStencilOperation::DecrementWrap => wgpu::StencilOperation::DecrementWrap,
    }
}

fn stencil_face_state_from_ffi(
    state: VelloWgpuStencilFaceState,
) -> Result<wgpu::StencilFaceState, VelloStatus> {
    Ok(wgpu::StencilFaceState {
        compare: compare_function_required(state.compare)?,
        fail_op: stencil_operation_from_ffi(state.fail_op),
        depth_fail_op: stencil_operation_from_ffi(state.depth_fail_op),
        pass_op: stencil_operation_from_ffi(state.pass_op),
    })
}

#[allow(dead_code)]
fn color_from_ffi(color: VelloWgpuColor) -> wgpu::Color {
    wgpu::Color {
        r: color.r,
        g: color.g,
        b: color.b,
        a: color.a,
    }
}

#[allow(dead_code)]
fn color_load_op_from_ffi(
    load: VelloWgpuLoadOp,
    clear: VelloWgpuColor,
) -> Result<wgpu::LoadOp<wgpu::Color>, VelloStatus> {
    Ok(match load {
        VelloWgpuLoadOp::Load => wgpu::LoadOp::Load,
        VelloWgpuLoadOp::Clear => wgpu::LoadOp::Clear(color_from_ffi(clear)),
    })
}

#[allow(dead_code)]
fn float_load_op_from_ffi(
    load: VelloWgpuLoadOp,
    clear: f32,
) -> Result<wgpu::LoadOp<f32>, VelloStatus> {
    Ok(match load {
        VelloWgpuLoadOp::Load => wgpu::LoadOp::Load,
        VelloWgpuLoadOp::Clear => wgpu::LoadOp::Clear(clear),
    })
}

#[allow(dead_code)]
fn uint_load_op_from_ffi(
    load: VelloWgpuLoadOp,
    clear: u32,
) -> Result<wgpu::LoadOp<u32>, VelloStatus> {
    Ok(match load {
        VelloWgpuLoadOp::Load => wgpu::LoadOp::Load,
        VelloWgpuLoadOp::Clear => wgpu::LoadOp::Clear(clear),
    })
}

#[allow(dead_code)]
fn store_op_from_ffi(store: VelloWgpuStoreOp) -> wgpu::StoreOp {
    match store {
        VelloWgpuStoreOp::Store => wgpu::StoreOp::Store,
        VelloWgpuStoreOp::Discard => wgpu::StoreOp::Discard,
    }
}

fn texture_format_to_ffi(format: TextureFormat) -> Option<VelloWgpuTextureFormat> {
    match format {
        TextureFormat::Rgba8Unorm => Some(VelloWgpuTextureFormat::Rgba8Unorm),
        TextureFormat::Rgba8UnormSrgb => Some(VelloWgpuTextureFormat::Rgba8UnormSrgb),
        TextureFormat::Bgra8Unorm => Some(VelloWgpuTextureFormat::Bgra8Unorm),
        TextureFormat::Bgra8UnormSrgb => Some(VelloWgpuTextureFormat::Bgra8UnormSrgb),
        TextureFormat::Rgba16Float => Some(VelloWgpuTextureFormat::Rgba16Float),
        TextureFormat::R8Uint => Some(VelloWgpuTextureFormat::R8Uint),
        _ => None,
    }
}

fn texture_usage_from_bits(bits: u32) -> Result<TextureUsages, VelloStatus> {
    if bits == 0 {
        return Err(VelloStatus::InvalidArgument);
    }
    let mut usages = TextureUsages::empty();
    if bits & 0x1 != 0 {
        usages |= TextureUsages::COPY_SRC;
    }
    if bits & 0x2 != 0 {
        usages |= TextureUsages::COPY_DST;
    }
    if bits & 0x4 != 0 {
        usages |= TextureUsages::TEXTURE_BINDING;
    }
    if bits & 0x8 != 0 {
        usages |= TextureUsages::STORAGE_BINDING;
    }
    if bits & 0x10 != 0 {
        usages |= TextureUsages::RENDER_ATTACHMENT;
    }
    if usages.is_empty() {
        Err(VelloStatus::InvalidArgument)
    } else {
        Ok(usages)
    }
}

fn buffer_usage_from_bits(bits: u32) -> Result<wgpu::BufferUsages, VelloStatus> {
    if bits == 0 {
        return Err(VelloStatus::InvalidArgument);
    }
    let mut usage = wgpu::BufferUsages::empty();
    if bits & 0x1 != 0 {
        usage |= wgpu::BufferUsages::MAP_READ;
    }
    if bits & 0x2 != 0 {
        usage |= wgpu::BufferUsages::MAP_WRITE;
    }
    if bits & 0x4 != 0 {
        usage |= wgpu::BufferUsages::COPY_SRC;
    }
    if bits & 0x8 != 0 {
        usage |= wgpu::BufferUsages::COPY_DST;
    }
    if bits & 0x10 != 0 {
        usage |= wgpu::BufferUsages::INDEX;
    }
    if bits & 0x20 != 0 {
        usage |= wgpu::BufferUsages::VERTEX;
    }
    if bits & 0x40 != 0 {
        usage |= wgpu::BufferUsages::UNIFORM;
    }
    if bits & 0x80 != 0 {
        usage |= wgpu::BufferUsages::STORAGE;
    }
    if bits & 0x100 != 0 {
        usage |= wgpu::BufferUsages::INDIRECT;
    }
    if bits & 0x200 != 0 {
        usage |= wgpu::BufferUsages::QUERY_RESOLVE;
    }
    if usage.is_empty() {
        Err(VelloStatus::InvalidArgument)
    } else {
        Ok(usage)
    }
}

fn alpha_mode_from_ffi(alpha: VelloWgpuCompositeAlphaMode) -> CompositeAlphaMode {
    match alpha {
        VelloWgpuCompositeAlphaMode::Auto => CompositeAlphaMode::Auto,
        VelloWgpuCompositeAlphaMode::Opaque => CompositeAlphaMode::Opaque,
        VelloWgpuCompositeAlphaMode::Premultiplied => CompositeAlphaMode::PreMultiplied,
        VelloWgpuCompositeAlphaMode::PostMultiplied => CompositeAlphaMode::PostMultiplied,
        VelloWgpuCompositeAlphaMode::Inherit => CompositeAlphaMode::Inherit,
    }
}

fn texture_view_dimension_from_u32(
    value: u32,
) -> Result<Option<TextureViewDimension>, VelloStatus> {
    match value {
        0 => Ok(None),
        1 => Ok(Some(TextureViewDimension::D1)),
        2 => Ok(Some(TextureViewDimension::D2)),
        3 => Ok(Some(TextureViewDimension::D2Array)),
        4 => Ok(Some(TextureViewDimension::Cube)),
        5 => Ok(Some(TextureViewDimension::CubeArray)),
        6 => Ok(Some(TextureViewDimension::D3)),
        _ => Err(VelloStatus::InvalidArgument),
    }
}

fn texture_aspect_from_u32(value: u32) -> Result<wgpu::TextureAspect, VelloStatus> {
    match value {
        0 => Ok(TextureAspect::All),
        1 => Ok(TextureAspect::StencilOnly),
        2 => Ok(TextureAspect::DepthOnly),
        3 => Ok(TextureAspect::Plane0),
        4 => Ok(TextureAspect::Plane1),
        _ => Err(VelloStatus::InvalidArgument),
    }
}

fn instance_descriptor_from_ffi(
    descriptor: Option<&VelloWgpuInstanceDescriptor>,
) -> Result<InstanceDescriptor, VelloStatus> {
    let desc = descriptor.copied().unwrap_or_default();
    let backends = wgpu_backends_from_bits(desc.backends)?;
    let flags = InstanceFlags::from_bits(desc.flags)
        .unwrap_or_else(|| InstanceFlags::from_bits_truncate(desc.flags));
    let mut instance_descriptor = InstanceDescriptor::from_env_or_default();
    instance_descriptor.backends = backends;
    instance_descriptor.flags = flags;
    instance_descriptor.backend_options.dx12.shader_compiler =
        dx12_compiler_from_ffi(desc.dx12_shader_compiler);
    Ok(instance_descriptor)
}

fn label_from_ptr(label: *const c_char) -> Result<Option<String>, VelloStatus> {
    if label.is_null() {
        Ok(None)
    } else {
        match unsafe { CStr::from_ptr(label) }.to_str() {
            Ok(value) => Ok(Some(value.to_owned())),
            Err(_) => Err(VelloStatus::InvalidArgument),
        }
    }
}

fn entry_point_from_bytes(bytes: &VelloBytes) -> Result<Option<String>, VelloStatus> {
    if bytes.length == 0 {
        return Ok(None);
    }
    if bytes.data.is_null() {
        return Err(VelloStatus::InvalidArgument);
    }
    let slice = unsafe { slice::from_raw_parts(bytes.data, bytes.length) };
    match std::str::from_utf8(slice) {
        Ok(value) => Ok(Some(value.to_owned())),
        Err(_) => Err(VelloStatus::InvalidArgument),
    }
}

fn texture_view_descriptor_from_ffi(
    descriptor: Option<&VelloWgpuTextureViewDescriptor>,
) -> Result<TextureViewDescriptor<'static>, VelloStatus> {
    if let Some(desc) = descriptor {
        let format = texture_format_from_ffi(desc.format);
        let dimension = texture_view_dimension_from_u32(desc.dimension)?;
        let aspect = texture_aspect_from_u32(desc.aspect)?;
        let base_mip_level = desc.base_mip_level;
        let mip_level_count = if desc.mip_level_count == 0 {
            None
        } else {
            Some(desc.mip_level_count)
        };
        let base_array_layer = desc.base_array_layer;
        let array_layer_count = if desc.array_layer_count == 0 {
            None
        } else {
            Some(desc.array_layer_count)
        };
        Ok(TextureViewDescriptor {
            label: None,
            format: Some(format),
            dimension,
            usage: None,
            aspect,
            base_mip_level,
            mip_level_count,
            base_array_layer,
            array_layer_count,
        })
    } else {
        Ok(TextureViewDescriptor::default())
    }
}

fn shader_stages_from_bits(bits: u32) -> Result<wgpu::ShaderStages, VelloStatus> {
    wgpu::ShaderStages::from_bits(bits).ok_or(VelloStatus::InvalidArgument)
}

fn buffer_binding_type_from_u32(value: u32) -> Result<wgpu::BufferBindingType, VelloStatus> {
    match value {
        0 => Ok(wgpu::BufferBindingType::Uniform),
        1 => Ok(wgpu::BufferBindingType::Storage { read_only: false }),
        2 => Ok(wgpu::BufferBindingType::Storage { read_only: true }),
        _ => Err(VelloStatus::InvalidArgument),
    }
}

fn texture_sample_type_from_u32(value: u32) -> Result<wgpu::TextureSampleType, VelloStatus> {
    match value {
        0 => Ok(wgpu::TextureSampleType::Float { filterable: true }),
        1 => Ok(wgpu::TextureSampleType::Float { filterable: false }),
        2 => Ok(wgpu::TextureSampleType::Depth),
        3 => Ok(wgpu::TextureSampleType::Sint),
        4 => Ok(wgpu::TextureSampleType::Uint),
        _ => Err(VelloStatus::InvalidArgument),
    }
}

fn sampler_binding_type_from_u32(value: u32) -> Result<wgpu::SamplerBindingType, VelloStatus> {
    match value {
        0 => Ok(wgpu::SamplerBindingType::Filtering),
        1 => Ok(wgpu::SamplerBindingType::NonFiltering),
        2 => Ok(wgpu::SamplerBindingType::Comparison),
        _ => Err(VelloStatus::InvalidArgument),
    }
}

fn storage_texture_access_from_u32(value: u32) -> Result<wgpu::StorageTextureAccess, VelloStatus> {
    match value {
        0 => Ok(wgpu::StorageTextureAccess::ReadOnly),
        1 => Ok(wgpu::StorageTextureAccess::WriteOnly),
        2 => Ok(wgpu::StorageTextureAccess::ReadWrite),
        _ => Err(VelloStatus::InvalidArgument),
    }
}

fn bind_group_layout_entry_from_ffi(
    entry: &VelloWgpuBindGroupLayoutEntry,
) -> Result<wgpu::BindGroupLayoutEntry, VelloStatus> {
    let visibility = shader_stages_from_bits(entry.visibility)?;
    let binding_type = match entry.ty {
        0 => {
            let ty = buffer_binding_type_from_u32(entry.buffer_type)?;
            let min_binding_size = NonZeroU64::new(entry.min_binding_size);
            wgpu::BindingType::Buffer {
                ty,
                has_dynamic_offset: entry.has_dynamic_offset,
                min_binding_size,
            }
        }
        1 => wgpu::BindingType::Sampler(sampler_binding_type_from_u32(entry.sampler_type)?),
        2 => {
            let dimension = texture_view_dimension_from_u32(entry.texture_view_dimension)?
                .ok_or(VelloStatus::InvalidArgument)?;
            let sample_type = texture_sample_type_from_u32(entry.texture_sample_type)?;
            wgpu::BindingType::Texture {
                sample_type,
                view_dimension: dimension,
                multisampled: entry.texture_multisampled,
            }
        }
        3 => {
            let dimension = texture_view_dimension_from_u32(entry.texture_view_dimension)?
                .ok_or(VelloStatus::InvalidArgument)?;
            let access = storage_texture_access_from_u32(entry.storage_texture_access)?;
            let format = texture_format_from_ffi(entry.storage_texture_format);
            wgpu::BindingType::StorageTexture {
                access,
                format,
                view_dimension: dimension,
            }
        }
        _ => return Err(VelloStatus::InvalidArgument),
    };
    Ok(wgpu::BindGroupLayoutEntry {
        binding: entry.binding,
        visibility,
        ty: binding_type,
        count: None,
    })
}

fn vertex_format_from_u32(value: u32) -> Result<wgpu::VertexFormat, VelloStatus> {
    match value {
        0 => Ok(wgpu::VertexFormat::Float32),
        1 => Ok(wgpu::VertexFormat::Float32x2),
        2 => Ok(wgpu::VertexFormat::Float32x3),
        3 => Ok(wgpu::VertexFormat::Float32x4),
        4 => Ok(wgpu::VertexFormat::Uint32),
        5 => Ok(wgpu::VertexFormat::Uint32x2),
        6 => Ok(wgpu::VertexFormat::Uint32x3),
        7 => Ok(wgpu::VertexFormat::Uint32x4),
        8 => Ok(wgpu::VertexFormat::Sint32),
        9 => Ok(wgpu::VertexFormat::Sint32x2),
        10 => Ok(wgpu::VertexFormat::Sint32x3),
        11 => Ok(wgpu::VertexFormat::Sint32x4),
        12 => Ok(wgpu::VertexFormat::Float16x2),
        13 => Ok(wgpu::VertexFormat::Float16x4),
        _ => Err(VelloStatus::InvalidArgument),
    }
}

fn vertex_step_mode_from_u32(value: u32) -> Result<wgpu::VertexStepMode, VelloStatus> {
    match value {
        0 => Ok(wgpu::VertexStepMode::Vertex),
        1 => Ok(wgpu::VertexStepMode::Instance),
        _ => Err(VelloStatus::InvalidArgument),
    }
}

fn index_format_from_u32(value: u32) -> Result<Option<wgpu::IndexFormat>, VelloStatus> {
    match value {
        0 => Ok(None),
        1 => Ok(Some(wgpu::IndexFormat::Uint16)),
        2 => Ok(Some(wgpu::IndexFormat::Uint32)),
        _ => Err(VelloStatus::InvalidArgument),
    }
}

fn primitive_topology_from_u32(value: u32) -> Result<wgpu::PrimitiveTopology, VelloStatus> {
    match value {
        0 => Ok(wgpu::PrimitiveTopology::PointList),
        1 => Ok(wgpu::PrimitiveTopology::LineList),
        2 => Ok(wgpu::PrimitiveTopology::LineStrip),
        3 => Ok(wgpu::PrimitiveTopology::TriangleList),
        4 => Ok(wgpu::PrimitiveTopology::TriangleStrip),
        _ => Err(VelloStatus::InvalidArgument),
    }
}

fn front_face_from_u32(value: u32) -> Result<wgpu::FrontFace, VelloStatus> {
    match value {
        0 => Ok(wgpu::FrontFace::Ccw),
        1 => Ok(wgpu::FrontFace::Cw),
        _ => Err(VelloStatus::InvalidArgument),
    }
}

fn cull_mode_from_u32(value: u32) -> Result<Option<wgpu::Face>, VelloStatus> {
    match value {
        0 => Ok(None),
        1 => Ok(Some(wgpu::Face::Front)),
        2 => Ok(Some(wgpu::Face::Back)),
        _ => Err(VelloStatus::InvalidArgument),
    }
}

fn polygon_mode_from_u32(value: u32) -> Result<wgpu::PolygonMode, VelloStatus> {
    match value {
        0 => Ok(wgpu::PolygonMode::Fill),
        1 => Ok(wgpu::PolygonMode::Line),
        2 => Ok(wgpu::PolygonMode::Point),
        _ => Err(VelloStatus::InvalidArgument),
    }
}

fn blend_factor_from_u32(value: u32) -> Result<wgpu::BlendFactor, VelloStatus> {
    match value {
        0 => Ok(wgpu::BlendFactor::Zero),
        1 => Ok(wgpu::BlendFactor::One),
        2 => Ok(wgpu::BlendFactor::Src),
        3 => Ok(wgpu::BlendFactor::OneMinusSrc),
        4 => Ok(wgpu::BlendFactor::SrcAlpha),
        5 => Ok(wgpu::BlendFactor::OneMinusSrcAlpha),
        6 => Ok(wgpu::BlendFactor::Dst),
        7 => Ok(wgpu::BlendFactor::OneMinusDst),
        8 => Ok(wgpu::BlendFactor::DstAlpha),
        9 => Ok(wgpu::BlendFactor::OneMinusDstAlpha),
        10 => Ok(wgpu::BlendFactor::SrcAlphaSaturated),
        11 => Ok(wgpu::BlendFactor::Constant),
        12 => Ok(wgpu::BlendFactor::OneMinusConstant),
        _ => Err(VelloStatus::InvalidArgument),
    }
}

fn blend_operation_from_u32(value: u32) -> Result<wgpu::BlendOperation, VelloStatus> {
    match value {
        0 => Ok(wgpu::BlendOperation::Add),
        1 => Ok(wgpu::BlendOperation::Subtract),
        2 => Ok(wgpu::BlendOperation::ReverseSubtract),
        3 => Ok(wgpu::BlendOperation::Min),
        4 => Ok(wgpu::BlendOperation::Max),
        _ => Err(VelloStatus::InvalidArgument),
    }
}

fn blend_component_from_ffi(
    component: &VelloWgpuBlendComponent,
) -> Result<wgpu::BlendComponent, VelloStatus> {
    Ok(wgpu::BlendComponent {
        src_factor: blend_factor_from_u32(component.src_factor)?,
        dst_factor: blend_factor_from_u32(component.dst_factor)?,
        operation: blend_operation_from_u32(component.operation)?,
    })
}

fn color_write_mask_from_u32(value: u32) -> Result<wgpu::ColorWrites, VelloStatus> {
    wgpu::ColorWrites::from_bits(value).ok_or(VelloStatus::InvalidArgument)
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_instance_create(
    descriptor: *const VelloWgpuInstanceDescriptor,
) -> *mut VelloWgpuInstanceHandle {
    clear_last_error();
    let descriptor = unsafe { descriptor.as_ref() };
    let instance_descriptor = match instance_descriptor_from_ffi(descriptor) {
        Ok(value) => value,
        Err(status) => {
            set_last_error(format!("Invalid instance descriptor: {:?}", status));
            return std::ptr::null_mut();
        }
    };
    let instance = Instance::new(&instance_descriptor);
    Box::into_raw(Box::new(VelloWgpuInstanceHandle { instance }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_instance_destroy(instance: *mut VelloWgpuInstanceHandle) {
    if !instance.is_null() {
        unsafe { drop(Box::from_raw(instance)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_instance_request_adapter(
    instance: *mut VelloWgpuInstanceHandle,
    options: *const VelloWgpuRequestAdapterOptions,
) -> *mut VelloWgpuAdapterHandle {
    clear_last_error();
    let Some(instance) = (unsafe { instance.as_ref() }) else {
        set_last_error("Instance pointer is null");
        return std::ptr::null_mut();
    };
    let opts = unsafe { options.as_ref() };
    let opts_value = opts.copied().unwrap_or(VelloWgpuRequestAdapterOptions {
        power_preference: VelloWgpuPowerPreference::None,
        force_fallback_adapter: false,
        compatible_surface: std::ptr::null(),
    });
    let compatible_surface = if opts_value.compatible_surface.is_null() {
        None
    } else {
        Some(unsafe { &(*opts_value.compatible_surface).surface })
    };
    let power_preference = power_preference_from_ffi(opts_value.power_preference)
        .unwrap_or(PowerPreference::default());
    let request = RequestAdapterOptions {
        power_preference,
        compatible_surface,
        force_fallback_adapter: opts_value.force_fallback_adapter,
    };
    match pollster::block_on(instance.instance.request_adapter(&request)) {
        Ok(adapter) => Box::into_raw(Box::new(VelloWgpuAdapterHandle { adapter })),
        Err(err) => {
            set_last_error(format!("No suitable adapter found: {err}"));
            std::ptr::null_mut()
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_adapter_destroy(adapter: *mut VelloWgpuAdapterHandle) {
    if !adapter.is_null() {
        unsafe { drop(Box::from_raw(adapter)) };
    }
}

fn backend_to_ffi(backend: Backend) -> VelloWgpuBackendType {
    match backend {
        Backend::Noop => VelloWgpuBackendType::Noop,
        Backend::Vulkan => VelloWgpuBackendType::Vulkan,
        Backend::Metal => VelloWgpuBackendType::Metal,
        Backend::Dx12 => VelloWgpuBackendType::Dx12,
        Backend::Gl => VelloWgpuBackendType::Gl,
        Backend::BrowserWebGpu => VelloWgpuBackendType::BrowserWebGpu,
    }
}

fn device_type_to_ffi(device_type: wgpu::DeviceType) -> VelloWgpuDeviceType {
    match device_type {
        wgpu::DeviceType::Other => VelloWgpuDeviceType::Other,
        wgpu::DeviceType::IntegratedGpu => VelloWgpuDeviceType::IntegratedGpu,
        wgpu::DeviceType::DiscreteGpu => VelloWgpuDeviceType::DiscreteGpu,
        wgpu::DeviceType::VirtualGpu => VelloWgpuDeviceType::VirtualGpu,
        wgpu::DeviceType::Cpu => VelloWgpuDeviceType::Cpu,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_adapter_get_info(
    adapter: *mut VelloWgpuAdapterHandle,
    out_info: *mut VelloWgpuAdapterInfo,
) -> VelloStatus {
    clear_last_error();
    let Some(adapter) = (unsafe { adapter.as_ref() }) else {
        return VelloStatus::NullPointer;
    };
    if out_info.is_null() {
        return VelloStatus::NullPointer;
    }
    let info = adapter.adapter.get_info();
    unsafe {
        *out_info = VelloWgpuAdapterInfo {
            vendor: info.vendor,
            device: info.device,
            backend: backend_to_ffi(info.backend),
            device_type: device_type_to_ffi(info.device_type),
        };
    }
    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_adapter_get_features(
    adapter: *mut VelloWgpuAdapterHandle,
    out_features: *mut u64,
) -> VelloStatus {
    clear_last_error();
    let Some(adapter) = (unsafe { adapter.as_ref() }) else {
        return VelloStatus::NullPointer;
    };
    if out_features.is_null() {
        return VelloStatus::NullPointer;
    }
    let features = adapter.adapter.features();
    unsafe {
        *out_features = features_to_bits(features);
    }
    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_adapter_request_device(
    adapter: *mut VelloWgpuAdapterHandle,
    descriptor: *const VelloWgpuDeviceDescriptor,
) -> *mut VelloWgpuDeviceHandle {
    clear_last_error();
    let Some(adapter) = (unsafe { adapter.as_ref() }) else {
        set_last_error("Adapter pointer is null");
        return std::ptr::null_mut();
    };
    let desc = unsafe { descriptor.as_ref() };
    let desc_value = desc.copied().unwrap_or_default();
    let label = if desc_value.label.is_null() {
        None
    } else {
        match unsafe { CStr::from_ptr(desc_value.label) }.to_str() {
            Ok(value) => Some(value),
            Err(err) => {
                set_last_error(format!("Device label is not valid UTF-8: {err}"));
                return std::ptr::null_mut();
            }
        }
    };

    let required_features = match features_from_bits(desc_value.required_features) {
        Ok(features) => features,
        Err(_) => {
            set_last_error("Unsupported feature request");
            return std::ptr::null_mut();
        }
    };

    let required_limits = limits_from_preset(desc_value.limits, &adapter.adapter);

    let descriptor = wgpu::DeviceDescriptor {
        label,
        required_features,
        required_limits,
        memory_hints: wgpu::MemoryHints::default(),
        trace: Default::default(),
    };

    match pollster::block_on(adapter.adapter.request_device(&descriptor)) {
        Ok((device, queue)) => Box::into_raw(Box::new(VelloWgpuDeviceHandle { device, queue })),
        Err(err) => {
            set_last_error(format!("Failed to create device: {err}"));
            std::ptr::null_mut()
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_device_destroy(device: *mut VelloWgpuDeviceHandle) {
    if !device.is_null() {
        unsafe { drop(Box::from_raw(device)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_device_get_features(
    device: *mut VelloWgpuDeviceHandle,
    out_features: *mut u64,
) -> VelloStatus {
    clear_last_error();
    let Some(device) = (unsafe { device.as_ref() }) else {
        return VelloStatus::NullPointer;
    };
    if out_features.is_null() {
        return VelloStatus::NullPointer;
    }
    let features = device.device.features();
    unsafe {
        *out_features = features_to_bits(features);
    }
    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_device_get_queue(
    device: *mut VelloWgpuDeviceHandle,
) -> *mut VelloWgpuQueueHandle {
    clear_last_error();
    let Some(device) = (unsafe { device.as_ref() }) else {
        set_last_error("Device pointer is null");
        return std::ptr::null_mut();
    };
    Box::into_raw(Box::new(VelloWgpuQueueHandle {
        queue: device.queue.clone(),
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_device_create_pipeline_cache(
    device: *mut VelloWgpuDeviceHandle,
    descriptor: *const VelloWgpuPipelineCacheDescriptor,
) -> *mut VelloWgpuPipelineCacheHandle {
    clear_last_error();
    let Some(device) = (unsafe { device.as_ref() }) else {
        set_last_error("Device pointer is null");
        return std::ptr::null_mut();
    };
    let Some(desc) = (unsafe { descriptor.as_ref() }) else {
        set_last_error("Pipeline cache descriptor is null");
        return std::ptr::null_mut();
    };
    let label = if desc.label.is_null() {
        None
    } else {
        match unsafe { CStr::from_ptr(desc.label) }.to_str() {
            Ok(value) => Some(value.to_owned()),
            Err(err) => {
                set_last_error(format!("Pipeline cache label is not valid UTF-8: {err}"));
                return std::ptr::null_mut();
            }
        }
    };
    let data = if desc.data.is_null() || desc.data_len == 0 {
        None
    } else {
        Some(unsafe { slice::from_raw_parts(desc.data, desc.data_len) })
    };
    let descriptor = wgpu::PipelineCacheDescriptor {
        label: label.as_deref(),
        data,
        fallback: desc.fallback,
    };
    let cache = unsafe { device.device.create_pipeline_cache(&descriptor) };
    Box::into_raw(Box::new(VelloWgpuPipelineCacheHandle { cache }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_pipeline_cache_destroy(
    cache: *mut VelloWgpuPipelineCacheHandle,
) {
    if !cache.is_null() {
        unsafe { drop(Box::from_raw(cache)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_pipeline_cache_get_data(
    cache: *mut VelloWgpuPipelineCacheHandle,
    out_data: *mut *const u8,
    out_len: *mut usize,
) -> VelloStatus {
    clear_last_error();
    let Some(cache) = (unsafe { cache.as_ref() }) else {
        return VelloStatus::NullPointer;
    };
    if out_data.is_null() || out_len.is_null() {
        return VelloStatus::NullPointer;
    }
    match cache.cache.get_data() {
        Some(data) => {
            let boxed = data.into_boxed_slice();
            let len = boxed.len();
            let ptr = boxed.as_ptr();
            std::mem::forget(boxed);
            unsafe {
                *out_data = ptr;
                *out_len = len;
            }
        }
        None => unsafe {
            *out_data = std::ptr::null();
            *out_len = 0;
        },
    }
    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_pipeline_cache_free_data(data: *const u8, len: usize) {
    if !data.is_null() && len != 0 {
        unsafe {
            let _ = Vec::from_raw_parts(data as *mut u8, len, len);
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_device_create_shader_module(
    device: *mut VelloWgpuDeviceHandle,
    descriptor: *const VelloWgpuShaderModuleDescriptor,
) -> *mut VelloWgpuShaderModuleHandle {
    clear_last_error();
    let Some(device) = (unsafe { device.as_ref() }) else {
        set_last_error("Device pointer is null");
        return std::ptr::null_mut();
    };
    let Some(desc) = (unsafe { descriptor.as_ref() }) else {
        set_last_error("Shader module descriptor is null");
        return std::ptr::null_mut();
    };

    let label_storage = match label_from_ptr(desc.label) {
        Ok(label) => label,
        Err(_) => {
            set_last_error("Shader module label is not valid UTF-8");
            return std::ptr::null_mut();
        }
    };
    let label = label_storage.as_deref();

    let shader_source = match desc.source_kind {
        VelloWgpuShaderSourceKind::Wgsl => {
            if desc.source_wgsl.data.is_null() || desc.source_wgsl.length == 0 {
                set_last_error("WGSL source is empty");
                return std::ptr::null_mut();
            }
            let bytes =
                unsafe { slice::from_raw_parts(desc.source_wgsl.data, desc.source_wgsl.length) };
            let source = match std::str::from_utf8(bytes) {
                Ok(value) => value.to_owned(),
                Err(err) => {
                    set_last_error(format!("WGSL source is not valid UTF-8: {err}"));
                    return std::ptr::null_mut();
                }
            };
            wgpu::ShaderSource::Wgsl(Cow::Owned(source))
        }
        VelloWgpuShaderSourceKind::Spirv => {
            set_last_error("SPIR-V shaders are not supported in this build");
            return std::ptr::null_mut();
        }
    };

    let module_descriptor = wgpu::ShaderModuleDescriptor {
        label,
        source: shader_source,
    }; // label lifetime tied to storage
    let module = device.device.create_shader_module(module_descriptor);
    Box::into_raw(Box::new(VelloWgpuShaderModuleHandle { module }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_shader_module_destroy(
    module: *mut VelloWgpuShaderModuleHandle,
) {
    if !module.is_null() {
        unsafe { drop(Box::from_raw(module)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_device_create_buffer(
    device: *mut VelloWgpuDeviceHandle,
    descriptor: *const VelloWgpuBufferDescriptor,
) -> *mut VelloWgpuBufferHandle {
    clear_last_error();
    let Some(device) = (unsafe { device.as_ref() }) else {
        set_last_error("Device pointer is null");
        return std::ptr::null_mut();
    };
    let Some(desc) = (unsafe { descriptor.as_ref() }) else {
        set_last_error("Buffer descriptor is null");
        return std::ptr::null_mut();
    };
    if desc.size == 0 {
        set_last_error("Buffer size must be greater than zero");
        return std::ptr::null_mut();
    }
    if !desc.initial_data.data.is_null() && desc.initial_data.length > 0 {
        set_last_error("Buffer initial data is not supported; use queue_write_buffer");
        return std::ptr::null_mut();
    }
    let usage = match buffer_usage_from_bits(desc.usage) {
        Ok(usage) => usage,
        Err(_) => {
            set_last_error("Unsupported buffer usage flags");
            return std::ptr::null_mut();
        }
    };
    let label_storage = match label_from_ptr(desc.label) {
        Ok(label) => label,
        Err(_) => {
            set_last_error("Buffer label is not valid UTF-8");
            return std::ptr::null_mut();
        }
    };
    let descriptor = wgpu::BufferDescriptor {
        label: label_storage.as_deref(),
        size: desc.size,
        usage,
        mapped_at_creation: desc.mapped_at_creation,
    };
    let buffer = device.device.create_buffer(&descriptor);
    Box::into_raw(Box::new(VelloWgpuBufferHandle {
        buffer,
        size: desc.size,
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_buffer_destroy(buffer: *mut VelloWgpuBufferHandle) {
    if !buffer.is_null() {
        unsafe { drop(Box::from_raw(buffer)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_buffer_get_size(buffer: *const VelloWgpuBufferHandle) -> u64 {
    if let Some(buffer) = unsafe { buffer.as_ref() } {
        buffer.size
    } else {
        0
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_device_create_sampler(
    device: *mut VelloWgpuDeviceHandle,
    descriptor: *const VelloWgpuSamplerDescriptor,
) -> *mut VelloWgpuSamplerHandle {
    clear_last_error();
    let Some(device) = (unsafe { device.as_ref() }) else {
        set_last_error("Device pointer is null");
        return std::ptr::null_mut();
    };
    let Some(desc) = (unsafe { descriptor.as_ref() }) else {
        set_last_error("Sampler descriptor is null");
        return std::ptr::null_mut();
    };
    let label_storage = match label_from_ptr(desc.label) {
        Ok(label) => label,
        Err(_) => {
            set_last_error("Sampler label is not valid UTF-8");
            return std::ptr::null_mut();
        }
    };
    let descriptor = wgpu::SamplerDescriptor {
        label: label_storage.as_deref(),
        address_mode_u: address_mode_from_ffi(desc.address_mode_u),
        address_mode_v: address_mode_from_ffi(desc.address_mode_v),
        address_mode_w: address_mode_from_ffi(desc.address_mode_w),
        mag_filter: filter_mode_from_ffi(desc.mag_filter),
        min_filter: filter_mode_from_ffi(desc.min_filter),
        mipmap_filter: filter_mode_from_ffi(desc.mip_filter),
        lod_min_clamp: desc.lod_min_clamp,
        lod_max_clamp: desc.lod_max_clamp,
        compare: compare_function_from_ffi(desc.compare),
        anisotropy_clamp: desc.anisotropy_clamp.max(1),
        border_color: None,
    };
    let sampler = device.device.create_sampler(&descriptor);
    Box::into_raw(Box::new(VelloWgpuSamplerHandle { sampler }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_sampler_destroy(sampler: *mut VelloWgpuSamplerHandle) {
    if !sampler.is_null() {
        unsafe { drop(Box::from_raw(sampler)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_device_create_texture(
    device: *mut VelloWgpuDeviceHandle,
    descriptor: *const VelloWgpuTextureDescriptor,
) -> *mut VelloWgpuTextureHandle {
    clear_last_error();
    let Some(device) = (unsafe { device.as_ref() }) else {
        set_last_error("Device pointer is null");
        return std::ptr::null_mut();
    };
    let Some(desc) = (unsafe { descriptor.as_ref() }) else {
        set_last_error("Texture descriptor is null");
        return std::ptr::null_mut();
    };
    if desc.size.width == 0 || desc.size.height == 0 {
        set_last_error("Texture dimensions must be greater than zero");
        return std::ptr::null_mut();
    }
    let usage = match texture_usage_from_bits(desc.usage) {
        Ok(usage) => usage,
        Err(_) => {
            set_last_error("Unsupported texture usage flags");
            return std::ptr::null_mut();
        }
    };
    let label_storage = match label_from_ptr(desc.label) {
        Ok(label) => label,
        Err(_) => {
            set_last_error("Texture label is not valid UTF-8");
            return std::ptr::null_mut();
        }
    };
    let view_formats = if desc.view_format_count > 0 && !desc.view_formats.is_null() {
        unsafe { std::slice::from_raw_parts(desc.view_formats, desc.view_format_count) }
            .iter()
            .map(|format| texture_format_from_ffi(*format))
            .collect::<Vec<_>>()
    } else {
        Vec::new()
    };
    let descriptor = wgpu::TextureDescriptor {
        label: label_storage.as_deref(),
        size: extent3d_from_ffi(desc.size),
        mip_level_count: desc.mip_level_count,
        sample_count: desc.sample_count,
        dimension: texture_dimension_from_ffi(desc.dimension),
        format: texture_format_from_ffi(desc.format),
        usage,
        view_formats: &view_formats,
    };
    let texture = device.device.create_texture(&descriptor);
    Box::into_raw(Box::new(VelloWgpuTextureHandle { texture }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_texture_destroy(texture: *mut VelloWgpuTextureHandle) {
    if !texture.is_null() {
        unsafe { drop(Box::from_raw(texture)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_texture_create_view(
    texture: *mut VelloWgpuTextureHandle,
    descriptor: *const VelloWgpuTextureViewDescriptor,
) -> *mut VelloWgpuTextureViewHandle {
    clear_last_error();
    let Some(texture) = (unsafe { texture.as_ref() }) else {
        set_last_error("Texture pointer is null");
        return std::ptr::null_mut();
    };
    let descriptor = match texture_view_descriptor_from_ffi(unsafe { descriptor.as_ref() }) {
        Ok(desc) => desc,
        Err(err) => {
            set_last_error(format!("Invalid texture view descriptor: {err:?}"));
            return std::ptr::null_mut();
        }
    };
    let view = texture.texture.create_view(&descriptor);
    Box::into_raw(Box::new(VelloWgpuTextureViewHandle { view }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_queue_destroy(queue: *mut VelloWgpuQueueHandle) {
    if !queue.is_null() {
        unsafe { drop(Box::from_raw(queue)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_device_create_bind_group_layout(
    device: *mut VelloWgpuDeviceHandle,
    descriptor: *const VelloWgpuBindGroupLayoutDescriptor,
) -> *mut VelloWgpuBindGroupLayoutHandle {
    clear_last_error();
    let Some(device) = (unsafe { device.as_ref() }) else {
        set_last_error("Device pointer is null");
        return std::ptr::null_mut();
    };
    let Some(desc) = (unsafe { descriptor.as_ref() }) else {
        set_last_error("Bind group layout descriptor is null");
        return std::ptr::null_mut();
    };
    let entries_slice = if desc.entry_count == 0 {
        &[][..]
    } else if desc.entries.is_null() {
        set_last_error("Bind group layout entries pointer is null");
        return std::ptr::null_mut();
    } else {
        unsafe { slice::from_raw_parts(desc.entries, desc.entry_count) }
    };
    let mut converted_entries = Vec::with_capacity(entries_slice.len());
    for entry in entries_slice {
        match bind_group_layout_entry_from_ffi(entry) {
            Ok(converted) => converted_entries.push(converted),
            Err(_) => {
                set_last_error("Invalid bind group layout entry");
                return std::ptr::null_mut();
            }
        }
    }
    let label_storage = match label_from_ptr(desc.label) {
        Ok(label) => label,
        Err(_) => {
            set_last_error("Bind group layout label is not valid UTF-8");
            return std::ptr::null_mut();
        }
    };
    let descriptor = wgpu::BindGroupLayoutDescriptor {
        label: label_storage.as_deref(),
        entries: &converted_entries,
    };
    let layout = device.device.create_bind_group_layout(&descriptor);
    Box::into_raw(Box::new(VelloWgpuBindGroupLayoutHandle { layout }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_bind_group_layout_destroy(
    layout: *mut VelloWgpuBindGroupLayoutHandle,
) {
    if !layout.is_null() {
        unsafe { drop(Box::from_raw(layout)) };
    }
}

fn bind_group_entry_resource_from_ffi(
    entry: &VelloWgpuBindGroupEntry,
) -> Result<wgpu::BindingResource<'static>, VelloStatus> {
    if let Some(buffer) = unsafe { entry.buffer.as_ref() } {
        let size = if entry.size == 0 {
            None
        } else {
            NonZeroU64::new(entry.size)
        };
        Ok(wgpu::BindingResource::Buffer(wgpu::BufferBinding {
            buffer: &buffer.buffer,
            offset: entry.offset,
            size,
        }))
    } else if let Some(sampler) = unsafe { entry.sampler.as_ref() } {
        Ok(wgpu::BindingResource::Sampler(&sampler.sampler))
    } else if let Some(view) = unsafe { entry.texture_view.as_ref() } {
        Ok(wgpu::BindingResource::TextureView(&view.view))
    } else {
        Err(VelloStatus::InvalidArgument)
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_device_create_bind_group(
    device: *mut VelloWgpuDeviceHandle,
    descriptor: *const VelloWgpuBindGroupDescriptor,
) -> *mut VelloWgpuBindGroupHandle {
    clear_last_error();
    let Some(device) = (unsafe { device.as_ref() }) else {
        set_last_error("Device pointer is null");
        return std::ptr::null_mut();
    };
    let Some(desc) = (unsafe { descriptor.as_ref() }) else {
        set_last_error("Bind group descriptor is null");
        return std::ptr::null_mut();
    };
    let Some(layout_handle) = (unsafe { desc.layout.as_ref() }) else {
        set_last_error("Bind group layout pointer is null");
        return std::ptr::null_mut();
    };
    let entries_slice = if desc.entry_count == 0 {
        &[][..]
    } else if desc.entries.is_null() {
        set_last_error("Bind group entries pointer is null");
        return std::ptr::null_mut();
    } else {
        unsafe { slice::from_raw_parts(desc.entries, desc.entry_count) }
    };
    let mut converted_entries = Vec::with_capacity(entries_slice.len());
    for entry in entries_slice {
        match bind_group_entry_resource_from_ffi(entry) {
            Ok(resource) => converted_entries.push(wgpu::BindGroupEntry {
                binding: entry.binding,
                resource,
            }),
            Err(_) => {
                set_last_error("Invalid bind group entry");
                return std::ptr::null_mut();
            }
        }
    }
    let label_storage = match label_from_ptr(desc.label) {
        Ok(label) => label,
        Err(_) => {
            set_last_error("Bind group label is not valid UTF-8");
            return std::ptr::null_mut();
        }
    };
    let descriptor = wgpu::BindGroupDescriptor {
        label: label_storage.as_deref(),
        layout: &layout_handle.layout,
        entries: &converted_entries,
    };
    let bind_group = device.device.create_bind_group(&descriptor);
    Box::into_raw(Box::new(VelloWgpuBindGroupHandle { bind_group }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_bind_group_destroy(bind_group: *mut VelloWgpuBindGroupHandle) {
    if !bind_group.is_null() {
        unsafe { drop(Box::from_raw(bind_group)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_device_create_pipeline_layout(
    device: *mut VelloWgpuDeviceHandle,
    descriptor: *const VelloWgpuPipelineLayoutDescriptor,
) -> *mut VelloWgpuPipelineLayoutHandle {
    clear_last_error();
    let Some(device) = (unsafe { device.as_ref() }) else {
        set_last_error("Device pointer is null");
        return std::ptr::null_mut();
    };
    let Some(desc) = (unsafe { descriptor.as_ref() }) else {
        set_last_error("Pipeline layout descriptor is null");
        return std::ptr::null_mut();
    };
    let layout_ptrs = if desc.bind_group_layout_count == 0 {
        &[][..]
    } else if desc.bind_group_layouts.is_null() {
        set_last_error("Pipeline layout bind group layouts pointer is null");
        return std::ptr::null_mut();
    } else {
        unsafe { slice::from_raw_parts(desc.bind_group_layouts, desc.bind_group_layout_count) }
    };
    let mut layouts = Vec::with_capacity(layout_ptrs.len());
    for &layout_ptr in layout_ptrs {
        let layout_handle = match unsafe { layout_ptr.as_ref() } {
            Some(layout) => layout,
            None => {
                set_last_error("Bind group layout pointer is null");
                return std::ptr::null_mut();
            }
        };
        layouts.push(&layout_handle.layout);
    }
    let label_storage = match label_from_ptr(desc.label) {
        Ok(label) => label,
        Err(_) => {
            set_last_error("Pipeline layout label is not valid UTF-8");
            return std::ptr::null_mut();
        }
    };
    let descriptor = wgpu::PipelineLayoutDescriptor {
        label: label_storage.as_deref(),
        bind_group_layouts: &layouts,
        push_constant_ranges: &[],
    };
    let layout = device.device.create_pipeline_layout(&descriptor);
    Box::into_raw(Box::new(VelloWgpuPipelineLayoutHandle { layout }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_pipeline_layout_destroy(
    layout: *mut VelloWgpuPipelineLayoutHandle,
) {
    if !layout.is_null() {
        unsafe { drop(Box::from_raw(layout)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_device_create_render_pipeline(
    device: *mut VelloWgpuDeviceHandle,
    descriptor: *const VelloWgpuRenderPipelineDescriptor,
) -> *mut VelloWgpuRenderPipelineHandle {
    clear_last_error();
    let Some(device) = (unsafe { device.as_ref() }) else {
        set_last_error("Device pointer is null");
        return std::ptr::null_mut();
    };
    let Some(desc) = (unsafe { descriptor.as_ref() }) else {
        set_last_error("Render pipeline descriptor is null");
        return std::ptr::null_mut();
    };

    let label_storage = match label_from_ptr(desc.label) {
        Ok(label) => label,
        Err(_) => {
            set_last_error("Render pipeline label is not valid UTF-8");
            return std::ptr::null_mut();
        }
    };

    let layout_ref = if desc.layout.is_null() {
        None
    } else {
        match unsafe { desc.layout.as_ref() } {
            Some(layout) => Some(&layout.layout),
            None => {
                set_last_error("Pipeline layout pointer is null");
                return std::ptr::null_mut();
            }
        }
    };

    let vertex_module = match unsafe { desc.vertex.module.as_ref() } {
        Some(handle) => &handle.module,
        None => {
            set_last_error("Vertex shader module pointer is null");
            return std::ptr::null_mut();
        }
    };
    let vertex_entry_point_storage = match entry_point_from_bytes(&desc.vertex.entry_point) {
        Ok(value) => value,
        Err(_) => {
            set_last_error("Vertex entry point is not valid UTF-8");
            return std::ptr::null_mut();
        }
    };
    let vertex_entry_point = vertex_entry_point_storage.as_deref();

    let vertex_buffers_slice = if desc.vertex.buffer_count == 0 {
        &[][..]
    } else if desc.vertex.buffers.is_null() {
        set_last_error("Vertex buffer layouts pointer is null");
        return std::ptr::null_mut();
    } else {
        unsafe { slice::from_raw_parts(desc.vertex.buffers, desc.vertex.buffer_count) }
    };
    struct ConvertedVertexBufferLayout {
        array_stride: u64,
        step_mode: wgpu::VertexStepMode,
        attributes: Vec<wgpu::VertexAttribute>,
    }
    let mut converted_vertex_buffers = Vec::with_capacity(vertex_buffers_slice.len());
    for buffer in vertex_buffers_slice {
        let attributes_slice = if buffer.attribute_count == 0 {
            &[][..]
        } else if buffer.attributes.is_null() {
            set_last_error("Vertex attribute pointer is null");
            return std::ptr::null_mut();
        } else {
            unsafe { slice::from_raw_parts(buffer.attributes, buffer.attribute_count) }
        };
        let mut attributes = Vec::with_capacity(attributes_slice.len());
        for attribute in attributes_slice {
            let format = match vertex_format_from_u32(attribute.format) {
                Ok(value) => value,
                Err(_) => {
                    set_last_error("Invalid vertex attribute format");
                    return std::ptr::null_mut();
                }
            };
            attributes.push(wgpu::VertexAttribute {
                format,
                offset: attribute.offset,
                shader_location: attribute.shader_location,
            });
        }
        let step_mode = match vertex_step_mode_from_u32(buffer.step_mode) {
            Ok(value) => value,
            Err(_) => {
                set_last_error("Invalid vertex step mode");
                return std::ptr::null_mut();
            }
        };
        converted_vertex_buffers.push(ConvertedVertexBufferLayout {
            array_stride: buffer.array_stride,
            step_mode,
            attributes,
        });
    }
    let mut vertex_buffer_layouts = Vec::with_capacity(converted_vertex_buffers.len());
    for converted in &converted_vertex_buffers {
        vertex_buffer_layouts.push(wgpu::VertexBufferLayout {
            array_stride: converted.array_stride,
            step_mode: converted.step_mode,
            attributes: converted.attributes.as_slice(),
        });
    }

    let primitive_state = {
        let topology = match primitive_topology_from_u32(desc.primitive.topology) {
            Ok(value) => value,
            Err(_) => {
                set_last_error("Invalid primitive topology");
                return std::ptr::null_mut();
            }
        };
        let strip_index_format = match index_format_from_u32(desc.primitive.strip_index_format) {
            Ok(value) => value,
            Err(_) => {
                set_last_error("Invalid strip index format");
                return std::ptr::null_mut();
            }
        };
        let front_face = match front_face_from_u32(desc.primitive.front_face) {
            Ok(value) => value,
            Err(_) => {
                set_last_error("Invalid front face value");
                return std::ptr::null_mut();
            }
        };
        let cull_mode = match cull_mode_from_u32(desc.primitive.cull_mode) {
            Ok(value) => value,
            Err(_) => {
                set_last_error("Invalid cull mode value");
                return std::ptr::null_mut();
            }
        };
        let polygon_mode = match polygon_mode_from_u32(desc.primitive.polygon_mode) {
            Ok(value) => value,
            Err(_) => {
                set_last_error("Invalid polygon mode");
                return std::ptr::null_mut();
            }
        };
        wgpu::PrimitiveState {
            topology,
            strip_index_format,
            front_face,
            cull_mode,
            unclipped_depth: desc.primitive.unclipped_depth,
            polygon_mode,
            conservative: desc.primitive.conservative,
        }
    };

    let depth_stencil_state = if desc.depth_stencil.is_null() {
        None
    } else {
        let depth = match unsafe { desc.depth_stencil.as_ref() } {
            Some(value) => value,
            None => {
                set_last_error("Depth stencil pointer is null");
                return std::ptr::null_mut();
            }
        };
        let depth_compare = match compare_function_required(depth.depth_compare) {
            Ok(value) => value,
            Err(_) => {
                set_last_error("Invalid depth compare function");
                return std::ptr::null_mut();
            }
        };
        let stencil_front = match stencil_face_state_from_ffi(depth.stencil_front) {
            Ok(value) => value,
            Err(_) => {
                set_last_error("Invalid stencil front state");
                return std::ptr::null_mut();
            }
        };
        let stencil_back = match stencil_face_state_from_ffi(depth.stencil_back) {
            Ok(value) => value,
            Err(_) => {
                set_last_error("Invalid stencil back state");
                return std::ptr::null_mut();
            }
        };
        Some(wgpu::DepthStencilState {
            format: texture_format_from_ffi(depth.format),
            depth_write_enabled: depth.depth_write_enabled,
            depth_compare,
            stencil: wgpu::StencilState {
                front: stencil_front,
                back: stencil_back,
                read_mask: depth.stencil_read_mask,
                write_mask: depth.stencil_write_mask,
            },
            bias: wgpu::DepthBiasState {
                constant: depth.bias_constant,
                slope_scale: depth.bias_slope_scale,
                clamp: depth.bias_clamp,
            },
        })
    };

    let multisample_state = wgpu::MultisampleState {
        count: desc.multisample.count,
        mask: desc.multisample.mask as u64,
        alpha_to_coverage_enabled: desc.multisample.alpha_to_coverage_enabled,
    };

    let mut fragment_storage: Option<(Option<String>, Vec<Option<wgpu::ColorTargetState>>)> = None;
    let fragment_state = if desc.fragment.is_null() {
        None
    } else {
        let fragment = match unsafe { desc.fragment.as_ref() } {
            Some(value) => value,
            None => {
                set_last_error("Fragment descriptor pointer is null");
                return std::ptr::null_mut();
            }
        };
        let fragment_module = match unsafe { fragment.module.as_ref() } {
            Some(handle) => &handle.module,
            None => {
                set_last_error("Fragment shader module pointer is null");
                return std::ptr::null_mut();
            }
        };
        let entry_point_storage = match entry_point_from_bytes(&fragment.entry_point) {
            Ok(value) => value,
            Err(_) => {
                set_last_error("Fragment entry point is not valid UTF-8");
                return std::ptr::null_mut();
            }
        };
        let targets_slice = if fragment.target_count == 0 {
            &[][..]
        } else if fragment.targets.is_null() {
            set_last_error("Fragment target pointer is null");
            return std::ptr::null_mut();
        } else {
            unsafe { slice::from_raw_parts(fragment.targets, fragment.target_count) }
        };
        let mut color_targets = Vec::with_capacity(targets_slice.len());
        for target in targets_slice {
            let blend_state = if target.blend.is_null() {
                None
            } else {
                let blend = match unsafe { target.blend.as_ref() } {
                    Some(value) => value,
                    None => {
                        set_last_error("Fragment blend pointer is null");
                        return std::ptr::null_mut();
                    }
                };
                let color = match blend_component_from_ffi(&blend.color) {
                    Ok(value) => value,
                    Err(_) => {
                        set_last_error("Invalid color blend component");
                        return std::ptr::null_mut();
                    }
                };
                let alpha = match blend_component_from_ffi(&blend.alpha) {
                    Ok(value) => value,
                    Err(_) => {
                        set_last_error("Invalid alpha blend component");
                        return std::ptr::null_mut();
                    }
                };
                Some(wgpu::BlendState { color, alpha })
            };
            let write_mask = match color_write_mask_from_u32(target.write_mask) {
                Ok(value) => value,
                Err(_) => {
                    set_last_error("Invalid color write mask");
                    return std::ptr::null_mut();
                }
            };
            let state = wgpu::ColorTargetState {
                format: texture_format_from_ffi(target.format),
                blend: blend_state,
                write_mask,
            };
            color_targets.push(Some(state));
        }
        fragment_storage = Some((entry_point_storage, color_targets));
        let storage_ref = fragment_storage.as_ref().unwrap();
        Some(wgpu::FragmentState {
            module: fragment_module,
            entry_point: storage_ref.0.as_deref(),
            compilation_options: wgpu::PipelineCompilationOptions::default(),
            targets: storage_ref.1.as_slice(),
        })
    };
    let _ = &fragment_storage;

    let vertex_state = wgpu::VertexState {
        module: vertex_module,
        entry_point: vertex_entry_point,
        compilation_options: wgpu::PipelineCompilationOptions::default(),
        buffers: vertex_buffer_layouts.as_slice(),
    };

    let pipeline_descriptor = wgpu::RenderPipelineDescriptor {
        label: label_storage.as_deref(),
        layout: layout_ref,
        vertex: vertex_state,
        primitive: primitive_state,
        depth_stencil: depth_stencil_state,
        multisample: multisample_state,
        fragment: fragment_state,
        multiview: None,
        cache: None,
    };

    let pipeline = device.device.create_render_pipeline(&pipeline_descriptor);
    Box::into_raw(Box::new(VelloWgpuRenderPipelineHandle { pipeline }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_render_pipeline_destroy(
    pipeline: *mut VelloWgpuRenderPipelineHandle,
) {
    if !pipeline.is_null() {
        unsafe { drop(Box::from_raw(pipeline)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_device_create_command_encoder(
    device: *mut VelloWgpuDeviceHandle,
    descriptor: *const VelloWgpuCommandEncoderDescriptor,
) -> *mut VelloWgpuCommandEncoderHandle {
    clear_last_error();
    let Some(device) = (unsafe { device.as_ref() }) else {
        set_last_error("Device pointer is null");
        return std::ptr::null_mut();
    };

    let label_storage = if let Some(desc) = unsafe { descriptor.as_ref() } {
        match label_from_ptr(desc.label) {
            Ok(label) => label,
            Err(_) => {
                set_last_error("Command encoder label is not valid UTF-8");
                return std::ptr::null_mut();
            }
        }
    } else {
        None
    };

    let encoder_desc = wgpu::CommandEncoderDescriptor {
        label: label_storage.as_deref(),
    };

    let encoder = device.device.create_command_encoder(&encoder_desc);
    Box::into_raw(Box::new(VelloWgpuCommandEncoderHandle {
        encoder: Some(encoder),
        pass_active: false,
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_command_encoder_destroy(
    encoder: *mut VelloWgpuCommandEncoderHandle,
) {
    if !encoder.is_null() {
        let mut handle = unsafe { Box::from_raw(encoder) };
        handle.encoder.take();
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_command_encoder_begin_render_pass(
    encoder: *mut VelloWgpuCommandEncoderHandle,
    descriptor: *const VelloWgpuRenderPassDescriptor,
) -> *mut VelloWgpuRenderPassHandle {
    clear_last_error();
    let Some(encoder_handle) = (unsafe { encoder.as_mut() }) else {
        set_last_error("Command encoder pointer is null");
        return std::ptr::null_mut();
    };

    if encoder_handle.pass_active {
        set_last_error("A render pass is already active for this command encoder");
        return std::ptr::null_mut();
    }

    let Some(encoder_ref) = encoder_handle.encoder.as_mut() else {
        set_last_error("Command encoder has already been finished");
        return std::ptr::null_mut();
    };

    let Some(desc) = (unsafe { descriptor.as_ref() }) else {
        set_last_error("Render pass descriptor is null");
        return std::ptr::null_mut();
    };

    let label_storage = match label_from_ptr(desc.label) {
        Ok(label) => label,
        Err(_) => {
            set_last_error("Render pass label is not valid UTF-8");
            return std::ptr::null_mut();
        }
    };

    let color_slice = if desc.color_attachment_count == 0 {
        &[][..]
    } else if desc.color_attachments.is_null() {
        set_last_error("Render pass color attachments pointer is null");
        return std::ptr::null_mut();
    } else {
        unsafe { slice::from_raw_parts(desc.color_attachments, desc.color_attachment_count) }
    };

    if color_slice.is_empty() {
        set_last_error("Render pass requires at least one color attachment");
        return std::ptr::null_mut();
    }

    let mut color_views = Vec::with_capacity(color_slice.len());
    let mut resolve_views = Vec::with_capacity(color_slice.len());
    let mut color_attachments: Vec<Option<wgpu::RenderPassColorAttachment<'static>>> =
        Vec::with_capacity(color_slice.len());

    for attachment in color_slice {
        let Some(view_handle) = (unsafe { attachment.view.as_ref() }) else {
            set_last_error("Render pass color attachment view is null");
            return std::ptr::null_mut();
        };
        let view_ref: &'static wgpu::TextureView = unsafe {
            std::mem::transmute::<&wgpu::TextureView, &'static wgpu::TextureView>(&view_handle.view)
        };

        let resolve_ref = if attachment.resolve_target.is_null() {
            None
        } else {
            let Some(handle) = (unsafe { attachment.resolve_target.as_ref() }) else {
                set_last_error("Render pass resolve target view is null");
                return std::ptr::null_mut();
            };
            Some(unsafe {
                std::mem::transmute::<&wgpu::TextureView, &'static wgpu::TextureView>(&handle.view)
            })
        };

        let load = match color_load_op_from_ffi(attachment.load, attachment.clear_color) {
            Ok(value) => value,
            Err(_) => {
                set_last_error("Invalid color load operation");
                return std::ptr::null_mut();
            }
        };

        let store = store_op_from_ffi(attachment.store);

        color_views.push(view_ref);
        resolve_views.push(resolve_ref);
        color_attachments.push(Some(wgpu::RenderPassColorAttachment {
            view: view_ref,
            depth_slice: None,
            resolve_target: resolve_ref,
            ops: wgpu::Operations { load, store },
        }));
    }

    let (depth_attachment, depth_view) = if desc.depth_stencil.is_null() {
        (None, None)
    } else {
        let Some(depth) = (unsafe { desc.depth_stencil.as_ref() }) else {
            set_last_error("Depth stencil descriptor is null");
            return std::ptr::null_mut();
        };

        let Some(view_handle) = (unsafe { depth.view.as_ref() }) else {
            set_last_error("Depth stencil view is null");
            return std::ptr::null_mut();
        };
        let depth_view_ref: &'static wgpu::TextureView = unsafe {
            std::mem::transmute::<&wgpu::TextureView, &'static wgpu::TextureView>(&view_handle.view)
        };

        let depth_ops = if depth.depth_read_only {
            None
        } else {
            let load = match float_load_op_from_ffi(depth.depth_load, depth.depth_clear) {
                Ok(value) => value,
                Err(_) => {
                    set_last_error("Invalid depth load operation");
                    return std::ptr::null_mut();
                }
            };
            Some(wgpu::Operations {
                load,
                store: store_op_from_ffi(depth.depth_store),
            })
        };

        let stencil_ops = if depth.stencil_read_only {
            None
        } else {
            let load = match uint_load_op_from_ffi(depth.stencil_load, depth.stencil_clear) {
                Ok(value) => value,
                Err(_) => {
                    set_last_error("Invalid stencil load operation");
                    return std::ptr::null_mut();
                }
            };
            Some(wgpu::Operations {
                load,
                store: store_op_from_ffi(depth.stencil_store),
            })
        };

        (
            Some(wgpu::RenderPassDepthStencilAttachment {
                view: depth_view_ref,
                depth_ops,
                stencil_ops,
            }),
            Some(depth_view_ref),
        )
    };

    let render_pass_desc = wgpu::RenderPassDescriptor {
        label: label_storage.as_deref(),
        color_attachments: color_attachments.as_slice(),
        depth_stencil_attachment: depth_attachment.clone(),
        timestamp_writes: None,
        occlusion_query_set: None,
    };

    let pass = encoder_ref.begin_render_pass(&render_pass_desc);
    let pass_box = Box::new(ManuallyDrop::new(pass));
    let pass_ptr = Box::into_raw(pass_box);

    encoder_handle.pass_active = true;

    let inner = Box::new(VelloWgpuRenderPassInner {
        pass: pass_ptr,
        encoder,
        color_views,
        resolve_views,
        depth_stencil_view: depth_view,
    });

    Box::into_raw(Box::new(VelloWgpuRenderPassHandle {
        inner: Box::into_raw(inner),
    }))
}

fn render_pass_inner_mut<'a>(
    pass: *mut VelloWgpuRenderPassHandle,
) -> Result<&'a mut VelloWgpuRenderPassInner, ()> {
    unsafe {
        let Some(handle) = pass.as_mut() else {
            set_last_error("Render pass pointer is null");
            return Err(());
        };

        if handle.inner.is_null() {
            set_last_error("Render pass has already ended");
            return Err(());
        }

        Ok(&mut *handle.inner)
    }
}

fn render_pass_mut<'a>(
    pass: *mut VelloWgpuRenderPassHandle,
) -> Result<&'a mut wgpu::RenderPass<'static>, ()> {
    let inner = render_pass_inner_mut(pass)?;
    if inner.pass.is_null() {
        set_last_error("Render pass has already ended");
        return Err(());
    }
    unsafe { Ok(&mut **inner.pass) }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_render_pass_set_viewport(
    pass: *mut VelloWgpuRenderPassHandle,
    x: f32,
    y: f32,
    width: f32,
    height: f32,
    min_depth: f32,
    max_depth: f32,
) {
    clear_last_error();
    if width <= 0.0 {
        set_last_error("Viewport width must be positive");
        return;
    }
    if height <= 0.0 {
        set_last_error("Viewport height must be positive");
        return;
    }
    if !(0.0..=1.0).contains(&min_depth) {
        set_last_error("Viewport minimum depth must be between 0 and 1");
        return;
    }
    if !(0.0..=1.0).contains(&max_depth) {
        set_last_error("Viewport maximum depth must be between 0 and 1");
        return;
    }
    if min_depth > max_depth {
        set_last_error("Viewport minimum depth must not exceed maximum depth");
        return;
    }
    let Ok(pass_ref) = render_pass_mut(pass) else {
        return;
    };
    pass_ref.set_viewport(x, y, width, height, min_depth, max_depth);
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_render_pass_set_pipeline(
    pass: *mut VelloWgpuRenderPassHandle,
    pipeline: *mut VelloWgpuRenderPipelineHandle,
) {
    clear_last_error();
    let Ok(pass_ref) = render_pass_mut(pass) else {
        return;
    };
    let Some(pipeline_handle) = (unsafe { pipeline.as_ref() }) else {
        set_last_error("Pipeline pointer is null");
        return;
    };
    pass_ref.set_pipeline(&pipeline_handle.pipeline);
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_render_pass_set_bind_group(
    pass: *mut VelloWgpuRenderPassHandle,
    index: u32,
    bind_group: *mut VelloWgpuBindGroupHandle,
    dynamic_offsets: *const u32,
    dynamic_offset_count: usize,
) {
    clear_last_error();
    let Ok(pass_ref) = render_pass_mut(pass) else {
        return;
    };
    let Some(bind_group_handle) = (unsafe { bind_group.as_ref() }) else {
        set_last_error("Bind group pointer is null");
        return;
    };
    let offsets = if dynamic_offset_count == 0 {
        &[][..]
    } else if dynamic_offsets.is_null() {
        set_last_error("Dynamic offsets pointer is null");
        return;
    } else {
        unsafe { slice::from_raw_parts(dynamic_offsets, dynamic_offset_count) }
    };
    pass_ref.set_bind_group(index, &bind_group_handle.bind_group, offsets);
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_render_pass_set_scissor_rect(
    pass: *mut VelloWgpuRenderPassHandle,
    x: u32,
    y: u32,
    width: u32,
    height: u32,
) {
    clear_last_error();
    if width == 0 {
        set_last_error("Scissor width must be greater than zero");
        return;
    }
    if height == 0 {
        set_last_error("Scissor height must be greater than zero");
        return;
    }
    let Ok(pass_ref) = render_pass_mut(pass) else {
        return;
    };
    pass_ref.set_scissor_rect(x, y, width, height);
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_render_pass_set_vertex_buffer(
    pass: *mut VelloWgpuRenderPassHandle,
    slot: u32,
    buffer: *mut VelloWgpuBufferHandle,
    offset: u64,
    size: u64,
) {
    clear_last_error();
    let Ok(pass_ref) = render_pass_mut(pass) else {
        return;
    };
    let Some(buffer_handle) = (unsafe { buffer.as_ref() }) else {
        set_last_error("Vertex buffer pointer is null");
        return;
    };
    let slice = if size == 0 {
        buffer_handle.buffer.slice(offset..)
    } else if let Some(end) = offset.checked_add(size) {
        buffer_handle.buffer.slice(offset..end)
    } else {
        set_last_error("Vertex buffer slice range is invalid");
        return;
    };
    pass_ref.set_vertex_buffer(slot, slice);
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_render_pass_set_index_buffer(
    pass: *mut VelloWgpuRenderPassHandle,
    buffer: *mut VelloWgpuBufferHandle,
    format: u32,
    offset: u64,
    size: u64,
) {
    clear_last_error();
    let Ok(pass_ref) = render_pass_mut(pass) else {
        return;
    };
    let Some(buffer_handle) = (unsafe { buffer.as_ref() }) else {
        set_last_error("Index buffer pointer is null");
        return;
    };
    let Some(index_format) = (match index_format_from_u32(format) {
        Ok(value) => value,
        Err(_) => {
            set_last_error("Invalid index buffer format");
            return;
        }
    }) else {
        set_last_error("Index buffer format must not be undefined");
        return;
    };
    let slice = if size == 0 {
        buffer_handle.buffer.slice(offset..)
    } else if let Some(end) = offset.checked_add(size) {
        buffer_handle.buffer.slice(offset..end)
    } else {
        set_last_error("Index buffer slice range is invalid");
        return;
    };
    pass_ref.set_index_buffer(slice, index_format);
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_render_pass_draw(
    pass: *mut VelloWgpuRenderPassHandle,
    vertex_count: u32,
    instance_count: u32,
    first_vertex: u32,
    first_instance: u32,
) {
    clear_last_error();
    let Ok(pass_ref) = render_pass_mut(pass) else {
        return;
    };
    let Some(vertex_end) = first_vertex.checked_add(vertex_count) else {
        set_last_error("Vertex draw range overflow");
        return;
    };
    let Some(instance_end) = first_instance.checked_add(instance_count) else {
        set_last_error("Instance draw range overflow");
        return;
    };
    pass_ref.draw(first_vertex..vertex_end, first_instance..instance_end);
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_render_pass_draw_indexed(
    pass: *mut VelloWgpuRenderPassHandle,
    index_count: u32,
    instance_count: u32,
    first_index: u32,
    base_vertex: i32,
    first_instance: u32,
) {
    clear_last_error();
    let Ok(pass_ref) = render_pass_mut(pass) else {
        return;
    };
    let Some(index_end) = first_index.checked_add(index_count) else {
        set_last_error("Index draw range overflow");
        return;
    };
    let Some(instance_end) = first_instance.checked_add(instance_count) else {
        set_last_error("Instance draw range overflow");
        return;
    };
    pass_ref.draw_indexed(
        first_index..index_end,
        base_vertex,
        first_instance..instance_end,
    );
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_render_pass_end(pass: *mut VelloWgpuRenderPassHandle) {
    clear_last_error();
    let Some(handle) = (unsafe { pass.as_mut() }) else {
        set_last_error("Render pass pointer is null");
        return;
    };
    if handle.inner.is_null() {
        return;
    }

    let inner = unsafe { Box::from_raw(handle.inner) };
    if !inner.pass.is_null() {
        let mut pass_box = unsafe { Box::from_raw(inner.pass) };
        unsafe { ManuallyDrop::drop(&mut *pass_box) };
    }

    if let Some(encoder_handle) = unsafe { inner.encoder.as_mut() } {
        encoder_handle.pass_active = false;
    }

    handle.inner = std::ptr::null_mut();
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_command_encoder_finish(
    encoder: *mut VelloWgpuCommandEncoderHandle,
    descriptor: *const VelloWgpuCommandBufferDescriptor,
) -> *mut VelloWgpuCommandBufferHandle {
    clear_last_error();
    let Some(encoder_handle) = (unsafe { encoder.as_mut() }) else {
        set_last_error("Command encoder pointer is null");
        return std::ptr::null_mut();
    };

    if encoder_handle.pass_active {
        set_last_error("Cannot finish command encoder while a render pass is active");
        return std::ptr::null_mut();
    }

    let Some(encoder_value) = encoder_handle.encoder.take() else {
        set_last_error("Command encoder has already been finished");
        return std::ptr::null_mut();
    };

    if let Some(desc) = unsafe { descriptor.as_ref() } {
        if label_from_ptr(desc.label).is_err() {
            set_last_error("Command buffer label is not valid UTF-8");
            return std::ptr::null_mut();
        }
    }

    let buffer = encoder_value.finish();
    Box::into_raw(Box::new(VelloWgpuCommandBufferHandle {
        buffer: Some(buffer),
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_command_buffer_destroy(
    buffer: *mut VelloWgpuCommandBufferHandle,
) {
    if !buffer.is_null() {
        let mut handle = unsafe { Box::from_raw(buffer) };
        handle.buffer.take();
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_queue_submit(
    queue: *mut VelloWgpuQueueHandle,
    buffers: *const *mut VelloWgpuCommandBufferHandle,
    buffer_count: usize,
) -> u64 {
    clear_last_error();
    let Some(queue_handle) = (unsafe { queue.as_ref() }) else {
        set_last_error("Queue pointer is null");
        return 0;
    };

    let buffer_slice = if buffer_count == 0 {
        &[][..]
    } else if buffers.is_null() {
        set_last_error("Command buffer pointer array is null");
        return 0;
    } else {
        unsafe { slice::from_raw_parts(buffers, buffer_count) }
    };

    let mut owned_buffers = Vec::with_capacity(buffer_slice.len());
    for &buffer_ptr in buffer_slice {
        let Some(handle) = (unsafe { buffer_ptr.as_mut() }) else {
            set_last_error("Command buffer pointer is null");
            return 0;
        };
        let Some(buffer) = handle.buffer.take() else {
            set_last_error("Command buffer has already been consumed");
            return 0;
        };
        owned_buffers.push(buffer);
        unsafe { drop(Box::from_raw(buffer_ptr)) };
    }

    let submission = queue_handle.queue.submit(owned_buffers);
    // SAFETY: `wgpu::SubmissionIndex` currently contains a single `u64` field and has no
    // destructor. Using `transmute` lets us preserve the numeric submission identifier for
    // consumers that rely on it (for example, waiting on a submission). This mirrors the
    // previous tuple-struct layout used by wgpu prior to 0.19.
    unsafe { std::mem::transmute::<wgpu::SubmissionIndex, u64>(submission) }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_queue_write_buffer(
    queue: *mut VelloWgpuQueueHandle,
    buffer: *mut VelloWgpuBufferHandle,
    offset: u64,
    data: VelloBytes,
) -> VelloStatus {
    clear_last_error();
    let Some(queue_handle) = (unsafe { queue.as_ref() }) else {
        set_last_error("Queue pointer is null");
        return VelloStatus::InvalidArgument;
    };
    let Some(buffer_handle) = (unsafe { buffer.as_ref() }) else {
        set_last_error("Buffer pointer is null");
        return VelloStatus::InvalidArgument;
    };
    if data.length == 0 || data.data.is_null() {
        return VelloStatus::Success;
    }
    let bytes = unsafe { slice::from_raw_parts(data.data, data.length) };
    queue_handle
        .queue
        .write_buffer(&buffer_handle.buffer, offset, bytes);
    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_queue_write_texture(
    queue: *mut VelloWgpuQueueHandle,
    destination: *const VelloWgpuImageCopyTexture,
    data: VelloBytes,
    data_layout: VelloWgpuTextureDataLayout,
    size: VelloWgpuExtent3d,
) -> VelloStatus {
    clear_last_error();
    let Some(queue_handle) = (unsafe { queue.as_ref() }) else {
        set_last_error("Queue pointer is null");
        return VelloStatus::InvalidArgument;
    };
    let Some(dest) = (unsafe { destination.as_ref() }) else {
        set_last_error("Destination descriptor is null");
        return VelloStatus::InvalidArgument;
    };
    let Some(texture_handle) = (unsafe { dest.texture.as_ref() }) else {
        set_last_error("Destination texture pointer is null");
        return VelloStatus::InvalidArgument;
    };
    if data.length == 0 || data.data.is_null() {
        return VelloStatus::Success;
    }
    let aspect = match texture_aspect_from_u32(dest.aspect) {
        Ok(value) => value,
        Err(_) => {
            set_last_error("Invalid texture aspect");
            return VelloStatus::InvalidArgument;
        }
    };
    let image_copy = wgpu::TexelCopyTextureInfo {
        texture: &texture_handle.texture,
        mip_level: dest.mip_level,
        origin: wgpu::Origin3d {
            x: dest.origin.x,
            y: dest.origin.y,
            z: dest.origin.z,
        },
        aspect,
    };
    let layout = wgpu::TexelCopyBufferLayout {
        offset: data_layout.offset,
        bytes_per_row: (data_layout.bytes_per_row != 0).then_some(data_layout.bytes_per_row),
        rows_per_image: (data_layout.rows_per_image != 0).then_some(data_layout.rows_per_image),
    };
    let extent = extent3d_from_ffi(size);
    let bytes = unsafe { slice::from_raw_parts(data.data, data.length) };
    queue_handle
        .queue
        .write_texture(image_copy, bytes, layout, extent);
    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_surface_create(
    instance: *mut VelloWgpuInstanceHandle,
    descriptor: VelloSurfaceDescriptor,
) -> *mut VelloWgpuSurfaceHandle {
    clear_last_error();
    let Some(instance) = (unsafe { instance.as_ref() }) else {
        set_last_error("Instance pointer is null");
        return std::ptr::null_mut();
    };
    if descriptor.handle.kind == VelloWindowHandleKind::Headless
        || descriptor.handle.kind == VelloWindowHandleKind::None
    {
        set_last_error("Window handle is required to create a surface");
        return std::ptr::null_mut();
    }
    let handles = match SurfaceTargetHandles::try_from(&descriptor.handle) {
        Ok(handles) => handles,
        Err(status) => {
            match status {
                VelloStatus::Unsupported => set_last_error("Window handle type not supported"),
                VelloStatus::InvalidArgument => set_last_error("Invalid window handle"),
                _ => set_last_error("Failed to parse window handle"),
            }
            return std::ptr::null_mut();
        }
    };
    let target = match handles {
        SurfaceTargetHandles::Raw { window, display } => SurfaceTargetUnsafe::RawHandle {
            raw_display_handle: display,
            raw_window_handle: window,
        },
        #[cfg(any(target_os = "macos", target_os = "ios"))]
        SurfaceTargetHandles::CoreAnimationLayer(layer) => {
            SurfaceTargetUnsafe::CoreAnimationLayer(layer)
        }
        #[cfg(target_os = "windows")]
        SurfaceTargetHandles::SwapChainPanel(panel) => SurfaceTargetUnsafe::SwapChainPanel(panel),
        #[cfg(not(target_os = "windows"))]
        SurfaceTargetHandles::SwapChainPanel(_) => {
            unreachable!("SwapChainPanel handles are not available on this platform")
        }
    };

    match unsafe { instance.instance.create_surface_unsafe(target) } {
        Ok(surface) => Box::into_raw(Box::new(VelloWgpuSurfaceHandle { surface })),
        Err(err) => {
            set_last_error(format!("Failed to create surface: {err}"));
            std::ptr::null_mut()
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_surface_destroy(surface: *mut VelloWgpuSurfaceHandle) {
    if !surface.is_null() {
        unsafe { drop(Box::from_raw(surface)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_surface_get_preferred_format(
    surface: *mut VelloWgpuSurfaceHandle,
    adapter: *mut VelloWgpuAdapterHandle,
    out_format: *mut VelloWgpuTextureFormat,
) -> VelloStatus {
    clear_last_error();
    let Some(surface) = (unsafe { surface.as_ref() }) else {
        return VelloStatus::NullPointer;
    };
    let Some(adapter) = (unsafe { adapter.as_ref() }) else {
        return VelloStatus::NullPointer;
    };
    if out_format.is_null() {
        return VelloStatus::NullPointer;
    }
    let capabilities = surface.surface.get_capabilities(&adapter.adapter);
    let Some(format) = capabilities
        .formats
        .into_iter()
        .find_map(texture_format_to_ffi)
    else {
        set_last_error("No supported texture format found");
        return VelloStatus::Unsupported;
    };
    unsafe {
        *out_format = format;
    }
    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_surface_configure(
    surface: *mut VelloWgpuSurfaceHandle,
    device: *mut VelloWgpuDeviceHandle,
    config: *const VelloWgpuSurfaceConfiguration,
) -> VelloStatus {
    clear_last_error();
    let Some(surface) = (unsafe { surface.as_ref() }) else {
        return VelloStatus::NullPointer;
    };
    let Some(device) = (unsafe { device.as_ref() }) else {
        return VelloStatus::NullPointer;
    };
    let Some(config) = (unsafe { config.as_ref() }) else {
        return VelloStatus::NullPointer;
    };
    if config.width == 0 || config.height == 0 {
        return VelloStatus::InvalidArgument;
    }
    let usage = match texture_usage_from_bits(config.usage) {
        Ok(usage) => usage,
        Err(_) => {
            set_last_error("Invalid texture usage flags");
            return VelloStatus::InvalidArgument;
        }
    };
    let format = texture_format_from_ffi(config.format);
    let alpha_mode = alpha_mode_from_ffi(config.alpha_mode);

    let view_formats = if config.view_format_count == 0 {
        vec![]
    } else {
        if config.view_formats.is_null() {
            set_last_error("View formats pointer is null");
            return VelloStatus::NullPointer;
        }
        let slice = unsafe { slice::from_raw_parts(config.view_formats, config.view_format_count) };
        let mut converted = Vec::with_capacity(slice.len());
        for fmt in slice {
            converted.push(texture_format_from_ffi(*fmt));
        }
        converted
    };

    let configuration = SurfaceConfiguration {
        usage,
        format,
        width: config.width,
        height: config.height,
        present_mode: config.present_mode.into(),
        desired_maximum_frame_latency: 2,
        alpha_mode,
        view_formats,
    };

    surface.surface.configure(&device.device, &configuration);
    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_surface_acquire_next_texture(
    surface: *mut VelloWgpuSurfaceHandle,
) -> *mut VelloWgpuSurfaceTextureHandle {
    clear_last_error();
    let Some(surface) = (unsafe { surface.as_ref() }) else {
        set_last_error("Surface pointer is null");
        return std::ptr::null_mut();
    };
    match surface.surface.get_current_texture() {
        Ok(texture) => Box::into_raw(Box::new(VelloWgpuSurfaceTextureHandle { texture })),
        Err(err) => {
            set_last_error(format!("Failed to acquire surface texture: {err}"));
            std::ptr::null_mut()
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_surface_texture_create_view(
    texture: *mut VelloWgpuSurfaceTextureHandle,
    descriptor: *const VelloWgpuTextureViewDescriptor,
) -> *mut VelloWgpuTextureViewHandle {
    clear_last_error();
    let Some(texture) = (unsafe { texture.as_ref() }) else {
        set_last_error("Surface texture pointer is null");
        return std::ptr::null_mut();
    };
    let descriptor = unsafe { descriptor.as_ref() };
    let view_desc = match texture_view_descriptor_from_ffi(descriptor) {
        Ok(value) => value,
        Err(_) => {
            set_last_error("Invalid texture view descriptor");
            return std::ptr::null_mut();
        }
    };
    let view = texture.texture.texture.create_view(&view_desc);
    Box::into_raw(Box::new(VelloWgpuTextureViewHandle { view }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_texture_view_destroy(view: *mut VelloWgpuTextureViewHandle) {
    if !view.is_null() {
        unsafe { drop(Box::from_raw(view)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_surface_texture_present(
    texture: *mut VelloWgpuSurfaceTextureHandle,
) {
    if !texture.is_null() {
        let texture = unsafe { Box::from_raw(texture) };
        texture.texture.present();
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_surface_texture_destroy(
    texture: *mut VelloWgpuSurfaceTextureHandle,
) {
    if !texture.is_null() {
        unsafe { drop(Box::from_raw(texture)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_renderer_create(
    device: *mut VelloWgpuDeviceHandle,
    options: VelloRendererOptions,
) -> *mut VelloWgpuRendererHandle {
    clear_last_error();
    let Some(device_handle) = (unsafe { device.as_ref() }) else {
        set_last_error("Device pointer is null");
        return std::ptr::null_mut();
    };

    let renderer_options = renderer_options_from_ffi(&options);
    let renderer = match Renderer::new(&device_handle.device, renderer_options) {
        Ok(renderer) => renderer,
        Err(err) => {
            set_last_error(format!("Failed to create renderer: {err}"));
            return std::ptr::null_mut();
        }
    };

    Box::into_raw(Box::new(VelloWgpuRendererHandle {
        device: device_handle.device.clone(),
        queue: device_handle.queue.clone(),
        renderer,
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_renderer_destroy(renderer: *mut VelloWgpuRendererHandle) {
    if !renderer.is_null() {
        unsafe { drop(Box::from_raw(renderer)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_renderer_render(
    renderer: *mut VelloWgpuRendererHandle,
    scene: *const VelloSceneHandle,
    texture_view: *const VelloWgpuTextureViewHandle,
    params: VelloRenderParams,
) -> VelloStatus {
    clear_last_error();
    let Some(renderer) = (unsafe { renderer.as_mut() }) else {
        return VelloStatus::NullPointer;
    };
    let Some(scene) = (unsafe { scene.as_ref() }) else {
        return VelloStatus::NullPointer;
    };
    let Some(texture_view) = (unsafe { texture_view.as_ref() }) else {
        return VelloStatus::NullPointer;
    };

    if params.width == 0 || params.height == 0 {
        return VelloStatus::InvalidArgument;
    }

    let render_params = RenderParams {
        base_color: params.base_color.into(),
        width: params.width,
        height: params.height,
        antialiasing_method: params.antialiasing.into(),
    };

    if let Err(err) = renderer.renderer.render_to_texture(
        &renderer.device,
        &renderer.queue,
        &scene.inner,
        &texture_view.view,
        &render_params,
    ) {
        set_last_error(format!("Render failed: {err}"));
        return VelloStatus::RenderError;
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_renderer_render_surface(
    renderer: *mut VelloWgpuRendererHandle,
    scene: *const VelloSceneHandle,
    surface_view: *const VelloWgpuTextureViewHandle,
    params: VelloRenderParams,
    surface_format: VelloWgpuTextureFormat,
) -> VelloStatus {
    clear_last_error();
    let Some(renderer) = (unsafe { renderer.as_mut() }) else {
        return VelloStatus::NullPointer;
    };
    let Some(scene) = (unsafe { scene.as_ref() }) else {
        return VelloStatus::NullPointer;
    };
    let Some(surface_view) = (unsafe { surface_view.as_ref() }) else {
        return VelloStatus::NullPointer;
    };

    if params.width == 0 || params.height == 0 {
        return VelloStatus::InvalidArgument;
    }

    let render_params = RenderParams {
        base_color: params.base_color.into(),
        width: params.width,
        height: params.height,
        antialiasing_method: params.antialiasing.into(),
    };

    let target = match create_render_target(&renderer.device, params.width, params.height) {
        Ok(target) => target,
        Err(status) => {
            set_last_error("Failed to create intermediate render target");
            return status;
        }
    };

    if let Err(err) = renderer.renderer.render_to_texture(
        &renderer.device,
        &renderer.queue,
        &scene.inner,
        &target.view,
        &render_params,
    ) {
        set_last_error(format!("Render failed: {err}"));
        return VelloStatus::RenderError;
    }

    let mut encoder = renderer
        .device
        .create_command_encoder(&wgpu::CommandEncoderDescriptor {
            label: Some("vello_ffi.surface_blit"),
        });

    let format = texture_format_from_ffi(surface_format);
    let blitter = TextureBlitter::new(&renderer.device, format);
    blitter.copy(
        &renderer.device,
        &mut encoder,
        &target.view,
        &surface_view.view,
    );

    renderer.queue.submit(std::iter::once(encoder.finish()));

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_renderer_profiler_set_enabled(
    renderer: *mut VelloWgpuRendererHandle,
    enabled: bool,
) -> VelloStatus {
    clear_last_error();
    let Some(renderer) = (unsafe { renderer.as_mut() }) else {
        return VelloStatus::NullPointer;
    };

    if let Err(err) =
        renderer
            .renderer
            .profiler
            .change_settings(wgpu_profiler::GpuProfilerSettings {
                enable_timer_queries: enabled,
                enable_debug_groups: enabled,
                ..Default::default()
            })
    {
        set_last_error(format!("Failed to configure GPU profiler: {err}"));
        return VelloStatus::RenderError;
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_renderer_profiler_get_results(
    renderer: *mut VelloWgpuRendererHandle,
    out_results: *mut VelloGpuProfilerResults,
) -> VelloStatus {
    clear_last_error();
    if out_results.is_null() {
        return VelloStatus::NullPointer;
    }

    let Some(renderer) = (unsafe { renderer.as_mut() }) else {
        return VelloStatus::NullPointer;
    };

    let results = match renderer.renderer.profile_result.take() {
        Some(results) => results,
        None => {
            unsafe {
                *out_results = VelloGpuProfilerResults {
                    handle: std::ptr::null_mut(),
                    slices: std::ptr::null(),
                    slice_count: 0,
                    labels: std::ptr::null(),
                    labels_len: 0,
                    total_gpu_time_ms: 0.0,
                };
            }
            return VelloStatus::Success;
        }
    };

    let mut slices = Vec::new();
    let mut labels = Vec::new();
    flatten_profiler_results(&results, 0, &mut slices, &mut labels);

    let total_gpu_time_ms = slices
        .first()
        .filter(|slice| slice.has_time != 0)
        .map(|slice| (slice.time_end_ms - slice.time_start_ms).max(0.0))
        .unwrap_or(0.0);

    let raw = Box::new(ProfilerResultsRaw {
        slices: slices.into_boxed_slice(),
        labels: labels.into_boxed_slice(),
    });
    let raw_ptr = Box::into_raw(raw);

    unsafe {
        let raw_ref: &ProfilerResultsRaw = &*raw_ptr;
        *out_results = VelloGpuProfilerResults {
            handle: raw_ptr.cast(),
            slices: raw_ref.slices.as_ptr(),
            slice_count: raw_ref.slices.len(),
            labels: raw_ref.labels.as_ptr(),
            labels_len: raw_ref.labels.len(),
            total_gpu_time_ms,
        };
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_renderer_profiler_results_free(handle: *mut c_void) {
    if handle.is_null() {
        return;
    }
    unsafe {
        drop(Box::from_raw(handle as *mut ProfilerResultsRaw));
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_svg_load_from_memory(
    data: *const u8,
    length: usize,
    scale: f32,
) -> *mut VelloSvgHandle {
    clear_last_error();
    if data.is_null() {
        set_last_error("SVG data pointer is null");
        return std::ptr::null_mut();
    }
    if length == 0 {
        set_last_error("SVG data length must be non-zero");
        return std::ptr::null_mut();
    }
    let bytes = unsafe { slice::from_raw_parts(data, length) };
    let contents = match std::str::from_utf8(bytes) {
        Ok(text) => text,
        Err(err) => {
            set_last_error(format!("SVG data is not valid UTF-8: {err}"));
            return std::ptr::null_mut();
        }
    };

    if scale.abs() > f32::EPSILON && (scale - 1.0).abs() > f32::EPSILON {
        set_last_error("Scaling SVG is not supported by the FFI bindings.");
        return std::ptr::null_mut();
    }

    match load_svg_from_str(contents) {
        Ok(svg) => Box::into_raw(Box::new(svg)),
        Err(err) => {
            set_last_error(err);
            std::ptr::null_mut()
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_svg_load_from_file(
    path: *const c_char,
    scale: f32,
) -> *mut VelloSvgHandle {
    clear_last_error();
    if path.is_null() {
        set_last_error("SVG path pointer is null");
        return std::ptr::null_mut();
    }
    let c_path = unsafe { CStr::from_ptr(path) };
    let path_str = match c_path.to_str() {
        Ok(value) => value,
        Err(err) => {
            set_last_error(format!("SVG path is not valid UTF-8: {err}"));
            return std::ptr::null_mut();
        }
    };
    let contents = match std::fs::read_to_string(path_str) {
        Ok(contents) => contents,
        Err(err) => {
            set_last_error(format!("Failed to read SVG file '{path_str}': {err}"));
            return std::ptr::null_mut();
        }
    };

    if scale.abs() > f32::EPSILON && (scale - 1.0).abs() > f32::EPSILON {
        set_last_error("Scaling SVG is not supported by the FFI bindings.");
        return std::ptr::null_mut();
    }

    match load_svg_from_str(&contents) {
        Ok(svg) => Box::into_raw(Box::new(svg)),
        Err(err) => {
            set_last_error(err);
            std::ptr::null_mut()
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_svg_destroy(svg: *mut VelloSvgHandle) {
    if !svg.is_null() {
        unsafe { drop(Box::from_raw(svg)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_svg_get_size(
    svg: *const VelloSvgHandle,
    out_size: *mut VelloPoint,
) -> VelloStatus {
    clear_last_error();
    let Some(svg) = (unsafe { svg.as_ref() }) else {
        return VelloStatus::NullPointer;
    };
    if out_size.is_null() {
        return VelloStatus::NullPointer;
    }

    unsafe {
        (*out_size).x = svg.resolution.x;
        (*out_size).y = svg.resolution.y;
    }
    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_svg_render(
    svg: *const VelloSvgHandle,
    scene: *mut VelloSceneHandle,
    transform: *const VelloAffine,
) -> VelloStatus {
    clear_last_error();
    let Some(svg) = (unsafe { svg.as_ref() }) else {
        return VelloStatus::NullPointer;
    };
    let Some(scene) = (unsafe { scene.as_mut() }) else {
        return VelloStatus::NullPointer;
    };

    let transform = match optional_affine(transform) {
        Ok(value) => value,
        Err(status) => return status,
    };

    scene.inner.append(&svg.scene, transform);
    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_velato_composition_load_from_memory(
    data: *const u8,
    length: usize,
) -> *mut VelloVelatoCompositionHandle {
    clear_last_error();
    if data.is_null() {
        set_last_error("Lottie data pointer is null");
        return std::ptr::null_mut();
    }
    if length == 0 {
        set_last_error("Lottie data length must be non-zero");
        return std::ptr::null_mut();
    }
    let bytes = unsafe { slice::from_raw_parts(data, length) };
    match load_velato_composition_from_slice(bytes) {
        Ok(handle) => Box::into_raw(Box::new(handle)),
        Err(err) => {
            set_last_error(err);
            std::ptr::null_mut()
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_velato_composition_load_from_file(
    path: *const c_char,
) -> *mut VelloVelatoCompositionHandle {
    clear_last_error();
    if path.is_null() {
        set_last_error("Lottie path pointer is null");
        return std::ptr::null_mut();
    }
    let c_path = unsafe { CStr::from_ptr(path) };
    let path_str = match c_path.to_str() {
        Ok(value) => value,
        Err(err) => {
            set_last_error(format!("Lottie path is not valid UTF-8: {err}"));
            return std::ptr::null_mut();
        }
    };
    let bytes = match std::fs::read(path_str) {
        Ok(bytes) => bytes,
        Err(err) => {
            set_last_error(format!("Failed to read Lottie file '{path_str}': {err}"));
            return std::ptr::null_mut();
        }
    };
    match load_velato_composition_from_slice(&bytes) {
        Ok(handle) => Box::into_raw(Box::new(handle)),
        Err(err) => {
            set_last_error(err);
            std::ptr::null_mut()
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_velato_composition_destroy(
    composition: *mut VelloVelatoCompositionHandle,
) {
    if !composition.is_null() {
        unsafe { drop(Box::from_raw(composition)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_velato_composition_get_info(
    composition: *const VelloVelatoCompositionHandle,
    out_info: *mut VelloVelatoCompositionInfo,
) -> VelloStatus {
    clear_last_error();
    let Some(composition) = (unsafe { composition.as_ref() }) else {
        return VelloStatus::NullPointer;
    };
    if out_info.is_null() {
        return VelloStatus::NullPointer;
    }

    let width = match u32::try_from(composition.composition.width) {
        Ok(value) => value,
        Err(_) => {
            set_last_error("Lottie composition width exceeds u32 range");
            return VelloStatus::InvalidArgument;
        }
    };
    let height = match u32::try_from(composition.composition.height) {
        Ok(value) => value,
        Err(_) => {
            set_last_error("Lottie composition height exceeds u32 range");
            return VelloStatus::InvalidArgument;
        }
    };

    unsafe {
        (*out_info).start_frame = composition.composition.frames.start;
        (*out_info).end_frame = composition.composition.frames.end;
        (*out_info).frame_rate = composition.composition.frame_rate;
        (*out_info).width = width;
        (*out_info).height = height;
    }

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_velato_renderer_create() -> *mut VelloVelatoRendererHandle {
    clear_last_error();
    let renderer = VelatoRenderer::new();
    Box::into_raw(Box::new(VelloVelatoRendererHandle {
        renderer: RefCell::new(renderer),
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_velato_renderer_destroy(renderer: *mut VelloVelatoRendererHandle) {
    if !renderer.is_null() {
        unsafe { drop(Box::from_raw(renderer)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_velato_renderer_render(
    renderer: *mut VelloVelatoRendererHandle,
    composition: *const VelloVelatoCompositionHandle,
    scene: *mut VelloSceneHandle,
    frame: f64,
    alpha: f64,
    transform: *const VelloAffine,
) -> VelloStatus {
    clear_last_error();
    let Some(renderer) = (unsafe { renderer.as_mut() }) else {
        return VelloStatus::NullPointer;
    };
    let Some(composition) = (unsafe { composition.as_ref() }) else {
        return VelloStatus::NullPointer;
    };
    let Some(scene) = (unsafe { scene.as_mut() }) else {
        return VelloStatus::NullPointer;
    };

    let transform = match optional_affine(transform) {
        Ok(value) => value.unwrap_or(Affine::IDENTITY),
        Err(status) => return status,
    };

    renderer.renderer.borrow_mut().append(
        &composition.composition,
        frame,
        transform,
        alpha,
        &mut scene.inner,
    );

    VelloStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_scene_draw_glyph_run(
    scene: *mut VelloSceneHandle,
    font: *const VelloFontHandle,
    glyphs: *const VelloGlyph,
    glyph_count: usize,
    options: VelloGlyphRunOptions,
) -> VelloStatus {
    clear_last_error();
    let Some(scene) = (unsafe { scene.as_mut() }) else {
        return VelloStatus::NullPointer;
    };
    let Some(font) = (unsafe { font.as_ref() }) else {
        return VelloStatus::NullPointer;
    };
    let glyphs = match unsafe { slice_from_raw(glyphs, glyph_count) } {
        Ok(slice) => slice,
        Err(status) => return status,
    };
    let brush = match brush_from_ffi(&options.brush) {
        Ok(brush) => brush,
        Err(status) => return status,
    };
    let brush_ref = BrushRef::from(&brush);
    let glyph_transform = match optional_affine(options.glyph_transform) {
        Ok(value) => value,
        Err(status) => return status,
    };
    let mut builder = scene
        .inner
        .draw_glyphs(&font.font)
        .transform(options.transform.into())
        .font_size(options.font_size)
        .hint(options.hint)
        .brush(brush_ref)
        .brush_alpha(options.brush_alpha);
    builder = builder.glyph_transform(glyph_transform);

    let glyph_iter = glyphs.iter().copied().map(|glyph| Glyph {
        id: glyph.id,
        x: glyph.x,
        y: glyph.y,
    });

    match options.style {
        VelloGlyphRunStyle::Fill => builder.draw(Fill::NonZero, glyph_iter),
        VelloGlyphRunStyle::Stroke => match options.stroke_style.to_stroke() {
            Ok(stroke) => builder.draw(&stroke, glyph_iter),
            Err(status) => return status,
        },
    }

    VelloStatus::Success
}

#[cfg(not(target_os = "windows"))]
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_device_create_shared_texture(
    _device: *mut VelloWgpuDeviceHandle,
    _desc: *const VelloSharedTextureDesc,
    out_handle: *mut *mut VelloSharedTextureHandle,
) -> VelloStatus {
    if out_handle.is_null() {
        set_last_error("Null out_handle passed to vello_wgpu_device_create_shared_texture");
        return VelloStatus::NullPointer;
    }

    unsafe {
        *out_handle = std::ptr::null_mut();
    }
    set_last_error("Shared texture interop is only supported on Windows builds");
    VelloStatus::Unsupported
}

#[cfg(not(target_os = "windows"))]
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_shared_texture_destroy(handle: *mut VelloSharedTextureHandle) {
    if !handle.is_null() {
        unsafe { drop(Box::from_raw(handle)) };
    }
}

#[cfg(target_os = "windows")]
pub use windows_shared_texture::{
    vello_shared_texture_acquire_mutex, vello_shared_texture_destroy, vello_shared_texture_flush,
    vello_shared_texture_release_mutex, vello_wgpu_device_create_shared_texture,
};
