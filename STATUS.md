# VelloSharp Binding Status

This document captures the current state of the .NET bindings for Vello and the work required to make them production ready.

## Current State

- `VelloSharp` currently exposes only `Renderer.Render(...)` and `Scene.FillPath`/`Scene.StrokePath`. Everything else in Vello’s scene API (layers, brushes, images, text, blur primitives, gradients, glyph runs) remains inaccessible. See `VelloSharp/VelloScene.cs`, `VelloSharp/VelloRenderer.cs`.
- Interop still relies on manual `.dylib` copies, but the `vello_ffi` layer now ships with feature-gated diagnostics, a single map/unmap readback path, and selectable RGBA/BGRA output.
- Samples now rely on the shared Avalonia control; Skia interop and render-path utilities are provided via `VelloSharp.Integration`, while automated verification is still pending.
- A surface-backed render path exists but is limited to Win32 and AppKit handles; Avalonia integration uses `VelloSurfaceView`, which falls back to the bitmap path when swapchain creation fails.

## Completed

- **Hardened the FFI layer:** removed unconditional diagnostics, tightened the readback pipeline (single map/unmap per frame with correct texture usages), added RGBA/BGRA selection, and exposed feature flags so the Rust crate can be redistributed cleanly.
- **Exposed additional scene/renderer functionality:** layers, gradient/image brushes, blurred rectangles, glyph runs, renderer options, fonts, and image helpers are now available via the FFI and .NET wrappers.
- **Automated native loading:** `dotnet build` now drives `cargo` to produce `libvello_ffi`, lays it out under `runtimes/<rid>/native`, and the managed assembly resolves it via a `DllImport` resolver at runtime.
- **Integration helpers:** introduced `VelloSharp.Integration` with an Avalonia `VelloView`, `SkiaRenderBridge`, and stride/format-negotiating render-path utilities for CPU and GPU targets.
- **Surface API prototype:** added `vello_render_context*`/`vello_render_surface*` FFI calls, managed wrappers (`VelloSurfaceContext`, `VelloSurface`, `VelloSurfaceRenderer`), and a headless smoke test that exercises the GPU pipeline without CPU readback. Avalonia gains `VelloSurfaceView`, which acquires native window handles and seamlessly falls back to the bitmap control.

## Completion Plan

- **Expand surface support:** expose Wayland/X11 handle helpers, integrate with Skia swapchains, and support partial-region presentation so the control can coexist with other Avalonia content.
- **Create safe C# abstractions:** manage resource lifetimes via `IDisposable`, introduce immutable structs for colors/gradients, provide span-friendly glyph/path builders, and ensure argument validation/error propagation through `NativeHelpers`.
- **Improve coverage:** add unit tests for marshaling and stroke/fill behaviours, render smoke tests that hash outputs, CI jobs building Rust and .NET, and documentation in `README.md` covering setup, threading, and deployment.
