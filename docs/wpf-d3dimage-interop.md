# WPF D3DImage Interop Prototype

This note captures the prototype design work for the DXGI shared-texture shim that bridges wgpu (D3D11/D3D12) to WPF's `D3DImage`, and the complementary D3D9Ex device manager that will own the compositor-facing surfaces.

## Goals
- Provide a Windows-specific hook around the existing `vello_ffi` wgpu wrappers to allocate a BGRA8 render target that advertises a legacy DXGI shared handle.
- Verify that the wgpu adapter selected for rendering matches the adapter that WPF will use through `D3D9Ex`, preventing cross-adapter sharing failures.
- Sketch how keyed mutexes (or fallbacks) will be coordinated between the wgpu writer and WPF's reader.

## Shared Texture Shim (wgpu → DXGI Shared Handle)

### High-level flow
1. Acquire the wgpu `Device` and HAL handle for the D3D11 backend (preferred) or D3D12.
2. Allocate a BGRA8 render target with `D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE` and the `D3D11_RESOURCE_MISC_SHARED` flag; optionally add `D3D11_RESOURCE_MISC_SHARED_KEYEDMUTEX` when keyed mutexes are available.
3. Retrieve the legacy `HANDLE` via `IDXGIResource::GetSharedHandle` and surface it to managed callers.
4. Record metadata: width/height, format, `DXGI_FORMAT_B8G8R8A8_UNORM`, and the adapter LUID used.

### Prototype API surface

#### Rust side (added to `ffi/vello_ffi/src/lib.rs`)

```rust
#[repr(C)]
pub struct VelloSharedTextureDesc {
    pub width: u32,
    pub height: u32,
    pub label: *const c_char,
    pub use_keyed_mutex: bool,
}

#[repr(C)]
pub struct VelloSharedTextureHandle {
    pub texture: *mut std::ffi::c_void,      // ID3D11Texture2D*
    pub shared_handle: RawHandle,            // HANDLE
    pub keyed_mutex: *mut std::ffi::c_void,  // IDXGIKeyedMutex*
    pub adapter_luid_low: u32,
    pub adapter_luid_high: i32,
}

#[no_mangle]
pub unsafe extern "C" fn vello_wgpu_device_create_shared_texture(
    device: *mut VelloWgpuDeviceHandle,
    desc: *const VelloSharedTextureDesc,
    out_handle: *mut *mut VelloSharedTextureHandle,
) -> VelloStatus {
    // Null checks omitted for brevity
    let device_ref = (*device).device.clone();
    let desc = &*desc;

    // Ensure we are on a DX11 backend; fall back to 11on12 helper otherwise.
    let hal_device = match device_ref.as_hal::<wgpu_hal::dx11::Api, _, _>(|hal| hal.cloned()) {
        Some(dev) => dev,
        None => {
            return VelloStatus::UnsupportedBackend;
        }
    };

    let mut tex_desc = D3D11_TEXTURE2D_DESC {
        Width: desc.width,
        Height: desc.height,
        MipLevels: 1,
        ArraySize: 1,
        Format: DXGI_FORMAT_B8G8R8A8_UNORM,
        SampleDesc: DXGI_SAMPLE_DESC { Count: 1, Quality: 0 },
        Usage: D3D11_USAGE_DEFAULT,
        BindFlags: D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE,
        CPUAccessFlags: 0,
        MiscFlags: if desc.use_keyed_mutex {
            D3D11_RESOURCE_MISC_SHARED | D3D11_RESOURCE_MISC_SHARED_KEYEDMUTEX
        } else {
            D3D11_RESOURCE_MISC_SHARED
        },
    };

    let mut texture: *mut ID3D11Texture2D = std::ptr::null_mut();
    unsafe {
        hal_device.CreateTexture2D(&tex_desc, std::ptr::null(), &mut texture)
    }.ok()?;

    let dxgi_resource: *mut IDXGIResource = texture.cast();
    let mut shared_handle = HANDLE::default();
    unsafe { (*dxgi_resource).GetSharedHandle(&mut shared_handle) }.ok()?;

    let keyed_mutex = if desc.use_keyed_mutex {
        let mut mutex: *mut IDXGIKeyedMutex = std::ptr::null_mut();
        unsafe { (*texture).QueryInterface(&IDXGIKeyedMutex::IID, &mut mutex.cast()) }.ok()?;
        mutex
    } else {
        std::ptr::null_mut()
    };

    let luid = unsafe {
        let mut desc = DXGI_ADAPTER_DESC1::default();
        let mut adapter = std::ptr::null_mut::<IDXGIAdapter1>();
        hal_device.GetAdapter(&mut adapter).ok()?;
        (*adapter).GetDesc1(&mut desc).ok()?;
        desc.AdapterLuid
    };

    let handle = Box::new(VelloSharedTextureHandle {
        texture: texture.cast(),
        shared_handle,
        keyed_mutex: keyed_mutex.cast(),
        adapter_luid_low: luid.LowPart,
        adapter_luid_high: luid.HighPart,
    });

    *out_handle = Box::into_raw(handle);
    VelloStatus::Ok
}
```

> **Notes**
> - `as_hal` requires enabling the `raw-window-handle` + `unsafe-hal-expose` features in `wgpu`. This prototype assumes we extend `vello_ffi` with the same feature flags already used in WinForms.
> - When only D3D12 is available, we will route through a helper that creates a D3D11 device via `D3D11On12CreateDevice` and then follows the same texture code path. That helper can live alongside this function and share the struct definitions.

### Adapter validation

The managed side can compare the LUID pair returned above with the LUID reported by the D3D9Ex device:

```csharp
internal readonly struct AdapterLuid
{
    public readonly int High;
    public readonly uint Low;

    public AdapterLuid(int high, uint low)
    {
        High = high;
        Low = low;
    }

    public bool Matches(AdapterLuid other) => High == other.High && Low == other.Low;
}

static AdapterLuid GetWpfAdapterLuid(IDirect3D9Ex d3d9)
{
    using var adapter = d3d9.GetAdapterIdentifier(0, 0);
    return new AdapterLuid(adapter.DeviceIdentifier.HighPart, adapter.DeviceIdentifier.LowPart);
}

static bool ValidateAdapters(AdapterLuid wgpu, AdapterLuid wpf, WindowsGpuDiagnostics diagnostics)
{
    if (!wgpu.Matches(wpf))
    {
        diagnostics.RecordAdapterMismatch(wgpu.High, wgpu.Low, wpf.High, wpf.Low);
        return false;
    }

    return true;
}
```

The BGRA8 texture should only be handed to `D3DImage` when the adapters match. If they differ, the shim can dispose the shared texture and instruct the control to fall back to CPU rendering until the mismatch is resolved (multi-GPU laptops, Remote Desktop, etc.).

## D3D9Ex Device Manager Sketch

### Responsibilities
- Lazily create and cache a single `IDirect3D9Ex` + `IDirect3DDevice9Ex` pair per adapter LUID.
- Open shared handles using `CreateTexture` with the `pSharedHandle` argument and store the resulting `IDirect3DSurface9` pointers.
- Expose an API to translate the wgpu-side `VelloSharedTextureHandle` into a `D3DImageBackBuffer` struct used by the WPF control.
- Coordinate keyed-mutex acquisition if present, and provide CPU flush fallback when not.

### Proposed shape (managed code)

```csharp
internal sealed class D3D9ExDeviceManager : IDisposable
{
    private readonly object _sync = new();
    private readonly AdapterLuid _luid;
    private readonly IDirect3D9Ex _d3d9;
    private readonly IDirect3DDevice9Ex _device;

    public D3D9ExDeviceManager(AdapterLuid luid)
    {
        _luid = luid;
        _d3d9 = Direct3DEx.Create();
        _device = _d3d9.CreateDeviceForLuid(luid);
    }

    public D3DImageBackBuffer AcquireBackBuffer(VelloSharedTextureHandle shared)
    {
        if (!shared.AdapterMatches(_luid))
        {
            throw new InvalidOperationException("Adapter mismatch detected.");
        }

        var texture = _device.CreateTextureFromHandle(
            shared.Width,
            shared.Height,
            D3DFMT.A8R8G8B8,
            shared.SharedHandle);

        var surface = texture.GetSurfaceLevel(0);
        return new D3DImageBackBuffer(texture, surface, shared.KeyedMutex);
    }

    public void Dispose() { /* release COM objects */ }
}
```

The helper `CreateDeviceForLuid` performs `IDirect3D9Ex::GetAdapterLUID` enumeration to find the adapter index whose LUID matches `_luid`; it then calls `CreateDeviceEx` with `D3DDEVTYPE_HAL`.

### Keyed mutex orchestration

1. When the wgpu render loop is ready to present, it calls `IDXGIKeyedMutex::AcquireSync(writeKey, timeout)` before recording GPU commands into the shared texture.
2. After submitting the command buffer and signaling completion, it calls `ReleaseSync(readKey)`—for example `(writeKey = 0, readKey = 1)`.
3. On the WPF side, just before invoking `D3DImage.Lock`, the `D3D9ExDeviceManager` (or a higher-level coordinator) calls `IDXGIKeyedMutex::AcquireSync(readKey, 0)` to ensure the GPU has finished writing.
4. Immediately after `D3DImage.Unlock`, the keyed mutex is released with `ReleaseSync(writeKey)` to allow the next wgpu frame to proceed.

For hardware lacking keyed mutex support, the pipeline falls back to `ID3D11DeviceContext::Flush()` after rendering and `IDirect3DDevice9Ex::Flush()` before unlocking the image. Diagnostics events should be fired so performance issues are visible.

### Integration points in `VelloCompositionView`
- During control initialization: acquire the shared texture via the new FFI entry point, validate adapter LUID match, and obtain the `IDirect3DSurface9` handle.
- On every present: wrap the keyed mutex acquire/release around the existing `D3DImage.Lock/AddDirtyRect/Unlock` sequence.
- On resize/device-loss: dispose the current shared texture, close the D3D9 surface, and repeat the allocation flow.

## Native Swapchain Host

The composition bridge covers the default “air-space free” scenario, but some workflows still need a real swapchain and HWND ownership. The existing `VelloNativeSwapChainHost` now sits behind a `VelloNativeSwapChainView` control that mirrors the composition API (dependency properties for `DeviceOptions`, `RenderMode`, `RenderLoopDriver`, plus `PaintSurface`/`RenderSurface` events and `RequestRender()`).

Reach for the native host when:
- Embedding DirectX-powered UI that insists on a swapchain (`IDXGISwapChain` debug overlays, PIX/RenderDoc capture, third-party controls).
- Diagnosing presentation glitches or device-loss issues where owning the HWND makes it easier to inspect messages and swapchain state.
- Prototyping full-screen or exclusive mode behaviour before wiring composition fallbacks.

Example XAML:

```xml
<vello:VelloNativeSwapChainView RenderMode="Continuous"
                                RenderLoopDriver="CompositionTarget"
                                PreferredBackend="Gpu"
                                PaintSurface="OnPaintSurface" />
```

The WPF sample sticks with the composition bridge so overlay controls render correctly; VelloNativeSwapChainView stays available as an opt-in control for diagnostics or deep DirectX interop experiments.
- If the bundled `vello_ffi` native library was built before the shared-texture entry points landed, `VelloView` now shows an in-control placeholder explaining how to update the binaries instead of repeatedly throwing at runtime.

## Follow-up Tasks
- Flesh out the `D3D11On12` helper used when wgpu exposes only the D3D12 backend.
- Add diagnostics hooks for adapter mismatch, keyed mutex contention, and flush fallbacks.
- Provide unit tests that validate adapter matching logic using fake LUIDs and mock COM wrappers.


