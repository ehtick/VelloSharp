# VelloSharp Binding Status

This document captures the current state of the .NET bindings for Vello and the work required to make them production ready.

## Current State

- `VelloSharp` currently exposes only `Renderer.Render(...)` and `Scene.FillPath`/`Scene.StrokePath`. Everything else in Vello’s scene API (layers, brushes, images, text, blur primitives, gradients, glyph runs) remains inaccessible. See `VelloSharp/VelloScene.cs`, `VelloSharp/VelloRenderer.cs`.
- Interop still relies on manual `.dylib` copies, but the `vello_ffi` layer now ships with feature-gated diagnostics, a single map/unmap readback path, and selectable RGBA/BGRA output.
- Samples render via copying into Avalonia’s `WriteableBitmap`; there is no Skia/Vello interop helper, no GPU render path, and no automated verification.

## Completed

- **Hardened the FFI layer:** removed unconditional diagnostics, tightened the readback pipeline (single map/unmap per frame with correct texture usages), added RGBA/BGRA selection, and exposed feature flags so the Rust crate can be redistributed cleanly.
- **Exposed additional scene/renderer functionality:** layers, gradient/image brushes, blurred rectangles, glyph runs, renderer options, fonts, and image helpers are now available via the FFI and .NET wrappers.
- **Automated native loading:** `dotnet build` now drives `cargo` to produce `libvello_ffi`, lays it out under `runtimes/<rid>/native`, and the managed assembly resolves it via a `DllImport` resolver at runtime.

## Completion Plan

- **Create safe C# abstractions:** manage resource lifetimes via `IDisposable`, introduce immutable structs for colors/gradients, provide span-friendly glyph/path builders, and ensure argument validation/error propagation through `NativeHelpers`.
- **Add integration helpers:** supply an Avalonia control owning the renderer, a SkiaSharp `SKBitmap`/`SKSurface` bridge, and CPU/GPU render paths with stride/format negotiation.
- **Improve coverage:** add unit tests for marshaling and stroke/fill behaviours, render smoke tests that hash outputs, CI jobs building Rust and .NET, and documentation in `README.md` covering setup, threading, and deployment.
