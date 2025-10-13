# VelloSharp.Ffi.Gpu

`VelloSharp.Ffi.Gpu` extends the FFI layer with GPU-specific bindings required to operate native rendering pipelines.

## Getting Started

1. Install using `dotnet add package VelloSharp.Ffi.Gpu`.
2. Add `using VelloSharp.Ffi.Gpu;` in modules that configure GPU-backed rendering through native interop.
3. Make sure the GPU native binaries are available via the `VelloSharp.Native.*` bundles that match your platform.
4. Wrap the GPU handles with managed abstractions or use them through `VelloSharp.Gpu` to simplify resource lifetime management.

## Usage Example

```csharp
using System;
using VelloSharp;

var rendererHandle = NativeMethods.vello_renderer_create(800, 600);
if (rendererHandle == IntPtr.Zero)
{
    throw new InvalidOperationException(NativeHelpers.GetLastErrorMessage() ?? "Renderer creation failed.");
}
NativeMethods.vello_renderer_destroy(rendererHandle);
```

## Next Steps

- Review the API reference for the list of exported GPU commands and structures.
- Validate compatibility with your target backend (Vulkan, Metal, Direct3D) before enabling advanced features in production.

