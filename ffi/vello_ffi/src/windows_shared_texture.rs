use std::ffi::{CStr, c_void};
use std::ptr;
use windows::Win32::Graphics::Dxgi::DXGI_ERROR_WAS_STILL_DRAWING;

use windows::Win32::Foundation::{CloseHandle, E_INVALIDARG, HANDLE};
use windows::Win32::Graphics::Direct3D::{D3D_FEATURE_LEVEL_11_0, D3D_FEATURE_LEVEL_11_1};
use windows::Win32::Graphics::Direct3D11::{
    D3D11_BIND_RENDER_TARGET, D3D11_BIND_SHADER_RESOURCE, D3D11_CREATE_DEVICE_BGRA_SUPPORT,
    D3D11_RESOURCE_MISC_SHARED, D3D11_RESOURCE_MISC_SHARED_KEYEDMUTEX, D3D11_TEXTURE2D_DESC,
    D3D11_USAGE_DEFAULT, ID3D11Device, ID3D11DeviceContext, ID3D11Texture2D,
};
use windows::Win32::Graphics::Direct3D11on12::D3D11On12CreateDevice;
use windows::Win32::Graphics::Direct3D12::{ID3D12CommandQueue, ID3D12Device, ID3D12Resource};
use windows::Win32::Graphics::Dxgi::Common::{DXGI_FORMAT_B8G8R8A8_UNORM, DXGI_SAMPLE_DESC};
use windows::Win32::Graphics::Dxgi::{
    DXGI_ADAPTER_DESC1, IDXGIAdapter1, IDXGIDevice, IDXGIKeyedMutex, IDXGIResource,
};
use windows::core::{IUnknown, Interface};

use wgpu::hal as wgpu_hal;
use wgpu::hal::api::Dx12;

use crate::{
    VelloSharedTextureDesc, VelloSharedTextureHandle, VelloStatus, VelloWgpuDeviceHandle,
    VelloWgpuTextureHandle, set_last_error,
};

struct SharedTextureInner {
    _d3d11_device: ID3D11Device,
    _d3d11_context: Option<ID3D11DeviceContext>,
    _texture: ID3D11Texture2D,
    keyed_mutex: Option<IDXGIKeyedMutex>,
    shared_handle: HANDLE,
    _adapter_desc: DXGI_ADAPTER_DESC1,
    wgpu_texture: *mut VelloWgpuTextureHandle,
}

impl SharedTextureInner {
    fn new(
        device: &VelloWgpuDeviceHandle,
        desc: &VelloSharedTextureDesc,
    ) -> Result<(Box<VelloSharedTextureHandle>, Box<Self>), VelloStatus> {
        if desc.width == 0 || desc.height == 0 {
            set_last_error("Shared texture dimensions must be greater than zero");
            return Err(VelloStatus::InvalidArgument);
        }

        let dx12_device_guard = unsafe { device.device.as_hal::<Dx12>() }.ok_or_else(|| {
            set_last_error("The wgpu device does not expose a Direct3D 12 backend");
            VelloStatus::Unsupported
        })?;
        let dx12_device: ID3D12Device = dx12_device_guard.raw_device().clone();

        let dx12_queue_guard = unsafe { device.queue.as_hal::<Dx12>() }.ok_or_else(|| {
            set_last_error("The wgpu queue does not expose a Direct3D 12 backend");
            VelloStatus::Unsupported
        })?;
        let dx12_queue: ID3D12CommandQueue = dx12_queue_guard.as_raw().clone();

        let feature_levels = [D3D_FEATURE_LEVEL_11_1, D3D_FEATURE_LEVEL_11_0];
        let queue_unknown: IUnknown = dx12_queue.into();
        let queue_slice: [Option<IUnknown>; 1] = [Some(queue_unknown.clone())];

        let mut d3d11_device: Option<ID3D11Device> = None;
        let mut d3d11_context: Option<ID3D11DeviceContext> = None;
        let mut chosen_feature = D3D_FEATURE_LEVEL_11_0;

        unsafe {
            D3D11On12CreateDevice(
                &dx12_device,
                D3D11_CREATE_DEVICE_BGRA_SUPPORT.0,
                Some(&feature_levels),
                Some(&queue_slice),
                0,
                Some(&mut d3d11_device),
                Some(&mut d3d11_context),
                Some(&mut chosen_feature),
            )
        }
        .map_err(|err| {
            set_last_error(format!(
                "D3D11On12CreateDevice failed: 0x{0:08x}",
                err.code().0 as u32
            ));
            VelloStatus::DeviceCreationFailed
        })?;

        let d3d11_device = d3d11_device.expect("ID3D11Device");

        let mut keyed_mutex_requested = desc.use_keyed_mutex;
        let mut desc2d = D3D11_TEXTURE2D_DESC {
            Width: desc.width,
            Height: desc.height,
            MipLevels: 1,
            ArraySize: 1,
            Format: DXGI_FORMAT_B8G8R8A8_UNORM,
            SampleDesc: DXGI_SAMPLE_DESC {
                Count: 1,
                Quality: 0,
            },
            Usage: D3D11_USAGE_DEFAULT,
            BindFlags: (D3D11_BIND_RENDER_TARGET.0 | D3D11_BIND_SHADER_RESOURCE.0) as u32,
            CPUAccessFlags: 0,
            MiscFlags: if keyed_mutex_requested {
                (D3D11_RESOURCE_MISC_SHARED.0 | D3D11_RESOURCE_MISC_SHARED_KEYEDMUTEX.0) as u32
            } else {
                D3D11_RESOURCE_MISC_SHARED.0 as u32
            },
        };

        let mut texture: Option<ID3D11Texture2D> = None;
        let mut create_result =
            unsafe { d3d11_device.CreateTexture2D(&desc2d, None, Some(&mut texture)) };

        if let Err(ref err) = create_result {
            if keyed_mutex_requested && err.code() == E_INVALIDARG {
                keyed_mutex_requested = false;
                desc2d.MiscFlags = D3D11_RESOURCE_MISC_SHARED.0 as u32;
                texture = None;
                create_result =
                    unsafe { d3d11_device.CreateTexture2D(&desc2d, None, Some(&mut texture)) };
            }
        }

        create_result.map_err(|err| {
            set_last_error(format!(
                "CreateTexture2D failed: 0x{0:08x}",
                err.code().0 as u32
            ));
            VelloStatus::DeviceCreationFailed
        })?;

        let texture = texture.expect("ID3D11Texture2D");

        let dxgi_resource: IDXGIResource = texture.clone().cast().map_err(|err| {
            set_last_error(format!(
                "QueryInterface IDXGIResource failed: 0x{0:08x}",
                err.code().0 as u32
            ));
            VelloStatus::DeviceCreationFailed
        })?;

        let shared_handle = unsafe { dxgi_resource.GetSharedHandle() }.map_err(|err| {
            set_last_error(format!(
                "GetSharedHandle failed: 0x{0:08x}",
                err.code().0 as u32
            ));
            VelloStatus::DeviceCreationFailed
        })?;

        let keyed_mutex = if keyed_mutex_requested {
            Some(texture.clone().cast().map_err(|err| {
                set_last_error(format!(
                    "QueryInterface IDXGIKeyedMutex failed: 0x{0:08x}",
                    err.code().0 as u32
                ));
                VelloStatus::DeviceCreationFailed
            })?)
        } else {
            None
        };

        let dxgi_device: IDXGIDevice = d3d11_device.clone().cast().map_err(|err| {
            set_last_error(format!(
                "QueryInterface IDXGIDevice failed: 0x{0:08x}",
                err.code().0 as u32
            ));
            VelloStatus::DeviceCreationFailed
        })?;

        let adapter = unsafe { dxgi_device.GetAdapter() }.map_err(|err| {
            set_last_error(format!(
                "IDXGIDevice::GetAdapter failed: 0x{0:08x}",
                err.code().0 as u32
            ));
            VelloStatus::DeviceCreationFailed
        })?;

        let adapter: IDXGIAdapter1 = adapter.cast().map_err(|err| {
            set_last_error(format!(
                "QueryInterface IDXGIAdapter1 failed: 0x{0:08x}",
                err.code().0 as u32
            ));
            VelloStatus::DeviceCreationFailed
        })?;

        let adapter_desc = unsafe { adapter.GetDesc1() }.map_err(|err| {
            set_last_error(format!(
                "IDXGIAdapter1::GetDesc1 failed: 0x{0:08x}",
                err.code().0 as u32
            ));
            VelloStatus::DeviceCreationFailed
        })?;

        let label = if desc.label.is_null() {
            None
        } else {
            Some(
                unsafe { CStr::from_ptr(desc.label) }
                    .to_string_lossy()
                    .into_owned(),
            )
        };

        let mut d3d12_resource: Option<ID3D12Resource> = None;
        unsafe { dx12_device.OpenSharedHandle(shared_handle, &mut d3d12_resource) }.map_err(
            |err| {
                set_last_error(format!(
                    "ID3D12Device::OpenSharedHandle failed: 0x{0:08x}",
                    err.code().0 as u32
                ));
                VelloStatus::DeviceCreationFailed
            },
        )?;

        let d3d12_resource = d3d12_resource.ok_or_else(|| {
            set_last_error("ID3D12Device::OpenSharedHandle returned null resource");
            VelloStatus::DeviceCreationFailed
        })?;

        let extent = wgpu::Extent3d {
            width: desc.width,
            height: desc.height,
            depth_or_array_layers: 1,
        };

        let hal_texture = unsafe {
            wgpu_hal::dx12::Device::texture_from_raw(
                d3d12_resource.clone(),
                wgpu::TextureFormat::Bgra8Unorm,
                wgpu::TextureDimension::D2,
                extent,
                1,
                1,
            )
        };

        let texture_descriptor = wgpu::TextureDescriptor {
            label: label.as_deref(),
            size: extent,
            mip_level_count: 1,
            sample_count: 1,
            dimension: wgpu::TextureDimension::D2,
            format: wgpu::TextureFormat::Bgra8Unorm,
            usage: wgpu::TextureUsages::RENDER_ATTACHMENT
                | wgpu::TextureUsages::TEXTURE_BINDING
                | wgpu::TextureUsages::COPY_SRC,
            view_formats: &[],
        };

        let wgpu_texture = unsafe {
            device
                .device
                .create_texture_from_hal::<Dx12>(hal_texture, &texture_descriptor)
        };

        let boxed_wgpu_texture = Box::new(VelloWgpuTextureHandle {
            texture: wgpu_texture,
        });

        let wgpu_texture_ptr = Box::into_raw(boxed_wgpu_texture);

        let keyed_mutex_ptr = keyed_mutex
            .as_ref()
            .map(|mutex: &IDXGIKeyedMutex| mutex.as_raw() as *mut c_void)
            .unwrap_or(ptr::null_mut());

        let inner = Box::new(SharedTextureInner {
            _d3d11_device: d3d11_device,
            _d3d11_context: d3d11_context,
            _texture: texture.clone(),
            keyed_mutex,
            shared_handle,
            _adapter_desc: adapter_desc,
            wgpu_texture: wgpu_texture_ptr,
        });

        let handle = Box::new(VelloSharedTextureHandle {
            texture: texture.as_raw() as *mut c_void,
            shared_handle: shared_handle.0 as usize as *mut c_void,
            keyed_mutex: keyed_mutex_ptr,
            wgpu_texture: wgpu_texture_ptr,
            adapter_luid_low: adapter_desc.AdapterLuid.LowPart,
            adapter_luid_high: adapter_desc.AdapterLuid.HighPart,
            width: desc.width,
            height: desc.height,
            reserved: ptr::null_mut(),
        });

        Ok((handle, inner))
    }
}

impl Drop for SharedTextureInner {
    fn drop(&mut self) {
        unsafe {
            if !self.wgpu_texture.is_null() {
                drop(Box::from_raw(self.wgpu_texture));
                self.wgpu_texture = std::ptr::null_mut();
            }
            if !self.shared_handle.is_invalid() {
                let _ = CloseHandle(self.shared_handle);
            }
        }
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_wgpu_device_create_shared_texture(
    device: *mut VelloWgpuDeviceHandle,
    desc: *const VelloSharedTextureDesc,
    out_handle: *mut *mut VelloSharedTextureHandle,
) -> VelloStatus {
    if device.is_null() || desc.is_null() || out_handle.is_null() {
        set_last_error("Null pointer passed to vello_wgpu_device_create_shared_texture");
        return VelloStatus::NullPointer;
    }

    let device_ref = unsafe { &*device };
    let desc_ref = unsafe { &*desc };

    match SharedTextureInner::new(device_ref, desc_ref) {
        Ok((mut handle, inner)) => {
            handle.reserved = Box::into_raw(inner) as *mut c_void;
            unsafe {
                *out_handle = Box::into_raw(handle);
            }
            VelloStatus::Success
        }
        Err(status) => status,
    }
}

fn shared_texture_inner(
    handle: *mut VelloSharedTextureHandle,
) -> Result<&'static mut SharedTextureInner, VelloStatus> {
    if handle.is_null() {
        set_last_error("Shared texture handle is null");
        return Err(VelloStatus::NullPointer);
    }

    let shared = unsafe { &mut *handle };
    if shared.reserved.is_null() {
        set_last_error("Shared texture metadata is unavailable");
        return Err(VelloStatus::NullPointer);
    }

    let inner = unsafe { &mut *(shared.reserved as *mut SharedTextureInner) };
    Ok(inner)
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_shared_texture_acquire_mutex(
    handle: *mut VelloSharedTextureHandle,
    key: u64,
    timeout_ms: u32,
) -> VelloStatus {
    match shared_texture_inner(handle) {
        Ok(inner) => {
            let Some(mutex) = inner.keyed_mutex.as_ref() else {
                set_last_error("Shared texture does not expose a keyed mutex");
                return VelloStatus::Unsupported;
            };

            match unsafe { mutex.AcquireSync(key, timeout_ms) } {
                Ok(()) => VelloStatus::Success,
                Err(err) => {
                    let code = err.code();
                    if code == DXGI_ERROR_WAS_STILL_DRAWING {
                        return VelloStatus::Timeout;
                    }

                    set_last_error(format!(
                        "IDXGIKeyedMutex::AcquireSync failed: 0x{0:08x}",
                        code.0 as u32
                    ));
                    VelloStatus::RenderError
                }
            }
        }
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_shared_texture_release_mutex(
    handle: *mut VelloSharedTextureHandle,
    key: u64,
) -> VelloStatus {
    match shared_texture_inner(handle) {
        Ok(inner) => {
            let Some(mutex) = inner.keyed_mutex.as_ref() else {
                set_last_error("Shared texture does not expose a keyed mutex");
                return VelloStatus::Unsupported;
            };

            match unsafe { mutex.ReleaseSync(key) } {
                Ok(()) => VelloStatus::Success,
                Err(err) => {
                    set_last_error(format!(
                        "IDXGIKeyedMutex::ReleaseSync failed: 0x{0:08x}",
                        err.code().0 as u32
                    ));
                    VelloStatus::RenderError
                }
            }
        }
        Err(status) => status,
    }
}

#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_shared_texture_flush(
    handle: *mut VelloSharedTextureHandle,
) -> VelloStatus {
    match shared_texture_inner(handle) {
        Ok(inner) => {
            if let Some(context) = inner._d3d11_context.as_ref() {
                unsafe { context.Flush() };
            }

            VelloStatus::Success
        }
        Err(status) => status,
    }
}
#[unsafe(no_mangle)]
pub unsafe extern "C" fn vello_shared_texture_destroy(handle: *mut VelloSharedTextureHandle) {
    if handle.is_null() {
        return;
    }

    let boxed = unsafe { Box::from_raw(handle) };
    if !boxed.reserved.is_null() {
        unsafe {
            drop(Box::from_raw(boxed.reserved as *mut SharedTextureInner));
        }
    }
}
