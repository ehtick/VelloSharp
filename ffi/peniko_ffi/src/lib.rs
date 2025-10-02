#![cfg_attr(not(test), deny(clippy::all))]
#![allow(clippy::missing_safety_doc)]
#![allow(missing_docs)]
#![allow(missing_debug_implementations)]

use std::{
    cell::RefCell,
    ffi::{CString, c_char},
    mem, ptr, slice,
    vec::Vec,
};

use peniko::color::Srgb;
use peniko::kurbo::Point;
use peniko::{
    Brush, Color, ColorStop, ColorStops, Extend, Gradient, GradientKind, LinearGradientPosition,
    RadialGradientPosition, SweepGradientPosition,
};

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

unsafe fn slice_from_raw<'a, T>(ptr: *const T, len: usize) -> Result<&'a [T], PenikoStatus> {
    if len == 0 {
        Ok(&[])
    } else if ptr.is_null() {
        Err(PenikoStatus::NullPointer)
    } else {
        Ok(unsafe { slice::from_raw_parts(ptr, len) })
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum PenikoStatus {
    Success = 0,
    NullPointer = 1,
    InvalidArgument = 2,
    OutOfMemory = 3,
    Unsupported = 4,
}

#[unsafe(no_mangle)]
pub extern "C" fn peniko_last_error_message() -> *const c_char {
    LAST_ERROR.with(|slot| match slot.borrow().as_ref() {
        Some(cstr) => cstr.as_ptr(),
        None => ptr::null(),
    })
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct PenikoPoint {
    pub x: f64,
    pub y: f64,
}

impl From<Point> for PenikoPoint {
    fn from(value: Point) -> Self {
        Self {
            x: value.x,
            y: value.y,
        }
    }
}

impl From<PenikoPoint> for Point {
    fn from(value: PenikoPoint) -> Self {
        Point::new(value.x, value.y)
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct PenikoColor {
    pub r: f32,
    pub g: f32,
    pub b: f32,
    pub a: f32,
}

impl From<Color> for PenikoColor {
    fn from(value: Color) -> Self {
        let [r, g, b, a] = value.components;
        Self { r, g, b, a }
    }
}

impl From<PenikoColor> for Color {
    fn from(value: PenikoColor) -> Self {
        Color::new([value.r, value.g, value.b, value.a])
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct PenikoColorStop {
    pub offset: f32,
    pub color: PenikoColor,
}

fn color_stops_from_slice(stops: &[PenikoColorStop]) -> ColorStops {
    let mut converted = Vec::with_capacity(stops.len());
    for stop in stops {
        converted.push(ColorStop::from((stop.offset, Color::from(stop.color))));
    }
    ColorStops::from(converted.as_slice())
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum PenikoExtend {
    Pad = 0,
    Repeat = 1,
    Reflect = 2,
}

fn extend_from_ffi(value: PenikoExtend) -> Extend {
    match value {
        PenikoExtend::Pad => Extend::Pad,
        PenikoExtend::Repeat => Extend::Repeat,
        PenikoExtend::Reflect => Extend::Reflect,
    }
}

fn extend_to_ffi(value: Extend) -> PenikoExtend {
    match value {
        Extend::Pad => PenikoExtend::Pad,
        Extend::Repeat => PenikoExtend::Repeat,
        Extend::Reflect => PenikoExtend::Reflect,
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct PenikoLinearGradient {
    pub start: PenikoPoint,
    pub end: PenikoPoint,
}

impl From<LinearGradientPosition> for PenikoLinearGradient {
    fn from(value: LinearGradientPosition) -> Self {
        Self {
            start: PenikoPoint::from(value.start),
            end: PenikoPoint::from(value.end),
        }
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct PenikoRadialGradient {
    pub start_center: PenikoPoint,
    pub start_radius: f32,
    pub end_center: PenikoPoint,
    pub end_radius: f32,
}

impl From<RadialGradientPosition> for PenikoRadialGradient {
    fn from(value: RadialGradientPosition) -> Self {
        Self {
            start_center: PenikoPoint::from(value.start_center),
            start_radius: value.start_radius,
            end_center: PenikoPoint::from(value.end_center),
            end_radius: value.end_radius,
        }
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct PenikoSweepGradient {
    pub center: PenikoPoint,
    pub start_angle: f32,
    pub end_angle: f32,
}

impl From<SweepGradientPosition> for PenikoSweepGradient {
    fn from(value: SweepGradientPosition) -> Self {
        Self {
            center: PenikoPoint::from(value.center),
            start_angle: value.start_angle,
            end_angle: value.end_angle,
        }
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum PenikoGradientKind {
    Linear = 0,
    Radial = 1,
    Sweep = 2,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum PenikoBrushKind {
    Solid = 0,
    Gradient = 1,
    Image = 2,
}

#[repr(C)]
pub struct PenikoBrushHandle {
    brush: Brush,
}

fn brush_from_kind(brush: &Brush) -> PenikoBrushKind {
    match brush {
        Brush::Solid(_) => PenikoBrushKind::Solid,
        Brush::Gradient(_) => PenikoBrushKind::Gradient,
        Brush::Image(_) => PenikoBrushKind::Image,
    }
}

fn copy_stops(
    stops: &ColorStops,
    out_stops: *mut PenikoColorStop,
    capacity: usize,
    out_len: *mut usize,
) -> Result<(), PenikoStatus> {
    let len = stops.len();
    if !out_len.is_null() {
        unsafe {
            *out_len = len;
        }
    }
    if len == 0 {
        return Ok(());
    }
    if capacity < len {
        return Err(PenikoStatus::InvalidArgument);
    }
    let Some(out_ptr) = (!out_stops.is_null()).then_some(out_stops) else {
        return Err(PenikoStatus::NullPointer);
    };
    for (index, stop) in stops.iter().enumerate() {
        let color = stop.color.to_alpha_color::<Srgb>();
        unsafe {
            *out_ptr.add(index) = PenikoColorStop {
                offset: stop.offset,
                color: PenikoColor::from(color),
            };
        }
    }
    Ok(())
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_brush_create_solid(color: PenikoColor) -> *mut PenikoBrushHandle {
    clear_last_error();
    let brush = Brush::Solid(Color::from(color));
    Box::into_raw(Box::new(PenikoBrushHandle { brush }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_brush_create_linear(
    gradient: PenikoLinearGradient,
    extend: PenikoExtend,
    stops: *const PenikoColorStop,
    stop_count: usize,
) -> *mut PenikoBrushHandle {
    clear_last_error();
    let Ok(stop_slice) = (unsafe { slice_from_raw(stops, stop_count) }) else {
        set_last_error("Gradient stops pointer is null");
        return ptr::null_mut();
    };
    let mut gradient_value = Gradient::new_linear(gradient.start, gradient.end);
    gradient_value.extend = extend_from_ffi(extend);
    gradient_value.stops = color_stops_from_slice(stop_slice);
    Box::into_raw(Box::new(PenikoBrushHandle {
        brush: Brush::Gradient(gradient_value),
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_brush_create_radial(
    gradient: PenikoRadialGradient,
    extend: PenikoExtend,
    stops: *const PenikoColorStop,
    stop_count: usize,
) -> *mut PenikoBrushHandle {
    clear_last_error();
    let Ok(stop_slice) = (unsafe { slice_from_raw(stops, stop_count) }) else {
        set_last_error("Gradient stops pointer is null");
        return ptr::null_mut();
    };
    let mut gradient_value = Gradient::new_two_point_radial(
        gradient.start_center,
        gradient.start_radius,
        gradient.end_center,
        gradient.end_radius,
    );
    gradient_value.extend = extend_from_ffi(extend);
    gradient_value.stops = color_stops_from_slice(stop_slice);
    Box::into_raw(Box::new(PenikoBrushHandle {
        brush: Brush::Gradient(gradient_value),
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_brush_create_sweep(
    gradient: PenikoSweepGradient,
    extend: PenikoExtend,
    stops: *const PenikoColorStop,
    stop_count: usize,
) -> *mut PenikoBrushHandle {
    clear_last_error();
    let Ok(stop_slice) = (unsafe { slice_from_raw(stops, stop_count) }) else {
        set_last_error("Gradient stops pointer is null");
        return ptr::null_mut();
    };
    let mut gradient_value =
        Gradient::new_sweep(gradient.center, gradient.start_angle, gradient.end_angle);
    gradient_value.extend = extend_from_ffi(extend);
    gradient_value.stops = color_stops_from_slice(stop_slice);
    Box::into_raw(Box::new(PenikoBrushHandle {
        brush: Brush::Gradient(gradient_value),
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_brush_clone(
    brush: *const PenikoBrushHandle,
) -> *mut PenikoBrushHandle {
    clear_last_error();
    let Some(handle) = (unsafe { brush.as_ref() }) else {
        set_last_error("Brush pointer is null");
        return ptr::null_mut();
    };
    Box::into_raw(Box::new(PenikoBrushHandle {
        brush: handle.brush.clone(),
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_brush_destroy(brush: *mut PenikoBrushHandle) {
    if !brush.is_null() {
        unsafe { drop(Box::from_raw(brush)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_brush_get_kind(
    brush: *const PenikoBrushHandle,
    out_kind: *mut PenikoBrushKind,
) -> PenikoStatus {
    clear_last_error();
    let Some(handle) = (unsafe { brush.as_ref() }) else {
        set_last_error("Brush pointer is null");
        return PenikoStatus::NullPointer;
    };
    let Some(out_kind) = (unsafe { out_kind.as_mut() }) else {
        set_last_error("Output pointer is null");
        return PenikoStatus::NullPointer;
    };
    *out_kind = brush_from_kind(&handle.brush);
    PenikoStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_brush_get_solid_color(
    brush: *const PenikoBrushHandle,
    out_color: *mut PenikoColor,
) -> PenikoStatus {
    clear_last_error();
    let Some(handle) = (unsafe { brush.as_ref() }) else {
        set_last_error("Brush pointer is null");
        return PenikoStatus::NullPointer;
    };
    let Some(out_color) = (unsafe { out_color.as_mut() }) else {
        set_last_error("Output pointer is null");
        return PenikoStatus::NullPointer;
    };
    match &handle.brush {
        Brush::Solid(color) => {
            *out_color = PenikoColor::from(*color);
            PenikoStatus::Success
        }
        _ => {
            set_last_error("Brush is not a solid color");
            PenikoStatus::InvalidArgument
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_brush_get_gradient_kind(
    brush: *const PenikoBrushHandle,
    out_kind: *mut PenikoGradientKind,
) -> PenikoStatus {
    clear_last_error();
    let Some(handle) = (unsafe { brush.as_ref() }) else {
        set_last_error("Brush pointer is null");
        return PenikoStatus::NullPointer;
    };
    let gradient = match &handle.brush {
        Brush::Gradient(gradient) => gradient,
        _ => {
            set_last_error("Brush is not a gradient");
            return PenikoStatus::InvalidArgument;
        }
    };
    let Some(out_kind) = (unsafe { out_kind.as_mut() }) else {
        set_last_error("Output pointer is null");
        return PenikoStatus::NullPointer;
    };
    *out_kind = match gradient.kind {
        GradientKind::Linear(_) => PenikoGradientKind::Linear,
        GradientKind::Radial(_) => PenikoGradientKind::Radial,
        GradientKind::Sweep(_) => PenikoGradientKind::Sweep,
    };
    PenikoStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_brush_get_linear_gradient(
    brush: *const PenikoBrushHandle,
    out_gradient: *mut PenikoLinearGradient,
    out_extend: *mut PenikoExtend,
    out_stops: *mut PenikoColorStop,
    stop_capacity: usize,
    out_stop_len: *mut usize,
) -> PenikoStatus {
    clear_last_error();
    let Some(handle) = (unsafe { brush.as_ref() }) else {
        set_last_error("Brush pointer is null");
        return PenikoStatus::NullPointer;
    };
    let gradient = match &handle.brush {
        Brush::Gradient(gradient) => gradient,
        _ => {
            set_last_error("Brush is not a gradient");
            return PenikoStatus::InvalidArgument;
        }
    };
    let linear = match gradient.kind {
        GradientKind::Linear(linear) => linear,
        _ => {
            set_last_error("Gradient is not linear");
            return PenikoStatus::InvalidArgument;
        }
    };
    if let Some(out) = unsafe { out_gradient.as_mut() } {
        *out = PenikoLinearGradient::from(linear);
    }
    if let Some(out) = unsafe { out_extend.as_mut() } {
        *out = extend_to_ffi(gradient.extend);
    }
    if let Err(status) = copy_stops(&gradient.stops, out_stops, stop_capacity, out_stop_len) {
        return status;
    }
    PenikoStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_brush_get_radial_gradient(
    brush: *const PenikoBrushHandle,
    out_gradient: *mut PenikoRadialGradient,
    out_extend: *mut PenikoExtend,
    out_stops: *mut PenikoColorStop,
    stop_capacity: usize,
    out_stop_len: *mut usize,
) -> PenikoStatus {
    clear_last_error();
    let Some(handle) = (unsafe { brush.as_ref() }) else {
        set_last_error("Brush pointer is null");
        return PenikoStatus::NullPointer;
    };
    let gradient = match &handle.brush {
        Brush::Gradient(gradient) => gradient,
        _ => {
            set_last_error("Brush is not a gradient");
            return PenikoStatus::InvalidArgument;
        }
    };
    let radial = match gradient.kind {
        GradientKind::Radial(radial) => radial,
        _ => {
            set_last_error("Gradient is not radial");
            return PenikoStatus::InvalidArgument;
        }
    };
    if let Some(out) = unsafe { out_gradient.as_mut() } {
        *out = PenikoRadialGradient::from(radial);
    }
    if let Some(out) = unsafe { out_extend.as_mut() } {
        *out = extend_to_ffi(gradient.extend);
    }
    if let Err(status) = copy_stops(&gradient.stops, out_stops, stop_capacity, out_stop_len) {
        return status;
    }
    PenikoStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_brush_get_sweep_gradient(
    brush: *const PenikoBrushHandle,
    out_gradient: *mut PenikoSweepGradient,
    out_extend: *mut PenikoExtend,
    out_stops: *mut PenikoColorStop,
    stop_capacity: usize,
    out_stop_len: *mut usize,
) -> PenikoStatus {
    clear_last_error();
    let Some(handle) = (unsafe { brush.as_ref() }) else {
        set_last_error("Brush pointer is null");
        return PenikoStatus::NullPointer;
    };
    let gradient = match &handle.brush {
        Brush::Gradient(gradient) => gradient,
        _ => {
            set_last_error("Brush is not a gradient");
            return PenikoStatus::InvalidArgument;
        }
    };
    let sweep = match gradient.kind {
        GradientKind::Sweep(sweep) => sweep,
        _ => {
            set_last_error("Gradient is not sweep");
            return PenikoStatus::InvalidArgument;
        }
    };
    if let Some(out) = unsafe { out_gradient.as_mut() } {
        *out = PenikoSweepGradient::from(sweep);
    }
    if let Some(out) = unsafe { out_extend.as_mut() } {
        *out = extend_to_ffi(gradient.extend);
    }
    if let Err(status) = copy_stops(&gradient.stops, out_stops, stop_capacity, out_stop_len) {
        return status;
    }
    PenikoStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_brush_with_alpha(
    brush: *mut PenikoBrushHandle,
    alpha: f32,
) -> PenikoStatus {
    clear_last_error();
    let Some(handle) = (unsafe { brush.as_mut() }) else {
        set_last_error("Brush pointer is null");
        return PenikoStatus::NullPointer;
    };
    let brush = mem::take(&mut handle.brush);
    handle.brush = brush.with_alpha(alpha);
    PenikoStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_brush_multiply_alpha(
    brush: *mut PenikoBrushHandle,
    alpha: f32,
) -> PenikoStatus {
    clear_last_error();
    let Some(handle) = (unsafe { brush.as_mut() }) else {
        set_last_error("Brush pointer is null");
        return PenikoStatus::NullPointer;
    };
    let brush = mem::take(&mut handle.brush);
    handle.brush = brush.multiply_alpha(alpha);
    PenikoStatus::Success
}
