# Vello WebGPU FFI ABI Notes

This document captures the layout guarantees for the C ABI exposed by
`ffi/vello_webgpu_ffi`.  All exported structs are annotated with
`#[repr(C)]` and validated through compile-time assertions so that both
native (`x86_64`) and browser (`wasm32`) builds agree on field ordering,
alignment, and total size.

The table below summarises the derived layout information.  Values marked
with `–` are identical on 32-bit and 64-bit targets.

| Type | Align (bytes) | Size (wasm32) | Size (x86_64) | Notes |
| ---- | ------------- | ------------- | ------------- | ----- |
| `VelloWebGpuRequestAdapterOptions` | 4 | 8 | 8 | trailing padding after `force_fallback_adapter` |
| `VelloWebGpuRequestDeviceOptions` | 8 | 16 | 24 | pointer-sized `label`; keep pointer-width specific packing in mind |
| `VelloWebGpuSurfaceConfiguration` | 4 | 12 | 12 | packed `u32` fields |
| `VelloWebGpuFuturePollResult` | 4 | 20 | 20 | five `u32` fields |
| `VelloWebGpuDeviceLimits` | 8 | 136 | 136 | mix of `u32`/`u64`, padded to 8-byte multiple |
| `VelloColor` | 4 | 16 | 16 | four `f32` values |
| `VelloRenderParams` | 4 | 32 | 32 | includes embedded `VelloColor` |
| `VelloRendererOptions` | 4 (wasm32) / 8 (x86_64) | 12 | 16 | pointer-sized `pipeline_cache` |

The layout checks live in `ffi/vello_webgpu_ffi/src/types.rs` and are enforced
via the [`static_assertions`](https://crates.io/crates/static_assertions) crate.
Any change to the ABI will cause a compile-time failure, ensuring updates are
intentional and verified on both pointer widths.

When re-generating headers with `cbindgen`, include these notes (or link to this
file) so downstream consumers understand the pointer-width dependent structs.
