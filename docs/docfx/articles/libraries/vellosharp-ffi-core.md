# VelloSharp.Ffi.Core

`VelloSharp.Ffi.Core` is the low-level interop layer that binds the managed runtime to the native Vello libraries.

## Getting Started

1. Add the package with `dotnet add package VelloSharp.Ffi.Core`.
2. Reference `using VelloSharp.Ffi.Core;` where you need direct access to the native handles and P/Invoke surface.
3. Ensure the corresponding native assets from the `VelloSharp.Native.*` packages are present at runtime before invoking the FFI entry points.
4. Wrap the raw handles in higher-level abstractions from `VelloSharp` or your own infrastructure to manage lifetime safely.

## Usage Example

```csharp
using System;
using VelloSharp;

IntPtr hwnd = /* obtain your HWND (for example via Form.Handle) */;
IntPtr hinstance = /* retrieve the module handle associated with that window */;
var win32Handle = SurfaceHandle.FromWin32(hwnd, hinstance);
var descriptor = new SurfaceDescriptor
{
    Width = 1920,
    Height = 1080,
    PresentMode = PresentMode.AutoVsync,
    Handle = win32Handle,
};
```

## Next Steps

- Use the API reference to inspect the raw structures, enums, and function signatures exported by the native layer.
- Treat the FFI surface as an advanced scenario; prefer the higher-level packages unless you need direct interop.

