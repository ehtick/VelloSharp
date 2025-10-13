# VelloSharp.Ffi.Sparse

`VelloSharp.Ffi.Sparse` surfaces the sparse rendering entry points within the native Vello toolchain, enabling advanced memory layouts and tile-based workloads.

## Getting Started

1. Install the package: `dotnet add package VelloSharp.Ffi.Sparse`.
2. Import `using VelloSharp.Ffi.Sparse;` wherever you work with sparse buffers or tile-aware rendering.
3. Deploy the matching `VelloSharp.Native.VelloSparse` assets with your application so the underlying native library loads successfully.
4. Combine the sparse FFI with managed helpers in `VelloSharp` or `VelloSharp.Gpu` to schedule work that targets sparse surfaces.

## Usage Example

```csharp
using System;
using VelloSharp;

var context = SparseNativeMethods.vello_sparse_render_context_create(640, 480);
try
{
    SparseNativeHelpers.ThrowOnError(
        SparseNativeMethods.vello_sparse_render_context_set_aliasing_threshold(context, 4),
        nameof(SparseNativeMethods.vello_sparse_render_context_set_aliasing_threshold));
}
finally
{
    SparseNativeMethods.vello_sparse_render_context_destroy(context);
}
```

## Next Steps

- Inspect the API reference to understand the structure layout expectations and capability flags.
- Prototype sparse workloads in a controlled environment before rolling the feature into customer-facing builds.

