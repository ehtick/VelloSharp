#![cfg_attr(not(test), deny(clippy::all))]
#![allow(clippy::missing_safety_doc)]
#![allow(missing_docs)]
#![allow(missing_debug_implementations)]

use std::{
    cell::RefCell,
    ffi::{CString, c_char},
    ptr, slice,
    sync::Arc,
    vec::Vec,
};

use peniko::color::{ColorSpace as _, DisplayP3, LinearSrgb, OpaqueColor, Srgb, XyzD50};
use peniko::kurbo::Point;
use peniko::{
    Blob, Brush, Color, ColorStop, ColorStops, Extend, Gradient, GradientKind, ImageAlphaType,
    ImageBrush, ImageData, ImageFormat, ImageQuality, LinearGradientPosition,
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
pub struct PenikoColorSpaceTransferFn {
    pub g: f32,
    pub a: f32,
    pub b: f32,
    pub c: f32,
    pub d: f32,
    pub e: f32,
    pub f: f32,
}

impl PenikoColorSpaceTransferFn {
    const SRGB: Self = Self {
        g: 2.4,
        a: 1.0,
        b: 0.055,
        c: 1.0 / 1.055,
        d: 0.040_45,
        e: 1.0 / 12.92,
        f: 0.0,
    };

    const LINEAR: Self = Self {
        g: 1.0,
        a: 1.0,
        b: 0.0,
        c: 0.0,
        d: 0.0,
        e: 0.0,
        f: 0.0,
    };
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct PenikoColorSpaceXyz {
    pub m00: f32,
    pub m01: f32,
    pub m02: f32,
    pub m10: f32,
    pub m11: f32,
    pub m12: f32,
    pub m20: f32,
    pub m21: f32,
    pub m22: f32,
}

impl PenikoColorSpaceXyz {
    const fn new(values: [f32; 9]) -> Self {
        Self {
            m00: values[0],
            m01: values[1],
            m02: values[2],
            m10: values[3],
            m11: values[4],
            m12: values[5],
            m20: values[6],
            m21: values[7],
            m22: values[8],
        }
    }
}

fn to_xyz_matrix<CS: peniko::color::ColorSpace>() -> [f32; 9] {
    let red = OpaqueColor::<CS>::new([1.0, 0.0, 0.0])
        .convert::<XyzD50>()
        .components;
    let green = OpaqueColor::<CS>::new([0.0, 1.0, 0.0])
        .convert::<XyzD50>()
        .components;
    let blue = OpaqueColor::<CS>::new([0.0, 0.0, 1.0])
        .convert::<XyzD50>()
        .components;

    [
        red[0], green[0], blue[0], red[1], green[1], blue[1], red[2], green[2], blue[2],
    ]
}

fn write_transfer_fn(
    destination: *mut PenikoColorSpaceTransferFn,
    value: PenikoColorSpaceTransferFn,
) -> PenikoStatus {
    if destination.is_null() {
        PenikoStatus::NullPointer
    } else {
        unsafe {
            *destination = value;
        }
        PenikoStatus::Success
    }
}

fn write_xyz<CS: peniko::color::ColorSpace>(destination: *mut PenikoColorSpaceXyz) -> PenikoStatus {
    if destination.is_null() {
        PenikoStatus::NullPointer
    } else {
        let matrix = PenikoColorSpaceXyz::new(to_xyz_matrix::<CS>());
        unsafe {
            *destination = matrix;
        }
        PenikoStatus::Success
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn peniko_color_space_transfer_fn_srgb(
    out_transfer_fn: *mut PenikoColorSpaceTransferFn,
) -> PenikoStatus {
    write_transfer_fn(out_transfer_fn, PenikoColorSpaceTransferFn::SRGB)
}

#[unsafe(no_mangle)]
pub extern "C" fn peniko_color_space_transfer_fn_linear_srgb(
    out_transfer_fn: *mut PenikoColorSpaceTransferFn,
) -> PenikoStatus {
    write_transfer_fn(out_transfer_fn, PenikoColorSpaceTransferFn::LINEAR)
}

#[unsafe(no_mangle)]
pub extern "C" fn peniko_color_space_transfer_fn_display_p3(
    out_transfer_fn: *mut PenikoColorSpaceTransferFn,
) -> PenikoStatus {
    write_transfer_fn(out_transfer_fn, PenikoColorSpaceTransferFn::SRGB)
}

#[unsafe(no_mangle)]
pub extern "C" fn peniko_color_space_xyz_srgb(out_xyz: *mut PenikoColorSpaceXyz) -> PenikoStatus {
    write_xyz::<Srgb>(out_xyz)
}

#[unsafe(no_mangle)]
pub extern "C" fn peniko_color_space_xyz_linear_srgb(
    out_xyz: *mut PenikoColorSpaceXyz,
) -> PenikoStatus {
    write_xyz::<LinearSrgb>(out_xyz)
}

#[unsafe(no_mangle)]
pub extern "C" fn peniko_color_space_xyz_display_p3(
    out_xyz: *mut PenikoColorSpaceXyz,
) -> PenikoStatus {
    write_xyz::<DisplayP3>(out_xyz)
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

impl Default for PenikoLinearGradient {
    fn default() -> Self {
        Self {
            start: PenikoPoint { x: 0.0, y: 0.0 },
            end: PenikoPoint { x: 0.0, y: 0.0 },
        }
    }
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

impl Default for PenikoRadialGradient {
    fn default() -> Self {
        Self {
            start_center: PenikoPoint { x: 0.0, y: 0.0 },
            start_radius: 0.0,
            end_center: PenikoPoint { x: 0.0, y: 0.0 },
            end_radius: 0.0,
        }
    }
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

impl Default for PenikoSweepGradient {
    fn default() -> Self {
        Self {
            center: PenikoPoint { x: 0.0, y: 0.0 },
            start_angle: 0.0,
            end_angle: 0.0,
        }
    }
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
pub enum PenikoImageFormat {
    Rgba8 = 0,
    Bgra8 = 1,
}

fn image_format_from_ffi(value: PenikoImageFormat) -> ImageFormat {
    match value {
        PenikoImageFormat::Rgba8 => ImageFormat::Rgba8,
        PenikoImageFormat::Bgra8 => ImageFormat::Bgra8,
    }
}

fn image_format_to_ffi(value: ImageFormat) -> PenikoImageFormat {
    match value {
        ImageFormat::Rgba8 => PenikoImageFormat::Rgba8,
        ImageFormat::Bgra8 => PenikoImageFormat::Bgra8,
        _ => PenikoImageFormat::Rgba8,
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum PenikoImageAlphaType {
    Alpha = 0,
    AlphaPremultiplied = 1,
}

fn image_alpha_from_ffi(value: PenikoImageAlphaType) -> ImageAlphaType {
    match value {
        PenikoImageAlphaType::Alpha => ImageAlphaType::Alpha,
        PenikoImageAlphaType::AlphaPremultiplied => ImageAlphaType::AlphaPremultiplied,
    }
}

fn image_alpha_to_ffi(value: ImageAlphaType) -> PenikoImageAlphaType {
    match value {
        ImageAlphaType::Alpha => PenikoImageAlphaType::Alpha,
        ImageAlphaType::AlphaPremultiplied => PenikoImageAlphaType::AlphaPremultiplied,
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum PenikoImageQuality {
    Low = 0,
    Medium = 1,
    High = 2,
}

fn image_quality_from_ffi(value: PenikoImageQuality) -> ImageQuality {
    match value {
        PenikoImageQuality::Low => ImageQuality::Low,
        PenikoImageQuality::Medium => ImageQuality::Medium,
        PenikoImageQuality::High => ImageQuality::High,
    }
}

fn image_quality_to_ffi(value: ImageQuality) -> PenikoImageQuality {
    match value {
        ImageQuality::Low => PenikoImageQuality::Low,
        ImageQuality::Medium => PenikoImageQuality::Medium,
        ImageQuality::High => PenikoImageQuality::High,
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct PenikoImageInfo {
    pub width: u32,
    pub height: u32,
    pub format: PenikoImageFormat,
    pub alpha: PenikoImageAlphaType,
    pub stride: usize,
}

#[repr(C)]
pub struct PenikoImageDataHandle {
    image: ImageData,
    stride: usize,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct PenikoImageBrushParams {
    pub image: *const PenikoImageDataHandle,
    pub x_extend: PenikoExtend,
    pub y_extend: PenikoExtend,
    pub quality: PenikoImageQuality,
    pub alpha: f32,
}

impl Default for PenikoImageBrushParams {
    fn default() -> Self {
        Self {
            image: ptr::null(),
            x_extend: PenikoExtend::Pad,
            y_extend: PenikoExtend::Pad,
            quality: PenikoImageQuality::Medium,
            alpha: 1.0,
        }
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct PenikoSerializedBrush {
    pub kind: PenikoBrushKind,
    pub gradient_kind: PenikoGradientKind,
    pub solid: PenikoColor,
    pub linear: PenikoLinearGradient,
    pub radial: PenikoRadialGradient,
    pub sweep: PenikoSweepGradient,
    pub extend: PenikoExtend,
    pub image: PenikoImageBrushParams,
}

impl Default for PenikoSerializedBrush {
    fn default() -> Self {
        Self {
            kind: PenikoBrushKind::Solid,
            gradient_kind: PenikoGradientKind::Linear,
            solid: PenikoColor {
                r: 0.0,
                g: 0.0,
                b: 0.0,
                a: 0.0,
            },
            linear: PenikoLinearGradient::default(),
            radial: PenikoRadialGradient::default(),
            sweep: PenikoSweepGradient::default(),
            extend: PenikoExtend::Pad,
            image: PenikoImageBrushParams::default(),
        }
    }
}

#[repr(C)]
struct ExternalVelloImageHandle {
    image: ImageData,
    stride: usize,
}

fn infer_stride(image: &ImageData, fallback: usize) -> usize {
    let height = image.height as usize;
    if height == 0 {
        return fallback;
    }
    let len = image.data.as_ref().len();
    if len == 0 { fallback } else { len / height }
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
    brush: Arc<Brush>,
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
pub unsafe extern "C" fn peniko_image_data_create(
    format: PenikoImageFormat,
    alpha: PenikoImageAlphaType,
    width: u32,
    height: u32,
    pixels: *const u8,
    stride: usize,
) -> *mut PenikoImageDataHandle {
    clear_last_error();
    if width == 0 || height == 0 {
        set_last_error("Image dimensions must be non-zero");
        return ptr::null_mut();
    }
    if pixels.is_null() {
        set_last_error("Pixel data pointer is null");
        return ptr::null_mut();
    }
    let row_bytes = match (width as usize).checked_mul(4) {
        Some(bytes) => bytes,
        None => {
            set_last_error("Image dimensions overflow");
            return ptr::null_mut();
        }
    };
    let src_stride = if stride == 0 { row_bytes } else { stride };
    if src_stride < row_bytes {
        set_last_error("Stride is smaller than row size");
        return ptr::null_mut();
    }
    let total_size = match src_stride.checked_mul(height as usize) {
        Some(size) => size,
        None => {
            set_last_error("Image size overflow");
            return ptr::null_mut();
        }
    };
    let src = unsafe { slice::from_raw_parts(pixels, total_size) };
    let mut buffer = vec![0u8; row_bytes * height as usize];
    for y in 0..height as usize {
        let src_offset = y * src_stride;
        let dst_offset = y * row_bytes;
        buffer[dst_offset..dst_offset + row_bytes]
            .copy_from_slice(&src[src_offset..src_offset + row_bytes]);
    }
    let blob = Blob::from(buffer);
    let image = ImageData {
        data: blob,
        format: image_format_from_ffi(format),
        alpha_type: image_alpha_from_ffi(alpha),
        width,
        height,
    };
    Box::into_raw(Box::new(PenikoImageDataHandle {
        image,
        stride: row_bytes,
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_image_data_create_from_vello(
    image: *const ExternalVelloImageHandle,
) -> *mut PenikoImageDataHandle {
    clear_last_error();
    let Some(handle) = (unsafe { image.as_ref() }) else {
        set_last_error("Image handle is null");
        return ptr::null_mut();
    };
    Box::into_raw(Box::new(PenikoImageDataHandle {
        image: handle.image.clone(),
        stride: handle.stride,
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_image_data_clone(
    image: *const PenikoImageDataHandle,
) -> *mut PenikoImageDataHandle {
    clear_last_error();
    let Some(handle) = (unsafe { image.as_ref() }) else {
        set_last_error("Image pointer is null");
        return ptr::null_mut();
    };
    Box::into_raw(Box::new(PenikoImageDataHandle {
        image: handle.image.clone(),
        stride: handle.stride,
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_image_data_destroy(image: *mut PenikoImageDataHandle) {
    if !image.is_null() {
        unsafe { drop(Box::from_raw(image)) };
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_image_data_get_info(
    image: *const PenikoImageDataHandle,
    out_info: *mut PenikoImageInfo,
) -> PenikoStatus {
    clear_last_error();
    let Some(handle) = (unsafe { image.as_ref() }) else {
        set_last_error("Image pointer is null");
        return PenikoStatus::NullPointer;
    };
    let Some(out) = (unsafe { out_info.as_mut() }) else {
        set_last_error("Output pointer is null");
        return PenikoStatus::NullPointer;
    };
    out.width = handle.image.width;
    out.height = handle.image.height;
    out.format = image_format_to_ffi(handle.image.format);
    out.alpha = image_alpha_to_ffi(handle.image.alpha_type);
    out.stride = if handle.stride == 0 {
        infer_stride(
            &handle.image,
            (handle.image.width as usize).saturating_mul(4),
        )
    } else {
        handle.stride
    };
    PenikoStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_image_data_copy_pixels(
    image: *const PenikoImageDataHandle,
    dest: *mut u8,
    dest_size: usize,
) -> PenikoStatus {
    clear_last_error();
    let Some(handle) = (unsafe { image.as_ref() }) else {
        set_last_error("Image pointer is null");
        return PenikoStatus::NullPointer;
    };
    if dest.is_null() {
        set_last_error("Destination pointer is null");
        return PenikoStatus::NullPointer;
    }
    let data = handle.image.data.as_ref();
    let slice = data.as_ref();
    if dest_size < slice.len() {
        set_last_error("Destination buffer is too small");
        return PenikoStatus::InvalidArgument;
    }
    unsafe {
        ptr::copy_nonoverlapping(slice.as_ptr(), dest, slice.len());
    }
    PenikoStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_brush_create_solid(color: PenikoColor) -> *mut PenikoBrushHandle {
    clear_last_error();
    let brush = Arc::new(Brush::Solid(Color::from(color)));
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
        brush: Arc::new(Brush::Gradient(gradient_value)),
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
        brush: Arc::new(Brush::Gradient(gradient_value)),
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
        brush: Arc::new(Brush::Gradient(gradient_value)),
    }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_brush_create_image(
    params: PenikoImageBrushParams,
) -> *mut PenikoBrushHandle {
    clear_last_error();
    let Some(image_handle) = (unsafe { params.image.as_ref() }) else {
        set_last_error("Image handle is null");
        return ptr::null_mut();
    };
    let mut brush = ImageBrush::new(image_handle.image.clone());
    brush.sampler.x_extend = extend_from_ffi(params.x_extend);
    brush.sampler.y_extend = extend_from_ffi(params.y_extend);
    brush.sampler.quality = image_quality_from_ffi(params.quality);
    brush.sampler.alpha = params.alpha;
    Box::into_raw(Box::new(PenikoBrushHandle {
        brush: Arc::new(Brush::Image(brush)),
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
        brush: Arc::clone(&handle.brush),
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
    let brush_ref = handle.brush.as_ref();
    *out_kind = brush_from_kind(brush_ref);
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
    match handle.brush.as_ref() {
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
    let gradient = match handle.brush.as_ref() {
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
    let gradient = match handle.brush.as_ref() {
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
    let gradient = match handle.brush.as_ref() {
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
    let gradient = match handle.brush.as_ref() {
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
pub unsafe extern "C" fn peniko_brush_get_image(
    brush: *const PenikoBrushHandle,
    out_params: *mut PenikoImageBrushParams,
    out_image: *mut *mut PenikoImageDataHandle,
) -> PenikoStatus {
    clear_last_error();
    let Some(handle) = (unsafe { brush.as_ref() }) else {
        set_last_error("Brush pointer is null");
        return PenikoStatus::NullPointer;
    };
    let image = match handle.brush.as_ref() {
        Brush::Image(image) => image,
        _ => {
            set_last_error("Brush is not an image");
            return PenikoStatus::InvalidArgument;
        }
    };
    let mut cloned_ptr: *mut PenikoImageDataHandle = ptr::null_mut();
    if !out_image.is_null() {
        let stride = infer_stride(&image.image, (image.image.width as usize).saturating_mul(4));
        let handle = PenikoImageDataHandle {
            image: image.image.clone(),
            stride,
        };
        cloned_ptr = Box::into_raw(Box::new(handle));
        unsafe {
            *out_image = cloned_ptr;
        }
    }
    if let Some(params) = unsafe { out_params.as_mut() } {
        params.x_extend = extend_to_ffi(image.sampler.x_extend);
        params.y_extend = extend_to_ffi(image.sampler.y_extend);
        params.quality = image_quality_to_ffi(image.sampler.quality);
        params.alpha = image.sampler.alpha;
        params.image = cloned_ptr;
    }
    PenikoStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn peniko_brush_serialize(
    brush: *const PenikoBrushHandle,
    out_serialized: *mut PenikoSerializedBrush,
    out_stops: *mut PenikoColorStop,
    stop_capacity: usize,
    out_stop_len: *mut usize,
) -> PenikoStatus {
    clear_last_error();
    let Some(handle) = (unsafe { brush.as_ref() }) else {
        set_last_error("Brush pointer is null");
        return PenikoStatus::NullPointer;
    };
    let Some(serialized) = (unsafe { out_serialized.as_mut() }) else {
        set_last_error("Output pointer is null");
        return PenikoStatus::NullPointer;
    };
    let Some(stop_len) = (unsafe { out_stop_len.as_mut() }) else {
        set_last_error("Stop length pointer is null");
        return PenikoStatus::NullPointer;
    };

    *serialized = PenikoSerializedBrush::default();
    *stop_len = 0;

    match handle.brush.as_ref() {
        Brush::Solid(color) => {
            serialized.kind = PenikoBrushKind::Solid;
            serialized.solid = PenikoColor::from(*color);
        }
        Brush::Gradient(gradient) => {
            serialized.kind = PenikoBrushKind::Gradient;
            serialized.extend = extend_to_ffi(gradient.extend);
            let len = gradient.stops.len();
            *stop_len = len;

            match gradient.kind {
                GradientKind::Linear(linear) => {
                    serialized.gradient_kind = PenikoGradientKind::Linear;
                    serialized.linear = PenikoLinearGradient::from(linear);
                }
                GradientKind::Radial(radial) => {
                    serialized.gradient_kind = PenikoGradientKind::Radial;
                    serialized.radial = PenikoRadialGradient::from(radial);
                }
                GradientKind::Sweep(sweep) => {
                    serialized.gradient_kind = PenikoGradientKind::Sweep;
                    serialized.sweep = PenikoSweepGradient::from(sweep);
                }
            }

            if len > 0 && !out_stops.is_null() {
                if let Err(status) =
                    copy_stops(&gradient.stops, out_stops, stop_capacity, out_stop_len)
                {
                    return status;
                }
            }
        }
        Brush::Image(image) => {
            serialized.kind = PenikoBrushKind::Image;
            serialized.image.x_extend = extend_to_ffi(image.sampler.x_extend);
            serialized.image.y_extend = extend_to_ffi(image.sampler.y_extend);
            serialized.image.quality = image_quality_to_ffi(image.sampler.quality);
            serialized.image.alpha = image.sampler.alpha;
            let stride = infer_stride(&image.image, (image.image.width as usize).saturating_mul(4));
            let handle = PenikoImageDataHandle {
                image: image.image.clone(),
                stride,
            };
            serialized.image.image = Box::into_raw(Box::new(handle));
        }
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
    let brush_ref = Arc::make_mut(&mut handle.brush);
    let new_brush = brush_ref.clone().with_alpha(alpha);
    *brush_ref = new_brush;
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
    let brush_ref = Arc::make_mut(&mut handle.brush);
    let new_brush = brush_ref.clone().multiply_alpha(alpha);
    *brush_ref = new_brush;
    PenikoStatus::Success
}
