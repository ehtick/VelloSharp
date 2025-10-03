#![cfg_attr(not(test), deny(clippy::all))]
#![allow(clippy::missing_safety_doc)]
#![allow(missing_docs)]
#![allow(missing_debug_implementations)]

use std::{
    cell::RefCell,
    ffi::{CString, c_char},
    ptr, slice,
};

use vello_common::glyph::Glyph as CpuGlyph;
use vello_common::{
    color::{AlphaColor, Srgb},
    kurbo::{Affine, BezPath, Cap, Join, PathEl, Point, Rect, Stroke},
    paint::{Image, PaintType},
    peniko::{
        self, BlendMode, Brush, Color, ColorStop, ColorStops, Extend, Fill, FontData, Gradient,
        ImageBrush, ImageData, ImageQuality,
    },
};
use vello_cpu::{Level, RenderContext, RenderMode, RenderSettings};

#[cfg(target_arch = "aarch64")]
use vello_common::fearless_simd::Neon;
#[cfg(all(target_arch = "wasm32", target_feature = "simd128"))]
use vello_common::fearless_simd::WasmSimd128;
#[cfg(any(target_arch = "x86", target_arch = "x86_64"))]
use vello_common::fearless_simd::{Avx2, Sse4_2};

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
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloSparseSimdLevel {
    Auto = -1,
    Fallback = 0,
    Neon = 1,
    WasmSimd128 = 2,
    Sse4_2 = 3,
    Avx2 = 4,
}

fn simd_level_from_level(level: Level) -> VelloSparseSimdLevel {
    match level {
        Level::Fallback(_) => VelloSparseSimdLevel::Fallback,
        #[cfg(target_arch = "aarch64")]
        Level::Neon(_) => VelloSparseSimdLevel::Neon,
        #[cfg(all(target_arch = "wasm32", target_feature = "simd128"))]
        Level::WasmSimd128(_) => VelloSparseSimdLevel::WasmSimd128,
        #[cfg(any(target_arch = "x86", target_arch = "x86_64"))]
        Level::Sse4_2(_) => VelloSparseSimdLevel::Sse4_2,
        #[cfg(any(target_arch = "x86", target_arch = "x86_64"))]
        Level::Avx2(_) => VelloSparseSimdLevel::Avx2,
        _ => VelloSparseSimdLevel::Fallback,
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

pub type VelloPathElement = VelloSparsePathElement;
pub type VelloAffine = VelloSparseAffine;
pub type VelloColor = VelloSparseColor;
pub type VelloRect = VelloSparseRect;
pub type VelloFillRule = VelloSparseFillRule;
pub type VelloRenderMode = VelloSparseRenderMode;
pub type VelloSimdLevel = VelloSparseSimdLevel;
pub type VelloLineCap = VelloSparseLineCap;
pub type VelloLineJoin = VelloSparseLineJoin;
pub type VelloStrokeStyle = VelloSparseStrokeStyle;

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloPoint {
    pub x: f64,
    pub y: f64,
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

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloImageBrushParams {
    pub image: *const VelloImageHandle,
    pub x_extend: VelloExtendMode,
    pub y_extend: VelloExtendMode,
    pub quality: VelloImageQualityMode,
    pub alpha: f32,
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
            VelloBlendMix::Clip => peniko::Mix::Clip,
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

fn convert_gradient_stops(stops: &[VelloGradientStop]) -> Result<ColorStops, VelloSparseStatus> {
    let mut result = ColorStops::new();
    for stop in stops {
        let color =
            AlphaColor::<Srgb>::new([stop.color.r, stop.color.g, stop.color.b, stop.color.a]);
        result.push(ColorStop::from((stop.offset, color)));
    }
    Ok(result)
}

fn convert_blend_mix(mix: VelloBlendMix) -> peniko::Mix {
    match mix {
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
        VelloBlendMix::Clip => unreachable!("Clip mix should be handled separately"),
    }
}

fn gradient_from_linear(linear: &VelloLinearGradient) -> Result<Gradient, VelloSparseStatus> {
    let stops = unsafe { slice_from_raw(linear.stops, linear.stop_count)? };
    let mut gradient = Gradient::new_linear(
        (linear.start.x, linear.start.y),
        (linear.end.x, linear.end.y),
    );
    gradient.extend = linear.extend.into();
    gradient.stops = convert_gradient_stops(stops)?;
    Ok(gradient)
}

fn gradient_from_radial(radial: &VelloRadialGradient) -> Result<Gradient, VelloSparseStatus> {
    if radial.start_radius < 0.0 || radial.end_radius < 0.0 {
        return Err(VelloSparseStatus::InvalidArgument);
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

fn gradient_from_sweep(sweep: &VelloSweepGradient) -> Result<Gradient, VelloSparseStatus> {
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

fn image_brush_from_params(
    params: &VelloImageBrushParams,
) -> Result<ImageBrush, VelloSparseStatus> {
    let Some(handle) = (unsafe { params.image.as_ref() }) else {
        return Err(VelloSparseStatus::NullPointer);
    };
    let mut brush = ImageBrush::new(handle.image.clone());
    brush.sampler.x_extend = params.x_extend.into();
    brush.sampler.y_extend = params.y_extend.into();
    brush.sampler.quality = params.quality.into();
    brush.sampler.alpha = params.alpha;
    Ok(brush)
}

fn brush_to_peniko_brush(brush: &VelloBrush) -> Result<Brush, VelloSparseStatus> {
    match brush.kind {
        VelloBrushKind::Solid => Ok(Brush::Solid(AlphaColor::<Srgb>::new([
            brush.solid.r,
            brush.solid.g,
            brush.solid.b,
            brush.solid.a,
        ]))),
        VelloBrushKind::LinearGradient => Ok(Brush::Gradient(gradient_from_linear(&brush.linear)?)),
        VelloBrushKind::RadialGradient => Ok(Brush::Gradient(gradient_from_radial(&brush.radial)?)),
        VelloBrushKind::SweepGradient => Ok(Brush::Gradient(gradient_from_sweep(&brush.sweep)?)),
        VelloBrushKind::Image => Ok(Brush::Image(image_brush_from_params(&brush.image)?)),
    }
}

fn paint_from_brush(brush: &VelloBrush, brush_alpha: f32) -> Result<PaintType, VelloSparseStatus> {
    let base_brush = brush_to_peniko_brush(brush)?;
    let alpha = brush_alpha.clamp(0.0, 1.0);
    let brush = base_brush.multiply_alpha(alpha);
    match brush {
        Brush::Solid(color) => Ok(PaintType::from(color)),
        Brush::Gradient(gradient) => Ok(PaintType::from(gradient)),
        Brush::Image(image_brush) => {
            let image = Image::from_peniko_image(&image_brush);
            Ok(PaintType::from(image))
        }
    }
}

fn optional_affine(ptr: *const VelloAffine) -> Result<Option<Affine>, VelloSparseStatus> {
    if ptr.is_null() {
        Ok(None)
    } else {
        let affine = unsafe { (*ptr).into() };
        Ok(Some(affine))
    }
}

fn blend_mode_from_params(params: &VelloLayerParams) -> BlendMode {
    BlendMode::new(convert_blend_mix(params.mix), params.compose.into())
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

fn resolve_simd_level(
    level: VelloSparseSimdLevel,
    default_level: Level,
) -> Result<Level, &'static str> {
    match level {
        VelloSparseSimdLevel::Auto => Ok(default_level),
        VelloSparseSimdLevel::Fallback => Ok(Level::fallback()),
        VelloSparseSimdLevel::Neon => {
            #[cfg(target_arch = "aarch64")]
            unsafe {
                return Ok(Level::Neon(Neon::new_unchecked()));
            }
            #[cfg(not(target_arch = "aarch64"))]
            {
                Err("Neon SIMD level is not supported on this architecture.")
            }
        }
        VelloSparseSimdLevel::WasmSimd128 => {
            #[cfg(all(target_arch = "wasm32", target_feature = "simd128"))]
            {
                return Ok(Level::WasmSimd128(WasmSimd128::new_unchecked()));
            }
            #[cfg(not(all(target_arch = "wasm32", target_feature = "simd128")))]
            {
                Err("Wasm SIMD128 level is not supported on this architecture.")
            }
        }
        VelloSparseSimdLevel::Sse4_2 => {
            #[cfg(any(target_arch = "x86", target_arch = "x86_64"))]
            unsafe {
                return Ok(Level::Sse4_2(Sse4_2::new_unchecked()));
            }
            #[cfg(not(any(target_arch = "x86", target_arch = "x86_64")))]
            {
                Err("SSE4.2 level is not supported on this architecture.")
            }
        }
        VelloSparseSimdLevel::Avx2 => {
            #[cfg(any(target_arch = "x86", target_arch = "x86_64"))]
            unsafe {
                return Ok(Level::Avx2(Avx2::new_unchecked()));
            }
            #[cfg(not(any(target_arch = "x86", target_arch = "x86_64")))]
            {
                Err("AVX2 level is not supported on this architecture.")
            }
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_create_with_options(
    width: u16,
    height: u16,
    enable_multithreading: bool,
    thread_count: u16,
    simd_level: VelloSparseSimdLevel,
) -> *mut RenderContext {
    clear_last_error();
    if width == 0 || height == 0 {
        set_last_error("Render context dimensions must be non-zero");
        return ptr::null_mut();
    }

    let mut settings = RenderSettings::default();

    let level = match resolve_simd_level(simd_level, settings.level) {
        Ok(level) => level,
        Err(message) => {
            set_last_error(message);
            return ptr::null_mut();
        }
    };

    settings.level = level;

    if !enable_multithreading {
        settings.num_threads = 0;
    } else if thread_count > 0 {
        settings.num_threads = thread_count;
    }

    let context = RenderContext::new_with(width, height, settings);
    Box::into_raw(Box::new(context))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_create(
    width: u16,
    height: u16,
) -> *mut RenderContext {
    unsafe {
        vello_sparse_render_context_create_with_options(
            width,
            height,
            true,
            0,
            VelloSparseSimdLevel::Auto,
        )
    }
}

#[unsafe(no_mangle)]
pub extern "C" fn vello_sparse_detect_simd_level() -> VelloSparseSimdLevel {
    simd_level_from_level(Level::new())
}

#[unsafe(no_mangle)]
pub extern "C" fn vello_sparse_detect_thread_count() -> u16 {
    std::thread::available_parallelism()
        .map(|value| value.get().min(u16::MAX as usize) as u16)
        .unwrap_or(0)
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
    color: VelloColor,
) -> VelloSparseStatus {
    clear_last_error();
    let Ok(ctx) = context_from_raw(ctx) else {
        return VelloSparseStatus::NullPointer;
    };
    ctx.set_paint(AlphaColor::<Srgb>::new([
        color.r, color.g, color.b, color.a,
    ]));
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
    fill_rule: VelloFillRule,
    transform: VelloAffine,
    color: VelloColor,
    elements: *const VelloPathElement,
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

    let previous_fill_rule = *ctx.fill_rule();
    let previous_transform = *ctx.transform();
    let previous_paint = ctx.paint().clone();

    ctx.set_fill_rule(fill_rule.into());
    ctx.set_transform(transform.into());
    ctx.set_paint(AlphaColor::<Srgb>::new([
        color.r, color.g, color.b, color.a,
    ]));

    ctx.fill_path(&path);

    ctx.set_paint(previous_paint);
    ctx.set_transform(previous_transform);
    ctx.set_fill_rule(previous_fill_rule);

    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_fill_path_brush(
    ctx: *mut RenderContext,
    fill_rule: VelloFillRule,
    transform: VelloAffine,
    brush: VelloBrush,
    brush_transform: *const VelloAffine,
    elements: *const VelloPathElement,
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

    let paint = match paint_from_brush(&brush, 1.0) {
        Ok(paint) => paint,
        Err(status) => return status,
    };
    let brush_transform = match optional_affine(brush_transform) {
        Ok(transform) => transform,
        Err(status) => return status,
    };

    let previous_fill_rule = *ctx.fill_rule();
    let previous_transform = *ctx.transform();
    let previous_paint = ctx.paint().clone();
    let previous_paint_transform = *ctx.paint_transform();

    ctx.set_fill_rule(fill_rule.into());
    ctx.set_transform(transform.into());
    ctx.set_paint(paint);

    if let Some(paint_transform) = brush_transform {
        ctx.set_paint_transform(paint_transform);
    } else {
        ctx.reset_paint_transform();
    }

    ctx.fill_path(&path);

    ctx.set_paint(previous_paint);
    ctx.set_transform(previous_transform);
    ctx.set_fill_rule(previous_fill_rule);
    ctx.set_paint_transform(previous_paint_transform);

    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_stroke_path(
    ctx: *mut RenderContext,
    stroke: VelloStrokeStyle,
    transform: VelloAffine,
    color: VelloColor,
    elements: *const VelloPathElement,
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

    let stroke = match stroke.to_stroke() {
        Ok(stroke) => stroke,
        Err(status) => return status,
    };

    let previous_transform = *ctx.transform();
    let previous_paint = ctx.paint().clone();
    let previous_stroke = ctx.stroke().clone();

    ctx.set_transform(transform.into());
    ctx.set_paint(AlphaColor::<Srgb>::new([
        color.r, color.g, color.b, color.a,
    ]));
    ctx.set_stroke(stroke);

    ctx.stroke_path(&path);

    ctx.set_stroke(previous_stroke);
    ctx.set_paint(previous_paint);
    ctx.set_transform(previous_transform);

    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_stroke_path_brush(
    ctx: *mut RenderContext,
    stroke: VelloStrokeStyle,
    transform: VelloAffine,
    brush: VelloBrush,
    brush_transform: *const VelloAffine,
    elements: *const VelloPathElement,
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

    let stroke = match stroke.to_stroke() {
        Ok(stroke) => stroke,
        Err(status) => return status,
    };
    let paint = match paint_from_brush(&brush, 1.0) {
        Ok(paint) => paint,
        Err(status) => return status,
    };
    let brush_transform = match optional_affine(brush_transform) {
        Ok(transform) => transform,
        Err(status) => return status,
    };

    let previous_transform = *ctx.transform();
    let previous_paint = ctx.paint().clone();
    let previous_paint_transform = *ctx.paint_transform();
    let previous_stroke = ctx.stroke().clone();

    ctx.set_transform(transform.into());
    ctx.set_paint(paint);
    ctx.set_stroke(stroke);

    if let Some(paint_transform) = brush_transform {
        ctx.set_paint_transform(paint_transform);
    } else {
        ctx.reset_paint_transform();
    }

    ctx.stroke_path(&path);

    ctx.set_paint_transform(previous_paint_transform);
    ctx.set_stroke(previous_stroke);
    ctx.set_paint(previous_paint);
    ctx.set_transform(previous_transform);

    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_fill_rect(
    ctx: *mut RenderContext,
    rect: VelloRect,
    color: VelloColor,
) -> VelloSparseStatus {
    clear_last_error();
    let Ok(ctx) = context_from_raw(ctx) else {
        return VelloSparseStatus::NullPointer;
    };
    let rect = match rect_from_ffi(rect) {
        Ok(rect) => rect,
        Err(status) => return status,
    };
    let previous_paint = ctx.paint().clone();
    ctx.set_paint(AlphaColor::<Srgb>::new([
        color.r, color.g, color.b, color.a,
    ]));
    ctx.fill_rect(&rect);
    ctx.set_paint(previous_paint);
    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_stroke_rect(
    ctx: *mut RenderContext,
    rect: VelloRect,
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
pub unsafe extern "C" fn vello_sparse_render_context_push_layer(
    ctx: *mut RenderContext,
    params: VelloLayerParams,
) -> VelloSparseStatus {
    clear_last_error();
    let Ok(ctx) = context_from_raw(ctx) else {
        return VelloSparseStatus::NullPointer;
    };

    let clip_elements =
        match unsafe { slice_from_raw(params.clip_elements, params.clip_element_count) } {
            Ok(slice) => slice,
            Err(status) => return status,
        };

    let clip_path = if clip_elements.is_empty() {
        None
    } else {
        match build_bez_path(clip_elements) {
            Ok(path) => Some(path),
            Err(err) => {
                set_last_error(err);
                return VelloSparseStatus::InvalidArgument;
            }
        }
    };

    let previous_transform = *ctx.transform();
    ctx.set_transform(params.transform.into());

    let clip_ref = clip_path.as_ref();
    let clip_option = clip_ref.map(|path| path as &BezPath);
    if params.mix == VelloBlendMix::Clip {
        let Some(path) = clip_option else {
            ctx.set_transform(previous_transform);
            return VelloSparseStatus::InvalidArgument;
        };

        ctx.push_clip_layer(path);
        ctx.set_transform(previous_transform);
        return VelloSparseStatus::Success;
    }

    let blend_mode = blend_mode_from_params(&params);

    ctx.push_layer(clip_option, Some(blend_mode), Some(params.alpha), None);

    ctx.set_transform(previous_transform);

    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_pop_layer(ctx: *mut RenderContext) {
    if let Ok(ctx) = context_from_raw(ctx) {
        ctx.pop_layer();
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_draw_image(
    ctx: *mut RenderContext,
    brush: VelloImageBrushParams,
    transform: VelloAffine,
) -> VelloSparseStatus {
    clear_last_error();
    let Ok(ctx) = context_from_raw(ctx) else {
        return VelloSparseStatus::NullPointer;
    };

    let image_brush = match image_brush_from_params(&brush) {
        Ok(brush) => brush,
        Err(status) => return status,
    };

    let width = f64::from(image_brush.image.width);
    let height = f64::from(image_brush.image.height);
    if width <= 0.0 || height <= 0.0 {
        return VelloSparseStatus::InvalidArgument;
    }

    let image = Image::from_peniko_image(&image_brush);

    let previous_transform = *ctx.transform();
    let previous_paint = ctx.paint().clone();
    let previous_paint_transform = *ctx.paint_transform();

    ctx.set_transform(transform.into());
    ctx.set_paint(PaintType::from(image));
    ctx.reset_paint_transform();

    let rect = Rect::new(0.0, 0.0, width, height);
    ctx.fill_rect(&rect);

    ctx.set_paint_transform(previous_paint_transform);
    ctx.set_paint(previous_paint);
    ctx.set_transform(previous_transform);

    VelloSparseStatus::Success
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_sparse_render_context_draw_glyph_run(
    ctx: *mut RenderContext,
    font: *const VelloFontHandle,
    glyphs: *const VelloGlyph,
    glyph_count: usize,
    options: VelloGlyphRunOptions,
) -> VelloSparseStatus {
    clear_last_error();
    let Ok(ctx) = context_from_raw(ctx) else {
        return VelloSparseStatus::NullPointer;
    };
    let Some(font_handle) = (unsafe { font.as_ref() }) else {
        return VelloSparseStatus::NullPointer;
    };
    let glyph_slice = match unsafe { slice_from_raw(glyphs, glyph_count) } {
        Ok(slice) => slice,
        Err(status) => return status,
    };

    let paint = match paint_from_brush(&options.brush, options.brush_alpha) {
        Ok(paint) => paint,
        Err(status) => return status,
    };
    let glyph_transform = match optional_affine(options.glyph_transform) {
        Ok(transform) => transform,
        Err(status) => return status,
    };

    let previous_transform = *ctx.transform();
    let previous_paint = ctx.paint().clone();
    let previous_paint_transform = *ctx.paint_transform();
    let previous_stroke = ctx.stroke().clone();

    ctx.set_transform(options.transform.into());
    ctx.set_paint(paint);
    ctx.reset_paint_transform();

    let stroke_for_run = if matches!(options.style, VelloGlyphRunStyle::Stroke) {
        match options.stroke_style.to_stroke() {
            Ok(stroke) => Some(stroke),
            Err(status) => return status,
        }
    } else {
        None
    };

    if let Some(ref stroke) = stroke_for_run {
        ctx.set_stroke(stroke.clone());
    }

    let glyph_iter = glyph_slice.iter().copied().map(|glyph| CpuGlyph {
        id: glyph.id,
        x: glyph.x,
        y: glyph.y,
    });

    let mut builder = ctx
        .glyph_run(&font_handle.font)
        .font_size(options.font_size)
        .hint(options.hint);

    if let Some(affine) = glyph_transform {
        builder = builder.glyph_transform(affine);
    }

    match options.style {
        VelloGlyphRunStyle::Fill => builder.fill_glyphs(glyph_iter),
        VelloGlyphRunStyle::Stroke => builder.stroke_glyphs(glyph_iter),
    }

    ctx.set_stroke(previous_stroke);
    ctx.set_paint_transform(previous_paint_transform);
    ctx.set_paint(previous_paint);
    ctx.set_transform(previous_transform);

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
