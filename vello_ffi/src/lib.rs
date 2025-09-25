#![cfg_attr(not(test), deny(clippy::all))]
#![allow(clippy::missing_safety_doc)]
#![allow(missing_docs)]
#![allow(missing_debug_implementations)]

use std::{
    cell::RefCell,
    ffi::{CString, c_char, c_ulong, c_void},
    mem,
    num::{NonZeroIsize, NonZeroUsize},
    ptr::NonNull,
    slice,
};

use futures_intrusive::channel::shared::oneshot_channel;
use kurbo::{Affine, BezPath, Cap, Join, Rect, Stroke};
use peniko::{
    BlendMode, Blob, Brush, BrushRef, Color, ColorStop, ColorStops, Extend, Fill, FontData,
    Gradient, ImageAlphaType, ImageBrush, ImageData, ImageFormat, ImageQuality,
};
use raw_window_handle::{
    AppKitDisplayHandle, AppKitWindowHandle, RawDisplayHandle, RawWindowHandle,
    WaylandDisplayHandle, WaylandWindowHandle, Win32WindowHandle, WindowsDisplayHandle,
    XlibDisplayHandle, XlibWindowHandle,
};
use vello::{
    AaConfig, AaSupport, Glyph, RenderParams, Renderer, RendererOptions, Scene,
    util::{RenderContext, RenderSurface},
};
use wgpu::{Buffer, Device, Instance, Queue, SurfaceError, SurfaceTargetUnsafe};

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
}

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

fn build_bez_path(elements: &[VelloPathElement]) -> Result<BezPath, &'static str> {
    if elements.is_empty() {
        return Err("path is empty");
    }
    let mut path = BezPath::new();
    let mut has_move = false;
    for (idx, elem) in elements.iter().enumerate() {
        if idx == 0 {
            trace_path!(
                "native first verb raw={:?} ({}), bytes={:02X?}",
                elem.verb,
                elem.verb as i32,
                unsafe { &*(elem as *const VelloPathElement as *const [u8; 16]) }
            );
        }
        match elem.verb {
            VelloPathVerb::MoveTo => {
                has_move = true;
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
    }
    if !has_move {
        return Err("path must contain a MoveTo");
    }
    Ok(path)
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
    RendererOptions {
        use_cpu: options.use_cpu,
        antialiasing_support: support,
        num_init_threads: init_threads,
        pipeline_cache: None,
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
#[derive(Copy, Clone)]
pub union VelloWindowHandlePayload {
    pub win32: VelloWin32WindowHandle,
    pub appkit: VelloAppKitWindowHandle,
    pub wayland: VelloWaylandWindowHandle,
    pub xlib: VelloXlibWindowHandle,
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

struct RawSurfaceHandles {
    window: RawWindowHandle,
    display: RawDisplayHandle,
}

impl RawSurfaceHandles {
    fn new(window: RawWindowHandle, display: RawDisplayHandle) -> Self {
        Self { window, display }
    }
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

impl TryFrom<&VelloWindowHandle> for RawSurfaceHandles {
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
                Ok(Self::new(
                    RawWindowHandle::Win32(win32),
                    RawDisplayHandle::Windows(WindowsDisplayHandle::new()),
                ))
            }
            VelloWindowHandleKind::AppKit => {
                let payload = unsafe { handle.payload.appkit };
                let ns_view = NonNull::new(payload.ns_view).ok_or(VelloStatus::InvalidArgument)?;
                Ok(Self::new(
                    RawWindowHandle::AppKit(AppKitWindowHandle::new(ns_view)),
                    RawDisplayHandle::AppKit(AppKitDisplayHandle::new()),
                ))
            }
            VelloWindowHandleKind::Wayland => {
                let payload = unsafe { handle.payload.wayland };
                let surface = NonNull::new(payload.surface).ok_or(VelloStatus::InvalidArgument)?;
                let display = NonNull::new(payload.display).ok_or(VelloStatus::InvalidArgument)?;
                Ok(Self::new(
                    RawWindowHandle::Wayland(WaylandWindowHandle::new(surface)),
                    RawDisplayHandle::Wayland(WaylandDisplayHandle::new(display)),
                ))
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
                Ok(Self::new(
                    RawWindowHandle::Xlib(window),
                    RawDisplayHandle::Xlib(xlib_display),
                ))
            }
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
    use kurbo::{Affine, Rect};
    use std::ptr;
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
        let handles = match RawSurfaceHandles::try_from(&descriptor.handle) {
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

        let surface_raw = match unsafe {
            ctx.inner
                .instance
                .create_surface_unsafe(SurfaceTargetUnsafe::RawHandle {
                    raw_display_handle: handles.display,
                    raw_window_handle: handles.window,
                })
        } {
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
    Image = 3,
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
pub struct VelloRendererOptions {
    pub use_cpu: bool,
    pub support_area: bool,
    pub support_msaa8: bool,
    pub support_msaa16: bool,
    pub init_threads: i32,
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

#[repr(C)]
pub struct VelloImageHandle {
    image: ImageData,
}

#[repr(C)]
pub struct VelloFontHandle {
    font: FontData,
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
            | wgpu::TextureUsages::STORAGE_BINDING,
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
    let blend_mode = blend_mode_from_params(&params);
    scene
        .inner
        .push_layer(blend_mode, params.alpha, params.transform.into(), &path);
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
    Box::into_raw(Box::new(VelloImageHandle { image }))
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_image_destroy(image: *mut VelloImageHandle) {
    if !image.is_null() {
        unsafe { drop(Box::from_raw(image)) };
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
