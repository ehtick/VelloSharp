#![allow(dead_code)]

use core::ffi::{c_char, c_void};

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWebGpuStatus {
    Success = 0,
    NullPointer = 1,
    Unsupported = 2,
    Panic = 3,
    AlreadyInitialized = 4,
    NotInitialized = 5,
    InvalidArgument = 6,
    Failed = 7,
    InvalidHandle = 8,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWebGpuPowerPreference {
    Auto = 0,
    LowPower = 1,
    HighPerformance = 2,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWebGpuRequestAdapterOptions {
    pub power_preference: VelloWebGpuPowerPreference,
    pub force_fallback_adapter: u8,
}

impl Default for VelloWebGpuRequestAdapterOptions {
    fn default() -> Self {
        Self {
            power_preference: VelloWebGpuPowerPreference::Auto,
            force_fallback_adapter: 0,
        }
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWebGpuRequestDeviceOptions {
    pub required_features_mask: u64,
    pub require_downlevel_defaults: u8,
    pub require_default_limits: u8,
    pub label: *const c_char,
}

impl Default for VelloWebGpuRequestDeviceOptions {
    fn default() -> Self {
        Self {
            required_features_mask: 0,
            require_downlevel_defaults: 1,
            require_default_limits: 0,
            label: core::ptr::null(),
        }
    }
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWebGpuSurfaceConfiguration {
    pub width: u32,
    pub height: u32,
    pub present_mode: VelloWebGpuPresentMode,
}

impl Default for VelloWebGpuSurfaceConfiguration {
    fn default() -> Self {
        Self {
            width: 0,
            height: 0,
            present_mode: VelloWebGpuPresentMode::Auto,
        }
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWebGpuFutureState {
    Pending = 0,
    Ready = 1,
    Failed = 2,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWebGpuFutureKind {
    Adapter = 0,
    Device = 1,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloWebGpuFuturePollResult {
    pub state: VelloWebGpuFutureState,
    pub kind: VelloWebGpuFutureKind,
    pub adapter_handle: u32,
    pub device_handle: u32,
    pub queue_handle: u32,
}

impl Default for VelloWebGpuFuturePollResult {
    fn default() -> Self {
        Self {
            state: VelloWebGpuFutureState::Pending,
            kind: VelloWebGpuFutureKind::Adapter,
            adapter_handle: 0,
            device_handle: 0,
            queue_handle: 0,
        }
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWebGpuPresentMode {
    Auto = 0,
    Fifo = 1,
    Immediate = 2,
}

#[repr(C)]
#[derive(Debug, Copy, Clone, Default)]
pub struct VelloWebGpuDeviceLimits {
    pub max_texture_dimension_1d: u32,
    pub max_texture_dimension_2d: u32,
    pub max_texture_dimension_3d: u32,
    pub max_texture_array_layers: u32,
    pub max_bind_groups: u32,
    pub max_bindings_per_bind_group: u32,
    pub max_dynamic_uniform_buffers_per_pipeline_layout: u32,
    pub max_dynamic_storage_buffers_per_pipeline_layout: u32,
    pub max_sampled_textures_per_shader_stage: u32,
    pub max_samplers_per_shader_stage: u32,
    pub max_storage_buffers_per_shader_stage: u32,
    pub max_storage_textures_per_shader_stage: u32,
    pub max_uniform_buffers_per_shader_stage: u32,
    pub max_uniform_buffer_binding_size: u64,
    pub max_storage_buffer_binding_size: u64,
    pub max_buffer_size: u64,
    pub max_vertex_buffers: u32,
    pub max_vertex_attributes: u32,
    pub max_vertex_buffer_array_stride: u32,
    pub max_inter_stage_shader_components: u32,
    pub max_color_attachments: u32,
    pub max_color_attachment_bytes_per_sample: u32,
    pub max_compute_workgroup_storage_size: u32,
    pub max_compute_invocations_per_workgroup: u32,
    pub max_compute_workgroup_size_x: u32,
    pub max_compute_workgroup_size_y: u32,
    pub max_compute_workgroup_size_z: u32,
    pub max_compute_workgroups_per_dimension: u32,
    pub max_push_constant_size: u32,
    pub max_non_sampler_bindings: u32,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWebGpuTextureFormat {
    Undefined = 0,
    Rgba8Unorm = 1,
    Rgba8UnormSrgb = 2,
    Bgra8Unorm = 3,
    Bgra8UnormSrgb = 4,
}

impl Default for VelloWebGpuTextureFormat {
    fn default() -> Self {
        VelloWebGpuTextureFormat::Undefined
    }
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloWebGpuLogLevel {
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warn = 3,
    Error = 4,
}

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloColor {
    pub r: f32,
    pub g: f32,
    pub b: f32,
    pub a: f32,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloRenderFormat {
    Rgba8 = 0,
    Bgra8 = 1,
}

#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum VelloAaMode {
    Area = 0,
    Msaa8 = 1,
    Msaa16 = 2,
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

#[repr(C)]
#[derive(Debug, Copy, Clone)]
pub struct VelloRendererOptions {
    pub use_cpu: bool,
    pub support_area: bool,
    pub support_msaa8: bool,
    pub support_msaa16: bool,
    pub init_threads: i32,
    pub pipeline_cache: *mut c_void,
}

#[cfg(test)]
mod abi_checks {
    use super::*;
    use core::mem::{align_of, size_of};
    use static_assertions::const_assert_eq;

    const_assert_eq!(align_of::<VelloWebGpuRequestAdapterOptions>(), 4);
    const_assert_eq!(size_of::<VelloWebGpuRequestAdapterOptions>(), 8);

    #[cfg(target_pointer_width = "32")]
    const_assert_eq!(align_of::<VelloWebGpuRequestDeviceOptions>(), 8);
    #[cfg(target_pointer_width = "64")]
    const_assert_eq!(align_of::<VelloWebGpuRequestDeviceOptions>(), 8);
    #[cfg(target_pointer_width = "32")]
    const_assert_eq!(size_of::<VelloWebGpuRequestDeviceOptions>(), 16);
    #[cfg(target_pointer_width = "64")]
    const_assert_eq!(size_of::<VelloWebGpuRequestDeviceOptions>(), 24);

    const_assert_eq!(align_of::<VelloWebGpuSurfaceConfiguration>(), 4);
    const_assert_eq!(size_of::<VelloWebGpuSurfaceConfiguration>(), 12);

    const_assert_eq!(align_of::<VelloWebGpuFuturePollResult>(), 4);
    const_assert_eq!(size_of::<VelloWebGpuFuturePollResult>(), 20);

    const_assert_eq!(align_of::<VelloColor>(), 4);
    const_assert_eq!(size_of::<VelloColor>(), 16);

    const_assert_eq!(align_of::<VelloRenderParams>(), 4);
    const_assert_eq!(size_of::<VelloRenderParams>(), 32);

    #[cfg(target_pointer_width = "32")]
    const_assert_eq!(align_of::<VelloRendererOptions>(), 4);
    #[cfg(target_pointer_width = "64")]
    const_assert_eq!(align_of::<VelloRendererOptions>(), 8);
    #[cfg(target_pointer_width = "32")]
    const_assert_eq!(size_of::<VelloRendererOptions>(), 12);
    #[cfg(target_pointer_width = "64")]
    const_assert_eq!(size_of::<VelloRendererOptions>(), 16);

    const_assert_eq!(align_of::<VelloWebGpuDeviceLimits>(), 8);
    const_assert_eq!(size_of::<VelloWebGpuDeviceLimits>(), 136);
}
