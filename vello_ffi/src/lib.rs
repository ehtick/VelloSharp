#![cfg_attr(not(test), deny(clippy::all))]
#![allow(clippy::missing_safety_doc)]
#![allow(missing_docs)]
#![allow(missing_debug_implementations)]

use std::{
    cell::RefCell,
    convert::TryFrom,
    ffi::{CStr, CString, c_char, c_ulong, c_void},
    mem,
    num::{NonZeroIsize, NonZeroUsize},
    ptr::NonNull,
    slice,
};

use futures_intrusive::channel::shared::oneshot_channel;
use peniko::{
    kurbo::{Affine, BezPath, Cap, Join, Rect, Stroke, Vec2},
    BlendMode, Blob, Brush, BrushRef, Color, ColorStop, ColorStops, Extend, Fill, FontData,
    Gradient, ImageAlphaType, ImageBrush, ImageData, ImageFormat, ImageQuality,
};
use raw_window_handle::{
    AppKitDisplayHandle, AppKitWindowHandle, RawDisplayHandle, RawWindowHandle,
    WaylandDisplayHandle, WaylandWindowHandle, Win32WindowHandle, WindowsDisplayHandle,
    XlibDisplayHandle, XlibWindowHandle,
};
use velato::{self, Composition as VelatoComposition, Renderer as VelatoRenderer};
use vello::{
    AaConfig, AaSupport, Glyph, RenderParams, Renderer, RendererOptions, Scene,
    util::{RenderContext, RenderSurface},
};
use vello_svg::{self, usvg};
use wgpu::{
    Adapter, Buffer, CompositeAlphaMode, Device, Dx12Compiler, Features, Instance,
    InstanceDescriptor, InstanceFlags, Limits, PowerPreference, Queue, RequestAdapterOptions,
    SurfaceConfiguration, SurfaceError, SurfaceTargetUnsafe, SurfaceTexture, TextureAspect,
    TextureFormat, TextureUsages, TextureView, TextureViewDescriptor, TextureViewDimension,
};

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
    use peniko::kurbo::{Affine, Rect};
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

fn features_from_bits(bits: u64) -> Result<Features, VelloStatus> {
    if bits == 0 {
        return Ok(Features::empty());
    }
    let mut remaining = bits;
    let mut features = Features::empty();
    const KNOWN_FEATURES: &[(u64, Features)] = &[
        (1 << 0, Features::TEXTURE_ADAPTER_SPECIFIC_FORMAT_FEATURES),
        (1 << 1, Features::TIMESTAMP_QUERY),
        (1 << 2, Features::PIPELINE_STATISTICS_QUERY),
        (1 << 3, Features::PUSH_CONSTANTS),
        (1 << 4, Features::TEXTURE_COMPRESSION_BC),
        (1 << 5, Features::TEXTURE_COMPRESSION_ETC2),
        (1 << 6, Features::TEXTURE_COMPRESSION_ASTC),
        (1 << 7, Features::INDIRECT_FIRST_INSTANCE),
        (1 << 8, Features::MAPPABLE_PRIMARY_BUFFERS),
    ];
    for (mask, feature) in KNOWN_FEATURES {
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
    }
}

fn texture_format_to_ffi(format: TextureFormat) -> Option<VelloWgpuTextureFormat> {
    match format {
        TextureFormat::Rgba8Unorm => Some(VelloWgpuTextureFormat::Rgba8Unorm),
        TextureFormat::Rgba8UnormSrgb => Some(VelloWgpuTextureFormat::Rgba8UnormSrgb),
        TextureFormat::Bgra8Unorm => Some(VelloWgpuTextureFormat::Bgra8Unorm),
        TextureFormat::Bgra8UnormSrgb => Some(VelloWgpuTextureFormat::Bgra8UnormSrgb),
        TextureFormat::Rgba16Float => Some(VelloWgpuTextureFormat::Rgba16Float),
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
pub unsafe extern "C" fn vello_wgpu_queue_destroy(queue: *mut VelloWgpuQueueHandle) {
    if !queue.is_null() {
        unsafe { drop(Box::from_raw(queue)) };
    }
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
    match unsafe {
        instance
            .instance
            .create_surface_unsafe(SurfaceTargetUnsafe::RawHandle {
                raw_display_handle: handles.display,
                raw_window_handle: handles.window,
            })
    } {
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
