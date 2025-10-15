#![cfg_attr(not(test), deny(clippy::all))]
#![allow(missing_docs)]
#![allow(missing_debug_implementations)]

mod types;
pub use types::*;

#[cfg(target_arch = "wasm32")]
mod wasm;

#[cfg(target_arch = "wasm32")]
pub use wasm::*;

#[cfg(not(target_arch = "wasm32"))]
mod unsupported {
    use super::*;
    use core::ffi::{c_char, c_void};
    use core::ptr;

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn vello_webgpu_initialize() -> VelloWebGpuStatus {
        VelloWebGpuStatus::Unsupported
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn vello_webgpu_shutdown() {}

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn vello_webgpu_last_error_message() -> *const c_char {
        ptr::null()
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn vello_webgpu_request_adapter_async(
        _options: *const VelloWebGpuRequestAdapterOptions,
        _out_future_id: *mut u32,
    ) -> VelloWebGpuStatus {
        VelloWebGpuStatus::Unsupported
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn vello_webgpu_future_poll(
        _future_id: u32,
        out_result: *mut VelloWebGpuFuturePollResult,
    ) -> VelloWebGpuStatus {
        if !out_result.is_null() {
            unsafe {
                *out_result = VelloWebGpuFuturePollResult::default();
            }
        }
        VelloWebGpuStatus::Unsupported
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn vello_webgpu_request_device_async(
        _adapter_handle: u32,
        _options: *const VelloWebGpuRequestDeviceOptions,
        _out_future_id: *mut u32,
    ) -> VelloWebGpuStatus {
        VelloWebGpuStatus::Unsupported
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn vello_webgpu_device_destroy(_handle: u32) -> VelloWebGpuStatus {
        VelloWebGpuStatus::Unsupported
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn vello_webgpu_device_get_limits(
        _device_handle: u32,
        out_limits: *mut VelloWebGpuDeviceLimits,
    ) -> VelloWebGpuStatus {
        if !out_limits.is_null() {
            unsafe {
                *out_limits = VelloWebGpuDeviceLimits::default();
            }
        }
        VelloWebGpuStatus::Unsupported
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn vello_webgpu_queue_destroy(_handle: u32) -> VelloWebGpuStatus {
        VelloWebGpuStatus::Unsupported
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn vello_webgpu_surface_from_canvas_selector(
        _selector: *const c_char,
        out_surface_handle: *mut u32,
    ) -> VelloWebGpuStatus {
        if !out_surface_handle.is_null() {
            unsafe {
                *out_surface_handle = 0;
            }
        }
        VelloWebGpuStatus::Unsupported
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn vello_webgpu_surface_from_canvas_id(
        _canvas_id: *const c_char,
        out_surface_handle: *mut u32,
    ) -> VelloWebGpuStatus {
        if !out_surface_handle.is_null() {
            unsafe {
                *out_surface_handle = 0;
            }
        }
        VelloWebGpuStatus::Unsupported
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn vello_webgpu_surface_destroy(_handle: u32) -> VelloWebGpuStatus {
        VelloWebGpuStatus::Unsupported
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn vello_webgpu_surface_configure(
        _surface_handle: u32,
        _adapter_handle: u32,
        _device_handle: u32,
        _configuration: *const VelloWebGpuSurfaceConfiguration,
    ) -> VelloWebGpuStatus {
        VelloWebGpuStatus::Unsupported
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn vello_webgpu_surface_acquire_next_texture(
        _surface_handle: u32,
        out_texture_handle: *mut u32,
    ) -> VelloWebGpuStatus {
        if !out_texture_handle.is_null() {
            unsafe {
                *out_texture_handle = 0;
            }
        }
        VelloWebGpuStatus::Unsupported
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn vello_webgpu_surface_present(
        _surface_handle: u32,
        _texture_handle: u32,
    ) -> VelloWebGpuStatus {
        VelloWebGpuStatus::Unsupported
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn vello_webgpu_surface_texture_destroy(
        _texture_handle: u32,
    ) -> VelloWebGpuStatus {
        VelloWebGpuStatus::Unsupported
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn vello_webgpu_surface_get_current_texture_format(
        _surface_handle: u32,
        out_format: *mut VelloWebGpuTextureFormat,
    ) -> VelloWebGpuStatus {
        if !out_format.is_null() {
            unsafe {
                *out_format = VelloWebGpuTextureFormat::Undefined;
            }
        }
        VelloWebGpuStatus::Unsupported
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn vello_webgpu_surface_resize_canvas(
        _surface_handle: u32,
        _logical_width: f32,
        _logical_height: f32,
        _device_pixel_ratio: f32,
    ) -> VelloWebGpuStatus {
        VelloWebGpuStatus::Unsupported
    }

    #[unsafe(no_mangle)]
    pub unsafe extern "C" fn vello_webgpu_set_log_callback(
        _callback: Option<unsafe extern "C" fn(VelloWebGpuLogLevel, *const c_char, *mut c_void)>,
        _user_data: *mut c_void,
    ) {
    }
}

#[cfg(not(target_arch = "wasm32"))]
pub use unsupported::*;
