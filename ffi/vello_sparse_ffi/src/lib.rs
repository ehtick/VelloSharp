#![cfg_attr(not(test), deny(clippy::all))]
#![allow(clippy::missing_safety_doc)]
#![allow(missing_docs)]
#![allow(missing_debug_implementations)]

use std::{
    cell::RefCell,
    ffi::{CString, c_char},
    ptr, slice,
};

use vello_common::{
    kurbo::{Affine, BezPath, Cap, Join, PathEl, Point, Rect, Stroke},
    peniko::{Color, Fill},
};
use vello_cpu::{RenderContext, RenderMode};

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

unsafe fn slice_from_raw<'a, T>(ptr: *const T, len: usize) -> Result<&'a [T], VelloSparseStatus> {
    if len == 0 {
        Ok(&[])
    } else if ptr.is_null() {
        Err(VelloSparseStatus::NullPointer)
    } else {
        Ok(unsafe { slice::from_raw_parts(ptr, len) })
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloSparseStatus {
    Success = 0,
    NullPointer = 1,
    InvalidArgument = 2,
    RenderError = 3,
}

#[unsafe(no_mangle)]
pub extern "C" fn vello_sparse_last_error_message() -> *const c_char {
    LAST_ERROR.with(|slot| match slot.borrow().as_ref() {
        Some(cstr) => cstr.as_ptr(),
        None => ptr::null(),
    })
}

#[repr(i32)]
#[derive(Debug, Copy, Clone)]
pub enum VelloSparsePathVerb {
    MoveTo = 0,
    LineTo = 1,
    QuadTo = 2,
    CubicTo = 3,
    Close = 4,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloSparsePathElement {
    pub verb: VelloSparsePathVerb,
    pub _padding: i32,
    pub x0: f64,
    pub y0: f64,
    pub x1: f64,
    pub y1: f64,
    pub x2: f64,
    pub y2: f64,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloSparseAffine {
    pub m11: f64,
    pub m12: f64,
    pub m21: f64,
    pub m22: f64,
    pub dx: f64,
    pub dy: f64,
}

impl From<VelloSparseAffine> for Affine {
    fn from(value: VelloSparseAffine) -> Self {
        Affine::new([
            value.m11, value.m12, value.m21, value.m22, value.dx, value.dy,
        ])
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloSparseColor {
    pub r: f32,
    pub g: f32,
    pub b: f32,
    pub a: f32,
}

impl From<VelloSparseColor> for Color {
    fn from(value: VelloSparseColor) -> Self {
        Color::new([value.r, value.g, value.b, value.a])
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloSparseRect {
    pub x: f64,
    pub y: f64,
    pub width: f64,
    pub height: f64,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone)]
pub enum VelloSparseFillRule {
    NonZero = 0,
    EvenOdd = 1,
}

impl From<VelloSparseFillRule> for Fill {
    fn from(value: VelloSparseFillRule) -> Self {
        match value {
            VelloSparseFillRule::NonZero => Fill::NonZero,
            VelloSparseFillRule::EvenOdd => Fill::EvenOdd,
        }
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone)]
pub enum VelloSparseRenderMode {
    OptimizeSpeed = 0,
    OptimizeQuality = 1,
}

impl From<VelloSparseRenderMode> for RenderMode {
    fn from(value: VelloSparseRenderMode) -> Self {
        match value {
            VelloSparseRenderMode::OptimizeSpeed => RenderMode::OptimizeSpeed,
            VelloSparseRenderMode::OptimizeQuality => RenderMode::OptimizeQuality,
        }
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone)]
pub enum VelloSparseLineCap {
    Butt = 0,
    Round = 1,
    Square = 2,
}

impl From<VelloSparseLineCap> for Cap {
    fn from(value: VelloSparseLineCap) -> Self {
        match value {
            VelloSparseLineCap::Butt => Cap::Butt,
            VelloSparseLineCap::Round => Cap::Round,
            VelloSparseLineCap::Square => Cap::Square,
        }
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone)]
pub enum VelloSparseLineJoin {
    Miter = 0,
    Round = 1,
    Bevel = 2,
}

impl From<VelloSparseLineJoin> for Join {
    fn from(value: VelloSparseLineJoin) -> Self {
        match value {
            VelloSparseLineJoin::Miter => Join::Miter,
            VelloSparseLineJoin::Round => Join::Round,
            VelloSparseLineJoin::Bevel => Join::Bevel,
        }
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloSparseStrokeStyle {
    pub width: f64,
    pub miter_limit: f64,
    pub start_cap: VelloSparseLineCap,
    pub end_cap: VelloSparseLineCap,
    pub line_join: VelloSparseLineJoin,
    pub dash_phase: f64,
    pub dash_pattern: *const f64,
    pub dash_length: usize,
}

impl VelloSparseStrokeStyle {
    fn to_stroke(&self) -> Result<Stroke, VelloSparseStatus> {
        if !(self.width.is_finite() && self.width >= 0.0) {
            return Err(VelloSparseStatus::InvalidArgument);
        }
        if !(self.miter_limit.is_finite() && self.miter_limit >= 0.0) {
            return Err(VelloSparseStatus::InvalidArgument);
        }
        let mut stroke = Stroke::new(self.width)
            .with_start_cap(self.start_cap.into())
            .with_end_cap(self.end_cap.into())
            .with_join(self.line_join.into())
            .with_miter_limit(self.miter_limit);
        if self.dash_length > 0 {
            if self.dash_pattern.is_null() {
                return Err(VelloSparseStatus::InvalidArgument);
            }
            let dashes = unsafe { slice::from_raw_parts(self.dash_pattern, self.dash_length) };
            stroke = stroke.with_dashes(self.dash_phase, dashes.to_vec());
        }
        Ok(stroke)
    }
}

fn build_bez_path(elements: &[VelloSparsePathElement]) -> Result<BezPath, &'static str> {
    let mut path = BezPath::new();
    for element in elements {
        match element.verb {
            VelloSparsePathVerb::MoveTo => {
                path.push(PathEl::MoveTo(Point::new(element.x0, element.y0)));
            }
            VelloSparsePathVerb::LineTo => {
                path.push(PathEl::LineTo(Point::new(element.x0, element.y0)));
            }
            VelloSparsePathVerb::QuadTo => {
                path.push(PathEl::QuadTo(
                    Point::new(element.x0, element.y0),
                    Point::new(element.x1, element.y1),
                ));
            }
            VelloSparsePathVerb::CubicTo => {
                path.push(PathEl::CurveTo(
                    Point::new(element.x0, element.y0),
                    Point::new(element.x1, element.y1),
                    Point::new(element.x2, element.y2),
                ));
            }
            VelloSparsePathVerb::Close => path.push(PathEl::ClosePath),
        }
    }
    Ok(path)
}

fn rect_from_ffi(rect: VelloSparseRect) -> Result<Rect, VelloSparseStatus> {
    if !rect.width.is_finite() || !rect.height.is_finite() {
        return Err(VelloSparseStatus::InvalidArgument);
    }
    if rect.width < 0.0 || rect.height < 0.0 {
        return Err(VelloSparseStatus::InvalidArgument);
    }
    Ok(Rect::new(
        rect.x,
        rect.y,
        rect.x + rect.width,
        rect.y + rect.height,
    ))
}

fn context_from_raw<'a>(
    ctx: *mut RenderContext,
) -> Result<&'a mut RenderContext, VelloSparseStatus> {
    let Some(ctx) = (unsafe { ctx.as_mut() }) else {
        return Err(VelloSparseStatus::NullPointer);
    };
    Ok(ctx)
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_create(
    width: u16,
    height: u16,
) -> *mut RenderContext {
    clear_last_error();
    if width == 0 || height == 0 {
        set_last_error("Render context dimensions must be non-zero");
        return ptr::null_mut();
    }
    let context = RenderContext::new(width, height);
    Box::into_raw(Box::new(context))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_destroy(ctx: *mut RenderContext) {
    if !ctx.is_null() {
        unsafe { drop(Box::from_raw(ctx)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_reset(
    ctx: *mut RenderContext,
) -> VelloSparseStatus {
    clear_last_error();
    let Ok(ctx) = context_from_raw(ctx) else {
        return VelloSparseStatus::NullPointer;
    };
    ctx.reset();
    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_flush(
    ctx: *mut RenderContext,
) -> VelloSparseStatus {
    clear_last_error();
    let Ok(ctx) = context_from_raw(ctx) else {
        return VelloSparseStatus::NullPointer;
    };
    ctx.flush();
    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_get_size(
    ctx: *const RenderContext,
    out_width: *mut u16,
    out_height: *mut u16,
) -> VelloSparseStatus {
    clear_last_error();
    if out_width.is_null() || out_height.is_null() {
        return VelloSparseStatus::NullPointer;
    }
    let Some(ctx) = (unsafe { ctx.as_ref() }) else {
        return VelloSparseStatus::NullPointer;
    };
    unsafe {
        *out_width = ctx.width();
        *out_height = ctx.height();
    }
    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_set_fill_rule(
    ctx: *mut RenderContext,
    fill_rule: VelloSparseFillRule,
) -> VelloSparseStatus {
    clear_last_error();
    let Ok(ctx) = context_from_raw(ctx) else {
        return VelloSparseStatus::NullPointer;
    };
    ctx.set_fill_rule(fill_rule.into());
    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_set_transform(
    ctx: *mut RenderContext,
    transform: VelloSparseAffine,
) -> VelloSparseStatus {
    clear_last_error();
    let Ok(ctx) = context_from_raw(ctx) else {
        return VelloSparseStatus::NullPointer;
    };
    ctx.set_transform(transform.into());
    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_reset_transform(
    ctx: *mut RenderContext,
) -> VelloSparseStatus {
    clear_last_error();
    let Ok(ctx) = context_from_raw(ctx) else {
        return VelloSparseStatus::NullPointer;
    };
    ctx.reset_transform();
    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_set_paint_transform(
    ctx: *mut RenderContext,
    transform: VelloSparseAffine,
) -> VelloSparseStatus {
    clear_last_error();
    let Ok(ctx) = context_from_raw(ctx) else {
        return VelloSparseStatus::NullPointer;
    };
    ctx.set_paint_transform(transform.into());
    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_reset_paint_transform(
    ctx: *mut RenderContext,
) -> VelloSparseStatus {
    clear_last_error();
    let Ok(ctx) = context_from_raw(ctx) else {
        return VelloSparseStatus::NullPointer;
    };
    ctx.reset_paint_transform();
    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_set_aliasing_threshold(
    ctx: *mut RenderContext,
    threshold: i32,
) -> VelloSparseStatus {
    clear_last_error();
    if threshold < -1 || threshold > 255 {
        return VelloSparseStatus::InvalidArgument;
    }
    let Ok(ctx) = context_from_raw(ctx) else {
        return VelloSparseStatus::NullPointer;
    };
    if threshold < 0 {
        ctx.set_aliasing_threshold(None);
    } else {
        ctx.set_aliasing_threshold(Some(threshold as u8));
    }
    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_set_solid_paint(
    ctx: *mut RenderContext,
    color: VelloSparseColor,
) -> VelloSparseStatus {
    clear_last_error();
    let Ok(ctx) = context_from_raw(ctx) else {
        return VelloSparseStatus::NullPointer;
    };
    ctx.set_paint(Color::from(color));
    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_set_stroke(
    ctx: *mut RenderContext,
    style: VelloSparseStrokeStyle,
) -> VelloSparseStatus {
    clear_last_error();
    let Ok(ctx) = context_from_raw(ctx) else {
        return VelloSparseStatus::NullPointer;
    };
    let stroke = match style.to_stroke() {
        Ok(stroke) => stroke,
        Err(status) => return status,
    };
    ctx.set_stroke(stroke);
    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_fill_path(
    ctx: *mut RenderContext,
    elements: *const VelloSparsePathElement,
    element_count: usize,
) -> VelloSparseStatus {
    clear_last_error();
    let Ok(ctx) = context_from_raw(ctx) else {
        return VelloSparseStatus::NullPointer;
    };
    let slice = match unsafe { slice_from_raw(elements, element_count) } {
        Ok(slice) => slice,
        Err(status) => return status,
    };
    let path = match build_bez_path(slice) {
        Ok(path) => path,
        Err(err) => {
            set_last_error(err);
            return VelloSparseStatus::InvalidArgument;
        }
    };
    ctx.fill_path(&path);
    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_stroke_path(
    ctx: *mut RenderContext,
    elements: *const VelloSparsePathElement,
    element_count: usize,
) -> VelloSparseStatus {
    clear_last_error();
    let Ok(ctx) = context_from_raw(ctx) else {
        return VelloSparseStatus::NullPointer;
    };
    let slice = match unsafe { slice_from_raw(elements, element_count) } {
        Ok(slice) => slice,
        Err(status) => return status,
    };
    let path = match build_bez_path(slice) {
        Ok(path) => path,
        Err(err) => {
            set_last_error(err);
            return VelloSparseStatus::InvalidArgument;
        }
    };
    ctx.stroke_path(&path);
    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_fill_rect(
    ctx: *mut RenderContext,
    rect: VelloSparseRect,
) -> VelloSparseStatus {
    clear_last_error();
    let Ok(ctx) = context_from_raw(ctx) else {
        return VelloSparseStatus::NullPointer;
    };
    let rect = match rect_from_ffi(rect) {
        Ok(rect) => rect,
        Err(status) => return status,
    };
    ctx.fill_rect(&rect);
    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_stroke_rect(
    ctx: *mut RenderContext,
    rect: VelloSparseRect,
) -> VelloSparseStatus {
    clear_last_error();
    let Ok(ctx) = context_from_raw(ctx) else {
        return VelloSparseStatus::NullPointer;
    };
    let rect = match rect_from_ffi(rect) {
        Ok(rect) => rect,
        Err(status) => return status,
    };
    ctx.stroke_rect(&rect);
    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_render_to_buffer(
    ctx: *mut RenderContext,
    buffer: *mut u8,
    length: usize,
    width: u16,
    height: u16,
    mode: VelloSparseRenderMode,
) -> VelloSparseStatus {
    clear_last_error();
    if buffer.is_null() {
        return VelloSparseStatus::NullPointer;
    }
    let Ok(ctx) = context_from_raw(ctx) else {
        return VelloSparseStatus::NullPointer;
    };
    if ctx.width() != width || ctx.height() != height {
        set_last_error("Provided dimensions do not match render context size");
        return VelloSparseStatus::InvalidArgument;
    }
    let pixel_count = match usize::from(width).checked_mul(usize::from(height)) {
        Some(count) => count,
        None => {
            set_last_error("Pixel count overflow");
            return VelloSparseStatus::InvalidArgument;
        }
    };
    let expected_len = match pixel_count.checked_mul(4) {
        Some(len) => len,
        None => {
            set_last_error("Buffer length overflow");
            return VelloSparseStatus::InvalidArgument;
        }
    };
    if length < expected_len {
        set_last_error("Buffer is too small for requested dimensions");
        return VelloSparseStatus::InvalidArgument;
    }
    let slice = unsafe { slice::from_raw_parts_mut(buffer, expected_len) };
    ctx.flush();
    ctx.render_to_buffer(slice, width, height, mode.into());
    VelloSparseStatus::Success
}
