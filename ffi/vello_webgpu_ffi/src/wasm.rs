#![allow(dead_code)]

use crate::types::{
    VelloAaMode, VelloColor, VelloRenderFormat, VelloRenderParams, VelloRendererOptions,
    VelloWebGpuDeviceLimits, VelloWebGpuFutureKind, VelloWebGpuFuturePollResult,
    VelloWebGpuFutureState, VelloWebGpuLogLevel, VelloWebGpuPowerPreference,
    VelloWebGpuPresentMode, VelloWebGpuRequestAdapterOptions, VelloWebGpuRequestDeviceOptions,
    VelloWebGpuStatus, VelloWebGpuSurfaceConfiguration, VelloWebGpuTextureFormat,
};
use core::num::NonZeroUsize;
use js_sys::{Array, Promise};
use log::Level;
use once_cell::sync::Lazy;
use std::{
    cell::RefCell,
    collections::HashMap,
    ffi::{CStr, CString, c_char, c_void},
    mem,
    rc::Rc,
};
use vello::{
    AaConfig, AaSupport, RenderParams as RenderParamsInternal, Renderer,
    RendererOptions as RendererOptionsInternal, Scene, peniko::Color,
};
use wasm_bindgen::{JsCast, JsValue, closure::Closure};
use wasm_bindgen_futures::future_to_promise;
use web_sys::{Element, HtmlCanvasElement, window};
use wgpu::{
    Backends, CompositeAlphaMode, DeviceDescriptor, Features, InstanceDescriptor, Limits,
    PowerPreference, PresentMode, RequestAdapterError, RequestAdapterOptions, SurfaceCapabilities,
    SurfaceConfiguration, SurfaceError, SurfaceTarget, TextureUsages, TextureViewDescriptor,
};

thread_local! {
    static LAST_ERROR: RefCell<Option<CString>> = RefCell::new(None);
    static WEBGPU_STATE: RefCell<Option<WebGpuState>> = RefCell::new(None);
    static ADAPTER_REGISTRY: RefCell<AdapterRegistry> = RefCell::new(AdapterRegistry::new());
    static LOG_CALLBACK: RefCell<Option<LogCallback>> = RefCell::new(None);
}

static INIT_LOGGER: Lazy<()> = Lazy::new(|| {
    let _ = console_log::init_with_level(Level::Info);
    console_error_panic_hook::set_once();
});

type LogCallbackFn = unsafe extern "C" fn(VelloWebGpuLogLevel, *const c_char, *mut c_void);

#[derive(Copy, Clone)]
struct LogCallback {
    func: LogCallbackFn,
    user_data: *mut c_void,
}

#[repr(C)]
struct VelloSceneHandle {
    inner: Scene,
}

struct WebGpuState {
    instance: wgpu::Instance,
    futures: FutureRegistry,
    devices: DeviceRegistry,
    queues: QueueRegistry,
    surfaces: SurfaceRegistry,
    textures: TextureRegistry,
    texture_views: TextureViewRegistry,
    renderers: RendererRegistry,
}

impl WebGpuState {
    fn new(instance: wgpu::Instance) -> Self {
        Self {
            instance,
            futures: FutureRegistry::new(),
            devices: DeviceRegistry::new(),
            queues: QueueRegistry::new(),
            surfaces: SurfaceRegistry::new(),
            textures: TextureRegistry::new(),
            texture_views: TextureViewRegistry::new(),
            renderers: RendererRegistry::new(),
        }
    }

    fn insert_device(&mut self, device: wgpu::Device, queue: wgpu::Queue) -> (u32, u32) {
        let device_handle = self.devices.insert(device);
        let queue_handle = self.queues.insert(queue);
        (device_handle, queue_handle)
    }

    fn remove_device(&mut self, handle: u32) -> bool {
        self.devices.remove(handle).is_some()
    }

    fn remove_queue(&mut self, handle: u32) -> bool {
        self.queues.remove(handle).is_some()
    }

    fn create_surface_from_element(&mut self, element: Element) -> Result<u32, String> {
        let canvas: HtmlCanvasElement = element
            .dyn_into()
            .map_err(|_| "Selected element is not an HTMLCanvasElement".to_string())?;

        let surface = self
            .instance
            .create_surface(SurfaceTarget::Canvas(canvas.clone()))
            .map_err(|err| format!("Failed to create surface: {err:?}"))?;

        let entry = SurfaceEntry::new(surface, canvas);
        Ok(self.surfaces.insert(entry))
    }

    fn create_surface_from_selector(&mut self, selector: &str) -> Result<u32, String> {
        let window = window().ok_or_else(|| "Window unavailable".to_string())?;
        let document = window
            .document()
            .ok_or_else(|| "Document unavailable".to_string())?;

        let element = document
            .query_selector(selector)
            .map_err(|err| format!("query_selector failed: {err:?}"))?
            .ok_or_else(|| format!("No element matches selector '{selector}'"))?;

        self.create_surface_from_element(element)
    }

    fn create_surface_from_canvas_id(&mut self, canvas_id: &str) -> Result<u32, String> {
        let window = window().ok_or_else(|| "Window unavailable".to_string())?;
        let document = window
            .document()
            .ok_or_else(|| "Document unavailable".to_string())?;

        let element = document
            .get_element_by_id(canvas_id)
            .ok_or_else(|| format!("No element matches id '{canvas_id}'"))?;

        self.create_surface_from_element(element)
    }

    fn resize_canvas(
        &mut self,
        surface_handle: u32,
        logical_width: f32,
        logical_height: f32,
        device_pixel_ratio: f32,
    ) -> Result<(u32, u32), (String, VelloWebGpuStatus)> {
        let Some(entry) = self.surfaces.get_mut(surface_handle) else {
            return Err((
                format!("Surface handle {surface_handle} not found"),
                VelloWebGpuStatus::InvalidHandle,
            ));
        };

        if !logical_width.is_finite()
            || !logical_height.is_finite()
            || !device_pixel_ratio.is_finite()
        {
            return Err((
                "Resize arguments must be finite".to_string(),
                VelloWebGpuStatus::InvalidArgument,
            ));
        }

        if logical_width < 0.0 || logical_height < 0.0 {
            return Err((
                "Canvas width and height must be non-negative".to_string(),
                VelloWebGpuStatus::InvalidArgument,
            ));
        }

        if device_pixel_ratio <= 0.0 {
            return Err((
                "Device pixel ratio must be greater than zero".to_string(),
                VelloWebGpuStatus::InvalidArgument,
            ));
        }

        let css_width = logical_width.max(0.0) as f64;
        let css_height = logical_height.max(0.0) as f64;
        let dpr = device_pixel_ratio as f64;
        let pixel_width = (css_width * dpr).round();
        let pixel_height = (css_height * dpr).round();

        if pixel_width <= 0.0 || pixel_height <= 0.0 {
            return Err((
                "Resulting canvas size must be positive".to_string(),
                VelloWebGpuStatus::InvalidArgument,
            ));
        }

        if pixel_width > u32::MAX as f64 || pixel_height > u32::MAX as f64 {
            return Err((
                "Resulting canvas size exceeds maximum dimensions".to_string(),
                VelloWebGpuStatus::InvalidArgument,
            ));
        }

        let width_px = (pixel_width as u32).max(1);
        let height_px = (pixel_height as u32).max(1);

        entry.canvas.set_width(width_px);
        entry.canvas.set_height(height_px);

        let style = entry.canvas.style();
        style
            .set_property("width", &format!("{css_width}px"))
            .map_err(|err| {
                (
                    format!("Failed to set canvas CSS width: {err:?}"),
                    VelloWebGpuStatus::Failed,
                )
            })?;
        style
            .set_property("height", &format!("{css_height}px"))
            .map_err(|err| {
                (
                    format!("Failed to set canvas CSS height: {err:?}"),
                    VelloWebGpuStatus::Failed,
                )
            })?;

        // Force the caller to reconfigure the surface after a resize.
        entry.config = None;

        Ok((width_px, height_px))
    }

    fn remove_surface(&mut self, handle: u32) -> bool {
        self.surfaces.remove(handle).is_some()
    }
}

struct AdapterRegistry {
    next_id: u32,
    adapters: HashMap<u32, wgpu::Adapter>,
}

impl AdapterRegistry {
    fn new() -> Self {
        Self {
            next_id: 1,
            adapters: HashMap::new(),
        }
    }

    fn insert(&mut self, adapter: wgpu::Adapter) -> u32 {
        let id = self.next_id;
        self.next_id = self.next_id.wrapping_add(1).max(1);
        self.adapters.insert(id, adapter);
        id
    }

    fn get(&self, id: u32) -> Option<wgpu::Adapter> {
        self.adapters.get(&id).cloned()
    }
}

struct DeviceRegistry {
    next_id: u32,
    devices: HashMap<u32, wgpu::Device>,
}

impl DeviceRegistry {
    fn new() -> Self {
        Self {
            next_id: 1,
            devices: HashMap::new(),
        }
    }

    fn insert(&mut self, device: wgpu::Device) -> u32 {
        let id = self.next_id;
        self.next_id = self.next_id.wrapping_add(1).max(1);
        self.devices.insert(id, device);
        id
    }

    fn remove(&mut self, id: u32) -> Option<wgpu::Device> {
        self.devices.remove(&id)
    }

    fn get(&self, id: u32) -> Option<wgpu::Device> {
        self.devices.get(&id).cloned()
    }
}

struct QueueRegistry {
    next_id: u32,
    queues: HashMap<u32, wgpu::Queue>,
}

impl QueueRegistry {
    fn new() -> Self {
        Self {
            next_id: 1,
            queues: HashMap::new(),
        }
    }

    fn insert(&mut self, queue: wgpu::Queue) -> u32 {
        let id = self.next_id;
        self.next_id = self.next_id.wrapping_add(1).max(1);
        self.queues.insert(id, queue);
        id
    }

    fn remove(&mut self, id: u32) -> Option<wgpu::Queue> {
        self.queues.remove(&id)
    }

    fn get(&self, id: u32) -> Option<wgpu::Queue> {
        self.queues.get(&id).cloned()
    }
}

struct SurfaceEntry {
    #[allow(dead_code)]
    surface: wgpu::Surface<'static>,
    #[allow(dead_code)]
    canvas: HtmlCanvasElement,
    config: Option<SurfaceConfiguration>,
}

impl SurfaceEntry {
    fn new(surface: wgpu::Surface<'_>, canvas: HtmlCanvasElement) -> Self {
        // SAFETY: the canvas is stored alongside the surface, guaranteeing it outlives the surface.
        let surface_static: wgpu::Surface<'static> = unsafe { mem::transmute(surface) };
        Self {
            surface: surface_static,
            canvas,
            config: None,
        }
    }
}

struct SurfaceRegistry {
    next_id: u32,
    surfaces: HashMap<u32, SurfaceEntry>,
}

impl SurfaceRegistry {
    fn new() -> Self {
        Self {
            next_id: 1,
            surfaces: HashMap::new(),
        }
    }

    fn insert(&mut self, entry: SurfaceEntry) -> u32 {
        let id = self.next_id;
        self.next_id = self.next_id.wrapping_add(1).max(1);
        self.surfaces.insert(id, entry);
        id
    }

    fn remove(&mut self, id: u32) -> Option<SurfaceEntry> {
        self.surfaces.remove(&id)
    }

    fn get_mut(&mut self, id: u32) -> Option<&mut SurfaceEntry> {
        self.surfaces.get_mut(&id)
    }
}

struct TextureEntry {
    texture: Option<wgpu::SurfaceTexture>,
}

impl TextureEntry {
    fn new(texture: wgpu::SurfaceTexture) -> Self {
        Self {
            texture: Some(texture),
        }
    }

    fn take_texture(&mut self) -> Option<wgpu::SurfaceTexture> {
        self.texture.take()
    }

    fn texture(&self) -> Option<&wgpu::SurfaceTexture> {
        self.texture.as_ref()
    }
}

struct TextureRegistry {
    next_id: u32,
    textures: HashMap<u32, TextureEntry>,
}

impl TextureRegistry {
    fn new() -> Self {
        Self {
            next_id: 1,
            textures: HashMap::new(),
        }
    }

    fn insert(&mut self, texture: wgpu::SurfaceTexture) -> u32 {
        let id = self.next_id;
        self.next_id = self.next_id.wrapping_add(1).max(1);
        self.textures.insert(id, TextureEntry::new(texture));
        id
    }

    fn remove(&mut self, id: u32) -> Option<TextureEntry> {
        self.textures.remove(&id)
    }

    fn get_mut(&mut self, id: u32) -> Option<&mut TextureEntry> {
        self.textures.get_mut(&id)
    }

    fn get(&self, id: u32) -> Option<&TextureEntry> {
        self.textures.get(&id)
    }
}

struct TextureViewRegistry {
    next_id: u32,
    views: HashMap<u32, wgpu::TextureView>,
}

impl TextureViewRegistry {
    fn new() -> Self {
        Self {
            next_id: 1,
            views: HashMap::new(),
        }
    }

    fn insert(&mut self, view: wgpu::TextureView) -> u32 {
        let id = self.next_id;
        self.next_id = self.next_id.wrapping_add(1).max(1);
        self.views.insert(id, view);
        id
    }

    fn remove(&mut self, id: u32) -> Option<wgpu::TextureView> {
        self.views.remove(&id)
    }

    fn get(&self, id: u32) -> Option<&wgpu::TextureView> {
        self.views.get(&id)
    }
}

struct RendererEntry {
    renderer: Renderer,
    device_handle: u32,
    queue_handle: u32,
}

struct RendererRegistry {
    next_id: u32,
    renderers: HashMap<u32, RendererEntry>,
}

impl RendererRegistry {
    fn new() -> Self {
        Self {
            next_id: 1,
            renderers: HashMap::new(),
        }
    }

    fn insert(&mut self, renderer: Renderer, device_handle: u32, queue_handle: u32) -> u32 {
        let id = self.next_id;
        self.next_id = self.next_id.wrapping_add(1).max(1);
        self.renderers.insert(
            id,
            RendererEntry {
                renderer,
                device_handle,
                queue_handle,
            },
        );
        id
    }

    fn remove(&mut self, id: u32) -> Option<RendererEntry> {
        self.renderers.remove(&id)
    }

    fn get(&self, id: u32) -> Option<&RendererEntry> {
        self.renderers.get(&id)
    }

    fn get_mut(&mut self, id: u32) -> Option<&mut RendererEntry> {
        self.renderers.get_mut(&id)
    }
}

#[derive(Clone)]
enum FutureOutput {
    Adapter {
        adapter_handle: Option<u32>,
    },
    Device {
        device_handle: Option<u32>,
        queue_handle: Option<u32>,
    },
}

#[derive(Copy, Clone, Debug, PartialEq, Eq)]
enum FutureState {
    Pending,
    Ready,
    Failed,
}

#[derive(Copy, Clone, Debug, PartialEq, Eq)]
enum FutureKind {
    Adapter,
    Device,
}

struct PromiseSharedState {
    state: FutureState,
    result: Option<FutureOutput>,
    error: Option<String>,
}

impl PromiseSharedState {
    fn new() -> Self {
        Self {
            state: FutureState::Pending,
            result: None,
            error: None,
        }
    }
}

struct FutureEntry {
    kind: FutureKind,
    shared: Rc<RefCell<PromiseSharedState>>,
    _on_resolve: Closure<dyn FnMut(JsValue)>,
    _on_reject: Closure<dyn FnMut(JsValue)>,
}

struct FutureSnapshot {
    state: FutureState,
    kind: FutureKind,
    result: Option<FutureOutput>,
    error: Option<String>,
}

struct FutureRegistry {
    next_id: u32,
    entries: HashMap<u32, FutureEntry>,
}

impl FutureRegistry {
    fn new() -> Self {
        Self {
            next_id: 1,
            entries: HashMap::new(),
        }
    }

    fn register_adapter_promise(&mut self, promise: Promise) -> u32 {
        let id = self.next_id;
        self.next_id = self.next_id.wrapping_add(1).max(1);

        let shared = Rc::new(RefCell::new(PromiseSharedState::new()));

        let shared_for_resolve = Rc::clone(&shared);
        let on_resolve = Closure::wrap(Box::new(move |value: JsValue| {
            let mut shared = shared_for_resolve.borrow_mut();
            shared.state = FutureState::Ready;
            let handle = value.as_f64().map(|v| v as u32).filter(|h| *h != 0);
            shared.result = Some(FutureOutput::Adapter {
                adapter_handle: handle,
            });
        }) as Box<dyn FnMut(JsValue)>);

        let shared_for_reject = Rc::clone(&shared);
        let on_reject = Closure::wrap(Box::new(move |value: JsValue| {
            let mut shared = shared_for_reject.borrow_mut();
            shared.state = FutureState::Failed;
            shared.error = Some(value.as_string().unwrap_or_else(|| format!("{value:?}")));
        }) as Box<dyn FnMut(JsValue)>);

        let _ = promise.then(&on_resolve);
        let _ = promise.catch(&on_reject);

        self.entries.insert(
            id,
            FutureEntry {
                kind: FutureKind::Adapter,
                shared,
                _on_resolve: on_resolve,
                _on_reject: on_reject,
            },
        );

        id
    }

    fn register_device_promise(&mut self, promise: Promise) -> u32 {
        let id = self.next_id;
        self.next_id = self.next_id.wrapping_add(1).max(1);

        let shared = Rc::new(RefCell::new(PromiseSharedState::new()));

        let shared_for_resolve = Rc::clone(&shared);
        let on_resolve = Closure::wrap(Box::new(move |value: JsValue| {
            let mut shared = shared_for_resolve.borrow_mut();
            shared.state = FutureState::Ready;
            let mut device_handle = None;
            let mut queue_handle = None;

            if let Some(array) = value.dyn_ref::<Array>() {
                device_handle = array.get(0).as_f64().map(|v| v as u32).filter(|h| *h != 0);
                queue_handle = array.get(1).as_f64().map(|v| v as u32).filter(|h| *h != 0);
            }

            shared.result = Some(FutureOutput::Device {
                device_handle,
                queue_handle,
            });
        }) as Box<dyn FnMut(JsValue)>);

        let shared_for_reject = Rc::clone(&shared);
        let on_reject = Closure::wrap(Box::new(move |value: JsValue| {
            let mut shared = shared_for_reject.borrow_mut();
            shared.state = FutureState::Failed;
            shared.error = Some(value.as_string().unwrap_or_else(|| format!("{value:?}")));
        }) as Box<dyn FnMut(JsValue)>);

        let _ = promise.then(&on_resolve);
        let _ = promise.catch(&on_reject);

        self.entries.insert(
            id,
            FutureEntry {
                kind: FutureKind::Device,
                shared,
                _on_resolve: on_resolve,
                _on_reject: on_reject,
            },
        );

        id
    }

    fn poll(&mut self, id: u32) -> Option<FutureSnapshot> {
        let remove_after_poll;
        let snapshot;
        if let Some(entry) = self.entries.get(&id) {
            let shared = entry.shared.borrow();
            snapshot = FutureSnapshot {
                state: shared.state,
                kind: entry.kind,
                result: shared.result.clone(),
                error: shared.error.clone(),
            };
            remove_after_poll = matches!(shared.state, FutureState::Ready | FutureState::Failed);
        } else {
            return None;
        }

        if remove_after_poll {
            self.entries.remove(&id);
        }

        Some(snapshot)
    }
}

fn clear_last_error() {
    LAST_ERROR.with(|slot| slot.borrow_mut().take());
}

fn set_last_error(message: impl Into<String>) {
    let message = message.into();
    emit_log(VelloWebGpuLogLevel::Error, &message);
    let c_string = make_c_string(&message);
    LAST_ERROR.with(|slot| *slot.borrow_mut() = Some(c_string));
}

fn make_c_string(message: &str) -> CString {
    CString::new(message)
        .or_else(|_| CString::new(message.replace('\0', "")))
        .unwrap_or_else(|_| CString::new("invalid message").expect("static string"))
}

fn emit_log(level: VelloWebGpuLogLevel, message: &str) {
    let callback = LOG_CALLBACK.with(|slot| slot.borrow().as_ref().copied());

    if let Some(callback) = callback {
        let c_string = make_c_string(message);
        unsafe {
            (callback.func)(level, c_string.as_ptr(), callback.user_data);
        }
    }
}

fn cstr_to_string(ptr: *const c_char) -> Option<String> {
    if ptr.is_null() {
        None
    } else {
        unsafe { CStr::from_ptr(ptr) }
            .to_str()
            .ok()
            .map(|s| s.to_string())
    }
}

fn map_power_preference(pref: VelloWebGpuPowerPreference) -> PowerPreference {
    match pref {
        VelloWebGpuPowerPreference::LowPower => PowerPreference::LowPower,
        VelloWebGpuPowerPreference::HighPerformance => PowerPreference::HighPerformance,
        _ => PowerPreference::None,
    }
}

fn select_present_mode(
    requested: VelloWebGpuPresentMode,
    capabilities: &SurfaceCapabilities,
) -> PresentMode {
    match requested {
        VelloWebGpuPresentMode::Fifo => PresentMode::Fifo,
        VelloWebGpuPresentMode::Immediate => PresentMode::Immediate,
        VelloWebGpuPresentMode::Auto => capabilities
            .present_modes
            .iter()
            .copied()
            .find(|mode| matches!(mode, PresentMode::Fifo))
            .or_else(|| capabilities.present_modes.first().copied())
            .unwrap_or(PresentMode::Fifo),
    }
}

fn select_alpha_mode(capabilities: &SurfaceCapabilities) -> CompositeAlphaMode {
    capabilities
        .alpha_modes
        .iter()
        .copied()
        .find(|mode| matches!(mode, CompositeAlphaMode::Auto | CompositeAlphaMode::Opaque))
        .or_else(|| capabilities.alpha_modes.first().copied())
        .unwrap_or(CompositeAlphaMode::Auto)
}

fn select_surface_format(capabilities: &SurfaceCapabilities) -> Option<wgpu::TextureFormat> {
    capabilities
        .formats
        .iter()
        .copied()
        .find(|format| {
            matches!(
                format,
                wgpu::TextureFormat::Bgra8Unorm
                    | wgpu::TextureFormat::Bgra8UnormSrgb
                    | wgpu::TextureFormat::Rgba8Unorm
                    | wgpu::TextureFormat::Rgba8UnormSrgb
            )
        })
        .or_else(|| capabilities.formats.first().copied())
}

fn resolve_surface_extent(
    entry: &SurfaceEntry,
    requested: &VelloWebGpuSurfaceConfiguration,
) -> (u32, u32) {
    let width = if requested.width == 0 {
        entry.canvas.width()
    } else {
        requested.width
    };
    let height = if requested.height == 0 {
        entry.canvas.height()
    } else {
        requested.height
    };
    (width.max(1), height.max(1))
}

fn surface_error_status(error: SurfaceError) -> (String, VelloWebGpuStatus) {
    match error {
        SurfaceError::Timeout => (
            "Surface acquisition timed out".to_string(),
            VelloWebGpuStatus::Failed,
        ),
        SurfaceError::Outdated => (
            "Surface configuration is outdated".to_string(),
            VelloWebGpuStatus::Failed,
        ),
        SurfaceError::Lost => (
            "Surface lost; configuration must be recreated".to_string(),
            VelloWebGpuStatus::Failed,
        ),
        SurfaceError::OutOfMemory => (
            "Surface acquisition failed due to insufficient memory".to_string(),
            VelloWebGpuStatus::Failed,
        ),
        SurfaceError::Other => ("Surface error".to_string(), VelloWebGpuStatus::Failed),
    }
}

fn limits_to_ffi(limits: &Limits) -> VelloWebGpuDeviceLimits {
    VelloWebGpuDeviceLimits {
        max_texture_dimension_1d: limits.max_texture_dimension_1d,
        max_texture_dimension_2d: limits.max_texture_dimension_2d,
        max_texture_dimension_3d: limits.max_texture_dimension_3d,
        max_texture_array_layers: limits.max_texture_array_layers,
        max_bind_groups: limits.max_bind_groups,
        max_bindings_per_bind_group: limits.max_bindings_per_bind_group,
        max_dynamic_uniform_buffers_per_pipeline_layout: limits
            .max_dynamic_uniform_buffers_per_pipeline_layout,
        max_dynamic_storage_buffers_per_pipeline_layout: limits
            .max_dynamic_storage_buffers_per_pipeline_layout,
        max_sampled_textures_per_shader_stage: limits.max_sampled_textures_per_shader_stage,
        max_samplers_per_shader_stage: limits.max_samplers_per_shader_stage,
        max_storage_buffers_per_shader_stage: limits.max_storage_buffers_per_shader_stage,
        max_storage_textures_per_shader_stage: limits.max_storage_textures_per_shader_stage,
        max_uniform_buffers_per_shader_stage: limits.max_uniform_buffers_per_shader_stage,
        max_uniform_buffer_binding_size: limits.max_uniform_buffer_binding_size as u64,
        max_storage_buffer_binding_size: limits.max_storage_buffer_binding_size as u64,
        max_buffer_size: limits.max_buffer_size,
        max_vertex_buffers: limits.max_vertex_buffers,
        max_vertex_attributes: limits.max_vertex_attributes,
        max_vertex_buffer_array_stride: limits.max_vertex_buffer_array_stride,
        max_inter_stage_shader_components: limits.max_inter_stage_shader_components,
        max_color_attachments: limits.max_color_attachments,
        max_color_attachment_bytes_per_sample: limits.max_color_attachment_bytes_per_sample,
        max_compute_workgroup_storage_size: limits.max_compute_workgroup_storage_size,
        max_compute_invocations_per_workgroup: limits.max_compute_invocations_per_workgroup,
        max_compute_workgroup_size_x: limits.max_compute_workgroup_size_x,
        max_compute_workgroup_size_y: limits.max_compute_workgroup_size_y,
        max_compute_workgroup_size_z: limits.max_compute_workgroup_size_z,
        max_compute_workgroups_per_dimension: limits.max_compute_workgroups_per_dimension,
        max_push_constant_size: limits.max_push_constant_size,
        max_non_sampler_bindings: limits.max_non_sampler_bindings,
    }
}

fn texture_format_to_ffi(format: wgpu::TextureFormat) -> Option<VelloWebGpuTextureFormat> {
    match format {
        wgpu::TextureFormat::Rgba8Unorm => Some(VelloWebGpuTextureFormat::Rgba8Unorm),
        wgpu::TextureFormat::Rgba8UnormSrgb => Some(VelloWebGpuTextureFormat::Rgba8UnormSrgb),
        wgpu::TextureFormat::Bgra8Unorm => Some(VelloWebGpuTextureFormat::Bgra8Unorm),
        wgpu::TextureFormat::Bgra8UnormSrgb => Some(VelloWebGpuTextureFormat::Bgra8UnormSrgb),
        _ => None,
    }
}

fn texture_format_from_ffi(format: VelloWebGpuTextureFormat) -> Option<wgpu::TextureFormat> {
    match format {
        VelloWebGpuTextureFormat::Rgba8Unorm => Some(wgpu::TextureFormat::Rgba8Unorm),
        VelloWebGpuTextureFormat::Rgba8UnormSrgb => Some(wgpu::TextureFormat::Rgba8UnormSrgb),
        VelloWebGpuTextureFormat::Bgra8Unorm => Some(wgpu::TextureFormat::Bgra8Unorm),
        VelloWebGpuTextureFormat::Bgra8UnormSrgb => Some(wgpu::TextureFormat::Bgra8UnormSrgb),
        VelloWebGpuTextureFormat::Undefined => None,
    }
}

fn color_from_ffi(color: VelloColor) -> Color {
    Color::new([color.r, color.g, color.b, color.a])
}

fn renderer_options_from_ffi(options: &VelloRendererOptions) -> RendererOptionsInternal {
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

    RendererOptionsInternal {
        use_cpu: options.use_cpu,
        antialiasing_support: support,
        num_init_threads: init_threads,
        pipeline_cache: None,
    }
}

fn render_params_from_ffi(params: &VelloRenderParams) -> RenderParamsInternal {
    RenderParamsInternal {
        base_color: color_from_ffi(params.base_color),
        width: params.width,
        height: params.height,
        antialiasing_method: params.antialiasing.into(),
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_initialize() -> VelloWebGpuStatus {
    Lazy::force(&INIT_LOGGER);
    clear_last_error();

    let status = WEBGPU_STATE.with(|cell| {
        let mut slot = cell.borrow_mut();
        if slot.is_some() {
            return VelloWebGpuStatus::AlreadyInitialized;
        }

        let instance_desc = InstanceDescriptor {
            backends: Backends::BROWSER_WEBGPU,
            ..Default::default()
        };

        let instance = wgpu::Instance::new(&instance_desc);
        *slot = Some(WebGpuState::new(instance));
        VelloWebGpuStatus::Success
    });

    if matches!(status, VelloWebGpuStatus::Success) {
        emit_log(VelloWebGpuLogLevel::Info, "WebGPU runtime initialized");
    }

    status
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_shutdown() {
    clear_last_error();
    WEBGPU_STATE.with(|cell| {
        cell.borrow_mut().take();
    });
    emit_log(VelloWebGpuLogLevel::Info, "WebGPU runtime shutdown");
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_last_error_message() -> *const c_char {
    LAST_ERROR.with(|slot| {
        slot.borrow()
            .as_ref()
            .map(|msg| msg.as_ptr())
            .unwrap_or(std::ptr::null())
    })
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_set_log_callback(
    callback: Option<LogCallbackFn>,
    user_data: *mut c_void,
) {
    let mut registered = false;
    LOG_CALLBACK.with(|slot| {
        if let Some(func) = callback {
            *slot.borrow_mut() = Some(LogCallback { func, user_data });
            registered = true;
        } else {
            slot.borrow_mut().take();
        }
    });

    if registered {
        emit_log(VelloWebGpuLogLevel::Info, "WebGPU log callback registered");
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_request_adapter_async(
    options: *const VelloWebGpuRequestAdapterOptions,
    out_future_id: *mut u32,
) -> VelloWebGpuStatus {
    if out_future_id.is_null() {
        set_last_error("Null pointer passed for out_future_id");
        return VelloWebGpuStatus::NullPointer;
    }

    clear_last_error();

    let opts = if options.is_null() {
        VelloWebGpuRequestAdapterOptions::default()
    } else {
        unsafe { *options }
    };

    let request_options = RequestAdapterOptions {
        power_preference: map_power_preference(opts.power_preference),
        force_fallback_adapter: opts.force_fallback_adapter != 0,
        compatible_surface: None,
    };

    let instance = match WEBGPU_STATE
        .with(|cell| cell.borrow().as_ref().map(|state| state.instance.clone()))
    {
        Some(instance) => instance,
        None => {
            set_last_error("WebGPU runtime not initialized");
            return VelloWebGpuStatus::NotInitialized;
        }
    };

    let promise = future_to_promise(async move {
        match instance.request_adapter(&request_options).await {
            Ok(adapter) => {
                let handle =
                    ADAPTER_REGISTRY.with(|registry| registry.borrow_mut().insert(adapter));
                Ok(JsValue::from_f64(handle as f64))
            }
            Err(RequestAdapterError::NotFound { .. }) => Ok(JsValue::UNDEFINED),
            Err(err) => Err(JsValue::from_str(&err.to_string())),
        }
    });

    let future_id = WEBGPU_STATE.with(|cell| {
        let mut slot = cell.borrow_mut();
        if let Some(state) = slot.as_mut() {
            state.futures.register_adapter_promise(promise)
        } else {
            0
        }
    });

    if future_id == 0 {
        set_last_error("WebGPU runtime not initialized");
        VelloWebGpuStatus::NotInitialized
    } else {
        unsafe { *out_future_id = future_id };
        VelloWebGpuStatus::Success
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_request_device_async(
    adapter_handle: u32,
    options: *const VelloWebGpuRequestDeviceOptions,
    out_future_id: *mut u32,
) -> VelloWebGpuStatus {
    if out_future_id.is_null() {
        set_last_error("Null pointer passed for out_future_id");
        return VelloWebGpuStatus::NullPointer;
    }

    clear_last_error();

    let adapter = match ADAPTER_REGISTRY.with(|registry| registry.borrow().get(adapter_handle)) {
        Some(adapter) => adapter,
        None => {
            set_last_error(format!("Adapter handle {adapter_handle} not found"));
            return VelloWebGpuStatus::InvalidHandle;
        }
    };

    let opts = if options.is_null() {
        VelloWebGpuRequestDeviceOptions::default()
    } else {
        unsafe { *options }
    };
    let _required_features_mask = opts.required_features_mask;
    let required_features = Features::empty();
    let _label = cstr_to_string(opts.label);

    let required_limits = if opts.require_downlevel_defaults != 0 {
        Limits::downlevel_defaults()
    } else if opts.require_default_limits != 0 {
        Limits::default()
    } else {
        Limits::downlevel_webgl2_defaults()
    };

    let descriptor = DeviceDescriptor {
        label: None,
        required_features,
        required_limits,
        ..Default::default()
    };

    let promise = future_to_promise(async move {
        match adapter.request_device(&descriptor).await {
            Ok((device, queue)) => {
                let (device_handle, queue_handle) = WEBGPU_STATE.with(|cell| {
                    let mut slot = cell.borrow_mut();
                    if let Some(state) = slot.as_mut() {
                        state.insert_device(device, queue)
                    } else {
                        (0, 0)
                    }
                });

                let result = Array::new();
                if device_handle != 0 {
                    result.push(&JsValue::from_f64(device_handle as f64));
                } else {
                    result.push(&JsValue::UNDEFINED);
                }
                if queue_handle != 0 {
                    result.push(&JsValue::from_f64(queue_handle as f64));
                } else {
                    result.push(&JsValue::UNDEFINED);
                }

                Ok(result.into())
            }
            Err(err) => Err(JsValue::from_str(&err.to_string())),
        }
    });

    let future_id = WEBGPU_STATE.with(|cell| {
        let mut slot = cell.borrow_mut();
        if let Some(state) = slot.as_mut() {
            state.futures.register_device_promise(promise)
        } else {
            0
        }
    });

    if future_id == 0 {
        set_last_error("WebGPU runtime not initialized");
        VelloWebGpuStatus::NotInitialized
    } else {
        unsafe { *out_future_id = future_id };
        VelloWebGpuStatus::Success
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_future_poll(
    future_id: u32,
    out_result: *mut VelloWebGpuFuturePollResult,
) -> VelloWebGpuStatus {
    if out_result.is_null() {
        set_last_error("Null pointer passed for out_result");
        return VelloWebGpuStatus::NullPointer;
    }

    clear_last_error();

    let snapshot = WEBGPU_STATE.with(|cell| {
        let mut slot = cell.borrow_mut();
        slot.as_mut()
            .and_then(|state| state.futures.poll(future_id))
    });

    let snapshot = match snapshot {
        Some(snapshot) => snapshot,
        None => {
            set_last_error(format!("Future handle {future_id} not found"));
            return VelloWebGpuStatus::InvalidArgument;
        }
    };

    let mut poll_result = VelloWebGpuFuturePollResult {
        state: snapshot.state.into(),
        kind: snapshot.kind.into(),
        adapter_handle: 0,
        device_handle: 0,
        queue_handle: 0,
    };

    match snapshot.state {
        FutureState::Pending => {
            unsafe { *out_result = poll_result };
            VelloWebGpuStatus::Success
        }
        FutureState::Ready => {
            if let Some(result) = snapshot.result {
                match result {
                    FutureOutput::Adapter { adapter_handle } => {
                        poll_result.adapter_handle = adapter_handle.unwrap_or(0);
                    }
                    FutureOutput::Device {
                        device_handle,
                        queue_handle,
                    } => {
                        poll_result.device_handle = device_handle.unwrap_or(0);
                        poll_result.queue_handle = queue_handle.unwrap_or(0);
                    }
                }
            }

            unsafe { *out_result = poll_result };
            VelloWebGpuStatus::Success
        }
        FutureState::Failed => {
            if let Some(err) = snapshot.error {
                set_last_error(err);
            } else {
                set_last_error("WebGPU future failed without an error message");
            }
            unsafe { *out_result = poll_result };
            VelloWebGpuStatus::Failed
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_device_destroy(handle: u32) -> VelloWebGpuStatus {
    clear_last_error();
    let removed = WEBGPU_STATE.with(|cell| {
        let mut slot = cell.borrow_mut();
        slot.as_mut()
            .map_or(false, |state| state.remove_device(handle))
    });

    if removed {
        VelloWebGpuStatus::Success
    } else {
        set_last_error(format!("Device handle {handle} not found"));
        VelloWebGpuStatus::InvalidHandle
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_device_get_limits(
    device_handle: u32,
    out_limits: *mut VelloWebGpuDeviceLimits,
) -> VelloWebGpuStatus {
    if out_limits.is_null() {
        set_last_error("Null pointer passed for out_limits");
        return VelloWebGpuStatus::NullPointer;
    }

    clear_last_error();

    let limits = WEBGPU_STATE.with(|cell| {
        let slot = cell.borrow();
        let Some(state) = slot.as_ref() else {
            return Err((
                "WebGPU runtime not initialized".to_string(),
                VelloWebGpuStatus::NotInitialized,
            ));
        };

        let Some(device) = state.devices.get(device_handle) else {
            return Err((
                format!("Device handle {device_handle} not found"),
                VelloWebGpuStatus::InvalidHandle,
            ));
        };

        Ok(device.limits())
    });

    match limits {
        Ok(limits) => {
            unsafe {
                *out_limits = limits_to_ffi(&limits);
            }
            VelloWebGpuStatus::Success
        }
        Err((message, status)) => {
            set_last_error(message);
            status
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_queue_destroy(handle: u32) -> VelloWebGpuStatus {
    clear_last_error();
    let removed = WEBGPU_STATE.with(|cell| {
        let mut slot = cell.borrow_mut();
        slot.as_mut()
            .map_or(false, |state| state.remove_queue(handle))
    });

    if removed {
        VelloWebGpuStatus::Success
    } else {
        set_last_error(format!("Queue handle {handle} not found"));
        VelloWebGpuStatus::InvalidHandle
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_renderer_create(
    device_handle: u32,
    queue_handle: u32,
    options: *const VelloRendererOptions,
    out_renderer_handle: *mut u32,
) -> VelloWebGpuStatus {
    if out_renderer_handle.is_null() {
        set_last_error("Null pointer passed for out_renderer_handle");
        return VelloWebGpuStatus::NullPointer;
    }

    clear_last_error();

    let options_value = if options.is_null() {
        VelloRendererOptions {
            use_cpu: false,
            support_area: true,
            support_msaa8: true,
            support_msaa16: true,
            init_threads: 0,
            pipeline_cache: core::ptr::null_mut(),
        }
    } else {
        unsafe { *options }
    };

    let result: Result<u32, (String, VelloWebGpuStatus)> = WEBGPU_STATE.with(|cell| {
        let mut slot = cell.borrow_mut();
        let Some(state) = slot.as_mut() else {
            return Err((
                "WebGPU runtime not initialized".to_string(),
                VelloWebGpuStatus::NotInitialized,
            ));
        };

        let device = state.devices.get(device_handle);
        let Some(device) = device else {
            return Err((
                format!("Device handle {device_handle} not found"),
                VelloWebGpuStatus::InvalidHandle,
            ));
        };

        let queue = state.queues.get(queue_handle);
        let Some(queue) = queue else {
            return Err((
                format!("Queue handle {queue_handle} not found"),
                VelloWebGpuStatus::InvalidHandle,
            ));
        };

        let renderer_options = renderer_options_from_ffi(&options_value);
        let renderer = match Renderer::new(&device, renderer_options) {
            Ok(renderer) => renderer,
            Err(err) => {
                return Err((
                    format!("Failed to create renderer: {err}"),
                    VelloWebGpuStatus::Failed,
                ));
            }
        };

        let handle = state
            .renderers
            .insert(renderer, device_handle, queue_handle);
        Ok(handle)
    });

    match result {
        Ok(handle) => {
            unsafe { *out_renderer_handle = handle };
            emit_log(
                VelloWebGpuLogLevel::Info,
                &format!(
                    "Created renderer {handle} for device {device_handle}, queue {queue_handle}"
                ),
            );
            VelloWebGpuStatus::Success
        }
        Err((message, status)) => {
            set_last_error(message);
            status
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_renderer_destroy(renderer_handle: u32) -> VelloWebGpuStatus {
    clear_last_error();
    let removed = WEBGPU_STATE.with(|cell| {
        let mut slot = cell.borrow_mut();
        slot.as_mut().map_or(false, |state| {
            state.renderers.remove(renderer_handle).is_some()
        })
    });

    if removed {
        emit_log(
            VelloWebGpuLogLevel::Debug,
            &format!("Destroyed renderer {renderer_handle}"),
        );
        VelloWebGpuStatus::Success
    } else {
        set_last_error(format!("Renderer handle {renderer_handle} not found"));
        VelloWebGpuStatus::InvalidHandle
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_renderer_render_surface(
    renderer_handle: u32,
    scene: *const VelloSceneHandle,
    texture_view_handle: u32,
    params: *const VelloRenderParams,
    surface_format: VelloWebGpuTextureFormat,
) -> VelloWebGpuStatus {
    if scene.is_null() || params.is_null() {
        set_last_error("Null pointer passed for scene or params");
        return VelloWebGpuStatus::NullPointer;
    }

    clear_last_error();

    let result: Result<(), (String, VelloWebGpuStatus)> = WEBGPU_STATE.with(|cell| {
        let mut slot = cell.borrow_mut();
        let Some(state) = slot.as_mut() else {
            return Err((
                "WebGPU runtime not initialized".to_string(),
                VelloWebGpuStatus::NotInitialized,
            ));
        };

        let Some(entry) = state.renderers.get_mut(renderer_handle) else {
            return Err((
                format!("Renderer handle {renderer_handle} not found"),
                VelloWebGpuStatus::InvalidHandle,
            ));
        };

        let device = state.devices.get(entry.device_handle);
        let Some(device) = device else {
            return Err((
                format!("Device handle {} not found", entry.device_handle),
                VelloWebGpuStatus::InvalidHandle,
            ));
        };

        let queue = state.queues.get(entry.queue_handle);
        let Some(queue) = queue else {
            return Err((
                format!("Queue handle {} not found", entry.queue_handle),
                VelloWebGpuStatus::InvalidHandle,
            ));
        };

        let view = state.texture_views.get(texture_view_handle);
        let Some(view) = view else {
            return Err((
                format!("Texture view handle {texture_view_handle} not found"),
                VelloWebGpuStatus::InvalidHandle,
            ));
        };

        let Some(_format) = texture_format_from_ffi(surface_format) else {
            return Err((
                "Surface texture format is undefined".to_string(),
                VelloWebGpuStatus::InvalidArgument,
            ));
        };

        let scene_ref = unsafe { &*scene };
        let params_ref = unsafe { &*params };
        let render_params = render_params_from_ffi(params_ref);

        if let Err(err) = entry.renderer.render_to_texture(
            &device,
            &queue,
            &scene_ref.inner,
            view,
            &render_params,
        ) {
            return Err((format!("Render failed: {err}"), VelloWebGpuStatus::Failed));
        }

        Ok(())
    });

    match result {
        Ok(()) => {
            emit_log(
                VelloWebGpuLogLevel::Debug,
                &format!(
                    "Rendered scene with renderer {renderer_handle} into texture view {texture_view_handle}"
                ),
            );
            VelloWebGpuStatus::Success
        }
        Err((message, status)) => {
            set_last_error(message);
            status
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_surface_from_canvas_selector(
    selector: *const c_char,
    out_surface_handle: *mut u32,
) -> VelloWebGpuStatus {
    if selector.is_null() || out_surface_handle.is_null() {
        set_last_error("Null pointer passed to vello_webgpu_surface_from_canvas_selector");
        return VelloWebGpuStatus::NullPointer;
    }

    clear_last_error();

    let selector_str = match cstr_to_string(selector) {
        Some(value) => value,
        None => {
            set_last_error("Invalid selector string");
            return VelloWebGpuStatus::InvalidArgument;
        }
    };

    let result = WEBGPU_STATE.with(|cell| {
        let mut slot = cell.borrow_mut();
        if let Some(state) = slot.as_mut() {
            match state.create_surface_from_selector(&selector_str) {
                Ok(handle) => Some(Ok(handle)),
                Err(err) => Some(Err(err)),
            }
        } else {
            None
        }
    });

    match result {
        Some(Ok(handle)) => {
            unsafe { *out_surface_handle = handle };
            VelloWebGpuStatus::Success
        }
        Some(Err(err)) => {
            set_last_error(err);
            VelloWebGpuStatus::Failed
        }
        None => {
            set_last_error("WebGPU runtime not initialized");
            VelloWebGpuStatus::NotInitialized
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_surface_from_canvas_id(
    canvas_id: *const c_char,
    out_surface_handle: *mut u32,
) -> VelloWebGpuStatus {
    if canvas_id.is_null() || out_surface_handle.is_null() {
        set_last_error("Null pointer passed to vello_webgpu_surface_from_canvas_id");
        return VelloWebGpuStatus::NullPointer;
    }

    clear_last_error();

    let canvas_id = match cstr_to_string(canvas_id) {
        Some(value) => value,
        None => {
            set_last_error("Invalid canvas id string");
            return VelloWebGpuStatus::InvalidArgument;
        }
    };

    let result = WEBGPU_STATE.with(|cell| {
        let mut slot = cell.borrow_mut();
        if let Some(state) = slot.as_mut() {
            match state.create_surface_from_canvas_id(&canvas_id) {
                Ok(handle) => Some(Ok(handle)),
                Err(err) => Some(Err(err)),
            }
        } else {
            None
        }
    });

    match result {
        Some(Ok(handle)) => {
            unsafe { *out_surface_handle = handle };
            VelloWebGpuStatus::Success
        }
        Some(Err(err)) => {
            set_last_error(err);
            VelloWebGpuStatus::Failed
        }
        None => {
            set_last_error("WebGPU runtime not initialized");
            VelloWebGpuStatus::NotInitialized
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_surface_resize_canvas(
    surface_handle: u32,
    logical_width: f32,
    logical_height: f32,
    device_pixel_ratio: f32,
) -> VelloWebGpuStatus {
    clear_last_error();

    let result = WEBGPU_STATE.with(|cell| {
        let mut slot = cell.borrow_mut();
        if let Some(state) = slot.as_mut() {
            state.resize_canvas(
                surface_handle,
                logical_width,
                logical_height,
                device_pixel_ratio,
            )
        } else {
            Err((
                "WebGPU runtime not initialized".to_string(),
                VelloWebGpuStatus::NotInitialized,
            ))
        }
    });

    match result {
        Ok((pixel_width, pixel_height)) => {
            emit_log(
                VelloWebGpuLogLevel::Debug,
                &format!(
                    "Resized surface {surface_handle} canvas to {pixel_width}x{pixel_height} (logical {logical_width}x{logical_height} @ {device_pixel_ratio} DPR)"
                ),
            );
            VelloWebGpuStatus::Success
        }
        Err((message, status)) => {
            set_last_error(message);
            status
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_surface_destroy(handle: u32) -> VelloWebGpuStatus {
    clear_last_error();
    let removed = WEBGPU_STATE.with(|cell| {
        let mut slot = cell.borrow_mut();
        slot.as_mut()
            .map_or(false, |state| state.remove_surface(handle))
    });

    if removed {
        VelloWebGpuStatus::Success
    } else {
        set_last_error(format!("Surface handle {handle} not found"));
        VelloWebGpuStatus::InvalidHandle
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_surface_configure(
    surface_handle: u32,
    adapter_handle: u32,
    device_handle: u32,
    configuration: *const VelloWebGpuSurfaceConfiguration,
) -> VelloWebGpuStatus {
    clear_last_error();

    let config = if configuration.is_null() {
        VelloWebGpuSurfaceConfiguration::default()
    } else {
        unsafe { *configuration }
    };

    let result: Result<(u32, u32), (String, VelloWebGpuStatus)> = WEBGPU_STATE.with(|cell| {
        let mut slot = cell.borrow_mut();
        let Some(state) = slot.as_mut() else {
            return Err((
                "WebGPU runtime not initialized".to_string(),
                VelloWebGpuStatus::NotInitialized,
            ));
        };

        let Some(surface_entry) = state.surfaces.get_mut(surface_handle) else {
            return Err((
                format!("Surface handle {surface_handle} not found"),
                VelloWebGpuStatus::InvalidHandle,
            ));
        };

        let adapter = ADAPTER_REGISTRY.with(|registry| registry.borrow().get(adapter_handle));
        let Some(adapter) = adapter else {
            return Err((
                format!("Adapter handle {adapter_handle} not found"),
                VelloWebGpuStatus::InvalidHandle,
            ));
        };

        let Some(device) = state.devices.get(device_handle) else {
            return Err((
                format!("Device handle {device_handle} not found"),
                VelloWebGpuStatus::InvalidHandle,
            ));
        };

        let capabilities = surface_entry.surface.get_capabilities(&adapter);
        let Some(format) = select_surface_format(&capabilities) else {
            return Err((
                "Surface does not report any supported texture formats".to_string(),
                VelloWebGpuStatus::Failed,
            ));
        };

        let (width, height) = resolve_surface_extent(surface_entry, &config);
        let present_mode = select_present_mode(config.present_mode, &capabilities);
        let alpha_mode = select_alpha_mode(&capabilities);

        let surface_config = SurfaceConfiguration {
            usage: TextureUsages::RENDER_ATTACHMENT,
            format,
            width,
            height,
            present_mode,
            alpha_mode,
            view_formats: Vec::new(),
            desired_maximum_frame_latency: 1,
        };

        surface_entry.surface.configure(&device, &surface_config);
        surface_entry.config = Some(surface_config);

        Ok((width, height))
    });

    match result {
        Ok((width, height)) => {
            emit_log(
                VelloWebGpuLogLevel::Info,
                &format!("Configured surface {surface_handle} ({width}x{height})"),
            );
            VelloWebGpuStatus::Success
        }
        Err((message, status)) => {
            set_last_error(message);
            status
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_surface_get_current_texture_format(
    surface_handle: u32,
    out_format: *mut VelloWebGpuTextureFormat,
) -> VelloWebGpuStatus {
    if out_format.is_null() {
        set_last_error("Null pointer passed for out_format");
        return VelloWebGpuStatus::NullPointer;
    }

    clear_last_error();

    let format = WEBGPU_STATE.with(|cell| {
        let mut slot = cell.borrow_mut();
        let Some(state) = slot.as_mut() else {
            return Err((
                "WebGPU runtime not initialized".to_string(),
                VelloWebGpuStatus::NotInitialized,
            ));
        };

        let Some(surface_entry) = state.surfaces.get_mut(surface_handle) else {
            return Err((
                format!("Surface handle {surface_handle} not found"),
                VelloWebGpuStatus::InvalidHandle,
            ));
        };

        let Some(config) = surface_entry.config.as_ref() else {
            return Err((
                "Surface is not configured".to_string(),
                VelloWebGpuStatus::Failed,
            ));
        };

        match texture_format_to_ffi(config.format) {
            Some(format) => Ok(format),
            None => Err((
                format!(
                    "Surface configured with unsupported texture format {:?}",
                    config.format
                ),
                VelloWebGpuStatus::Failed,
            )),
        }
    });

    match format {
        Ok(format) => {
            unsafe {
                *out_format = format;
            }
            VelloWebGpuStatus::Success
        }
        Err((message, status)) => {
            set_last_error(message);
            status
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_surface_acquire_next_texture(
    surface_handle: u32,
    out_texture_handle: *mut u32,
) -> VelloWebGpuStatus {
    if out_texture_handle.is_null() {
        set_last_error("Null pointer passed for out_texture_handle");
        return VelloWebGpuStatus::NullPointer;
    }

    clear_last_error();

    let result: Result<u32, (String, VelloWebGpuStatus)> = WEBGPU_STATE.with(|cell| {
        let mut slot = cell.borrow_mut();
        let Some(state) = slot.as_mut() else {
            return Err((
                "WebGPU runtime not initialized".to_string(),
                VelloWebGpuStatus::NotInitialized,
            ));
        };

        let Some(surface_entry) = state.surfaces.get_mut(surface_handle) else {
            return Err((
                format!("Surface handle {surface_handle} not found"),
                VelloWebGpuStatus::InvalidHandle,
            ));
        };

        if surface_entry.config.is_none() {
            return Err((
                "Surface must be configured before acquiring frames".to_string(),
                VelloWebGpuStatus::Failed,
            ));
        }

        match surface_entry.surface.get_current_texture() {
            Ok(texture) => {
                let handle = state.textures.insert(texture);
                Ok(handle)
            }
            Err(err) => {
                let (message, status) = surface_error_status(err);
                Err((message, status))
            }
        }
    });

    match result {
        Ok(handle) => {
            unsafe {
                *out_texture_handle = handle;
            }
            emit_log(
                VelloWebGpuLogLevel::Debug,
                &format!("Acquired surface texture {handle} for surface {surface_handle}"),
            );
            VelloWebGpuStatus::Success
        }
        Err((message, status)) => {
            set_last_error(message);
            status
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_surface_texture_create_view(
    texture_handle: u32,
    out_view_handle: *mut u32,
) -> VelloWebGpuStatus {
    if out_view_handle.is_null() {
        set_last_error("Null pointer passed for out_view_handle");
        return VelloWebGpuStatus::NullPointer;
    }

    clear_last_error();

    let result: Result<u32, (String, VelloWebGpuStatus)> = WEBGPU_STATE.with(|cell| {
        let mut slot = cell.borrow_mut();
        let Some(state) = slot.as_mut() else {
            return Err((
                "WebGPU runtime not initialized".to_string(),
                VelloWebGpuStatus::NotInitialized,
            ));
        };

        let entry = state.textures.get(texture_handle);
        let Some(entry) = entry else {
            return Err((
                format!("Texture handle {texture_handle} not found"),
                VelloWebGpuStatus::InvalidHandle,
            ));
        };

        let Some(surface_texture) = entry.texture() else {
            return Err((
                "Surface texture is no longer available".to_string(),
                VelloWebGpuStatus::InvalidHandle,
            ));
        };

        let view = surface_texture
            .texture
            .create_view(&TextureViewDescriptor::default());
        let handle = state.texture_views.insert(view);
        Ok(handle)
    });

    match result {
        Ok(handle) => {
            unsafe {
                *out_view_handle = handle;
            }
            emit_log(
                VelloWebGpuLogLevel::Debug,
                &format!("Created texture view {handle} for surface texture {texture_handle}"),
            );
            VelloWebGpuStatus::Success
        }
        Err((message, status)) => {
            set_last_error(message);
            status
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_surface_present(
    _surface_handle: u32,
    texture_handle: u32,
) -> VelloWebGpuStatus {
    clear_last_error();

    let result: Result<(), (String, VelloWebGpuStatus)> = WEBGPU_STATE.with(|cell| {
        let mut slot = cell.borrow_mut();
        let Some(state) = slot.as_mut() else {
            return Err((
                "WebGPU runtime not initialized".to_string(),
                VelloWebGpuStatus::NotInitialized,
            ));
        };

        let entry = state.textures.get_mut(texture_handle);
        let Some(entry) = entry else {
            return Err((
                format!("Texture handle {texture_handle} not found"),
                VelloWebGpuStatus::InvalidHandle,
            ));
        };

        let Some(texture) = entry.take_texture() else {
            return Err((
                "Surface texture has already been presented".to_string(),
                VelloWebGpuStatus::InvalidHandle,
            ));
        };

        texture.present();
        state.textures.remove(texture_handle);
        Ok(())
    });

    match result {
        Ok(()) => {
            emit_log(
                VelloWebGpuLogLevel::Debug,
                &format!("Presented surface texture {texture_handle}"),
            );
            VelloWebGpuStatus::Success
        }
        Err((message, status)) => {
            set_last_error(message);
            status
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_surface_texture_destroy(
    texture_handle: u32,
) -> VelloWebGpuStatus {
    clear_last_error();
    let removed = WEBGPU_STATE.with(|cell| {
        let mut slot = cell.borrow_mut();
        slot.as_mut().map_or(false, |state| {
            state.textures.remove(texture_handle).is_some()
        })
    });

    if removed {
        emit_log(
            VelloWebGpuLogLevel::Debug,
            &format!("Destroyed surface texture {texture_handle}"),
        );
        VelloWebGpuStatus::Success
    } else {
        set_last_error(format!("Texture handle {texture_handle} not found"));
        VelloWebGpuStatus::InvalidHandle
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_webgpu_texture_view_destroy(
    texture_view_handle: u32,
) -> VelloWebGpuStatus {
    clear_last_error();
    let removed = WEBGPU_STATE.with(|cell| {
        let mut slot = cell.borrow_mut();
        slot.as_mut().map_or(false, |state| {
            state.texture_views.remove(texture_view_handle).is_some()
        })
    });

    if removed {
        emit_log(
            VelloWebGpuLogLevel::Debug,
            &format!("Destroyed texture view {texture_view_handle}"),
        );
        VelloWebGpuStatus::Success
    } else {
        set_last_error(format!(
            "Texture view handle {texture_view_handle} not found"
        ));
        VelloWebGpuStatus::InvalidHandle
    }
}

impl From<FutureState> for VelloWebGpuFutureState {
    fn from(value: FutureState) -> Self {
        match value {
            FutureState::Pending => VelloWebGpuFutureState::Pending,
            FutureState::Ready => VelloWebGpuFutureState::Ready,
            FutureState::Failed => VelloWebGpuFutureState::Failed,
        }
    }
}

impl From<FutureKind> for VelloWebGpuFutureKind {
    fn from(value: FutureKind) -> Self {
        match value {
            FutureKind::Adapter => VelloWebGpuFutureKind::Adapter,
            FutureKind::Device => VelloWebGpuFutureKind::Device,
        }
    }
}

#[cfg(all(test, target_arch = "wasm32"))]
mod tests {
    use super::*;
    use gloo_timers::future::TimeoutFuture;
    use js_sys::{Array, Promise};
    use wasm_bindgen::JsValue;
    use wasm_bindgen_test::*;

    wasm_bindgen_test_configure!(run_in_browser);

    #[wasm_bindgen_test(async)]
    async fn future_registry_transitions_to_ready_state() {
        let mut registry = FutureRegistry::new();

        let payload = Array::new();
        payload.push(&JsValue::from(7));
        payload.push(&JsValue::from(11));
        let promise = Promise::resolve(&payload.into());

        let id = registry.register_device_promise(promise);
        assert_ne!(id, 0, "future id should be non-zero");

        let snapshot = registry.poll(id).expect("snapshot must exist");
        assert_eq!(snapshot.state, FutureState::Pending);

        TimeoutFuture::new(0).await;

        let snapshot = registry.poll(id).expect("snapshot must exist after resolve");
        assert_eq!(snapshot.state, FutureState::Ready);

        match snapshot.result {
            Some(FutureOutput::Device { device_handle, queue_handle }) => {
                assert_eq!(device_handle, Some(7));
                assert_eq!(queue_handle, Some(11));
            }
            other => panic!("unexpected result: {other:?}"),
        }
    }
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
