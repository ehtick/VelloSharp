# Avalonia Browser Vello WebGPU Integration Plan

## Background & Context
- The “Integrating Rust Vello WASM with Avalonia Browser for High-Performance Rendering (No JS Interop)” PDF establishes a proof-of-concept pipeline where Rust-produced WebAssembly exposes `extern "C"` entry points consumed by Avalonia Browser without invoking JavaScript.
- Current production FFI crates (`ffi/vello_ffi`, `ffi/wgpu_ffi`, etc.) target native hosts (Win32, macOS, Linux). The managed bindings live in `bindings/VelloSharp.*` and assume POSIX- or Win32-style dynamic libraries.
- Avalonia Browser today renders through Skia/WASM; GPU-backed rendering is blocked on a browser-ready swapchain implementation that can exercise WebGPU safely from WebAssembly.
- Goal: extend the existing FFI surface so a single WebAssembly module (Rust) can own WebGPU device/surface creation, expose deterministic ABI to .NET, and enable an Avalonia Browser host + sample (`samples/AvaloniaVelloBrowserDemo`) that renders through Vello without any JavaScript interop code paths.

## Goals
1. [x] Deliver a browser-ready Rust runtime that compiles to `wasm32-unknown-unknown`, exports a Vello/WebGPU-centric C ABI, and implements the no-JS interop model defined in the PDF.
2. [x] Provide managed bindings (`bindings/VelloSharp.WebAssembly`) that mirror the native bindings API, hide asynchronous WebGPU initialization differences, and integrate with Avalonia Browser hosting APIs.
3. [ ] Support full WebGPU feature negotiation (adapter/device selection, surface configuration, presentation, capture) through the FFI, including browser-specific limits/capabilities.
4. [ ] Wire Avalonia Browser’s rendering pipeline to the new bindings, delivering a functional sample with resizing, DPI scaling, composition, diagnostics, and graceful fallback when WebGPU is unavailable.
5. [ ] Ensure CI/build tooling can produce the WebAssembly artifact, package it alongside managed assemblies, and verify it loads under `dotnet publish -t:Run` for the browser profile.

## Non-Goals & Constraints
- Do not introduce JavaScript glue helpers; rely on Rust + WebAssembly component bindings and .NET interop only.
- Do not attempt to backport legacy WebGL paths or provide CPU fallbacks in this iteration; Skia/Avalonia skia remains the fallback renderer.
- Avoid diverging from upstream wgpu/vello APIs; stick to extension points already earmarked for WebGPU support.
- Keep browser assets (WASM, JS harness) self-contained per sample; broader documentation/marketing updates are deferred.

## Architecture Overview

### Layered Responsibilities
- **Rust WebAssembly runtime (`ffi/vello_webgpu_ffi`)** – wraps `wgpu`’s web backend, exposes device/surface management, scene rendering, and async adapter/device requests via a stable C ABI callable from .NET.
- **Managed interop layer (`bindings/VelloSharp.WebAssembly`)** – provides P/Invoke bindings into the WASM exports, adapter/device/task helpers, marshaling for descriptors, and diagnostics hooks aligning with existing managed abstractions.
- **Avalonia Browser integration (`VelloSharp.Avalonia.Browser`)** – implements Avalonia’s `IPlatformRenderInterface`/swapchain hooks for the browser target, translating Avalonia’s drawing commands into Vello scenes and targeting the WebGPU surface.
- **Sample & tooling** – `samples/AvaloniaVelloBrowserDemo` hosts Avalonia in the browser profile, bootstraps the WASM module, and demonstrates dynamic content, input, scaling, and diagnostics overlays.

### Render Flow (target state)
1. Avalonia Browser bootstrap loads the Rust WASM module (compiled with `#[no_mangle] extern "C"` exports) alongside the .NET runtime.
2. Managed bindings call `vello_webgpu_initialize` with handles describing the canvas and desired adapter/device configuration.
3. Rust runtime requests the browser’s WebGPU adapter/device asynchronously, returning opaque handles through the ABI; managed code awaits completion via promise/future shims.
4. Avalonia render loop builds scenes via existing VelloSharp helpers, submits them to `vello_webgpu_renderer_render`, and presents them via `wgpu::Queue::submit`.
5. Resize/DPI events trigger `vello_webgpu_surface_configure` calls; browser visibility/state changes drive suspend/resume logic via managed hooks.

## Detailed Workstreams

### 1. Rust WebAssembly Runtime Preparation
1.1. [x] Create `ffi/vello_webgpu_ffi` targeting `wasm32-unknown-unknown`, mirroring workspace layout, `cbindgen`, and feature flag conventions.
1.2. [x] Author cross-platform WASM build scripts (`scripts/build-wasm-vello.ps1/.sh`) that invoke `cargo build`, `wasm-bindgen`, and `wasm-opt -O2`.
1.3. [x] Enable the `wgpu` web backend (`wgpu::Backends::BROWSER_WEBGPU`) with matching Cargo features (`webgpu`, optional `webgl` fallback, `wasm-bindgen` DOM glue).
1.4. [x] Implement async adapter/device acquisition using `wasm-bindgen-futures::JsFuture` and expose it through a pollable `FutureRegistry`.

### 2. FFI Surface & Device API Design

2.1. [x] Define initialization and shutdown exports

## Design Options Explored (WASM Support)

### Option A — Minimal JS bridge (fastest to working)
- Summary: Keep Rust built with `wasm-bindgen` (web-sys/wgpu web backend). Add a tiny JS module that loads `vello_webgpu_ffi_bg.wasm` via the generated `vello_webgpu_ffi.js` and exposes a stable, C-like surface to .NET using `[JSImport]` on the browser target. Managed code calls JS; JS writes/reads raw structs in wasm memory and forwards to Rust exports.
- Pros:
  - Unblocks immediately; aligns with wgpu’s web backend (wasm-bindgen).
  - No changes to Rust crate layout or to .NET runtime.
  - Good runtime perf when calls are batched; heavy work remains in Rust.
- Cons:
  - Requires hand-written JS shims for non-trivial out/ref structs.
  - JS boundary exists (though amortized by batching).
  - Diverges from the “no-JS interop” aspiration in the original PDF.

### Option B — .NET 9 bundler + browser loader (chosen)
- Summary: Retarget to `net9.0-browser` and declare the Vello bundle as deployed assets (`WasmDeployedFile`). Add a small loader (`vello_loader.js`) that initializes the wasm-bindgen module at startup and publishes exports on `globalThis.__vello`. Gradually replace `[LibraryImport]` with browser-only `[JSImport]` that call into the loader (or retain P/Invoke for native targets and use JS at browser).
- Pros:
  - Uses current .NET 9 WebAssembly bundling; assets show up reliably in the boot manifest.
  - Keeps Rust as-is (wasm-bindgen/web-sys), no emscripten/WASI detour.
  - Controlled migration: browser target uses JS; desktop/mobile stay on P/Invoke.
  - Maintains near-native perf; render work remains in Rust; one call per frame.
- Cons:
  - Source‑generated JS interop has constraints (no ref/out/uint/enums by default). Complex signatures require small, typed JS marshallers per function.
  - Still some JS in the loop (but contained and testable).

### Option C — Emscripten/WASI side module (not chosen)
- Summary: Recompile Rust to a dynamic side module (e.g., `wasm32-emscripten` with `SIDE_MODULE`), then `NativeFileReference` or similar to dlopen it via .NET.
- Pros:
  - Hypothetically lets .NET `DllImport` resolve WASM like a native dylib.
- Cons:
  - Incompatible with wgpu’s web backend (depends on wasm-bindgen + DOM APIs).
  - Side modules are not a supported interop story for .NET’s browser runtime.
  - Significant complexity with no clear perf upside vs Option B.

### Option D — Static link into `dotnet.native.wasm` (not pursued)
- Summary: Statically combine Rust object code into the .NET native wasm.
- Pros: Single module, zero dynamic load; potentially best startup.
- Cons: Not supported in the current .NET toolchain for arbitrary Rust crates; breaks modularity and dev iteration; would couple release cadence with .NET runtime.

### Option E — Direct `DllImport` to `vello_webgpu_ffi` wasm (not viable)
- Summary: Keep `[LibraryImport]` and rely on the runtime to dlopen the separate wasm bundle like a native library.
- Cons: The .NET browser runtime does not dynamically load wasm modules this way; not supported with wasm-bindgen-generated artifacts.

## Decision
- Adopt Option B.
  - Retarget sample to `net9.0-browser`.
  - Deploy the Vello wasm bundle (wasm + js + package.json + snippets) via `WasmDeployedFile`.
  - Initialize at startup with a tiny loader (`vello_loader.js`) and publish exports on `globalThis.__vello`.
  - Phase in browser‑only `[JSImport]` shims backed by small JS marshalling functions for signatures that are not directly supported by source‑generated JS interop. Keep P/Invoke for non‑browser targets.

## Rationale (Pros/Cons)
- Performance: Equivalent to the best practical alternative because render work stays in Rust; per‑frame submissions are one call. JS overhead is negligible with batching.
- Compatibility: Works with wgpu’s web backend and wasm-bindgen; no toolchain fork.
- Maintainability: Minimal Rust changes; contained JS surface; clear migration path.
- Risk: Small, centered on authoring JS marshallers for a handful of functions.

## Current Status (what’s in the repo)
- Sample retargeted to `net9.0-browser` and builds/publishes with the wasm bundle included.
- Loader added: `samples/AvaloniaVelloBrowserDemo/AvaloniaVelloBrowserDemo/wwwroot/native/vello_loader.js`.
- App startup awaits loader: `samples/AvaloniaVelloBrowserDemo/AvaloniaVelloBrowserDemo/wwwroot/main.js` imports and awaits `initVello()`.
- Managed fallback: Browser and non‑browser platforms now catch `DllNotFoundException`/`EntryPointNotFoundException`/`BadImageFormatException` and log a warning instead of crashing if the native library can’t be resolved.
- Next: Add targeted JS marshallers + `[JSImport]` wrappers for the hot path (Initialize/RequestAdapter/RequestDevice/FuturePoll/Surface configure+present/Renderer render), then turn on the WebGPU path in the browser renderer.

## How to Validate
- Build sample (browser): `dotnet build samples/AvaloniaVelloBrowserDemo/AvaloniaVelloBrowserDemo -c Debug`
- Publish sample (browser): `dotnet publish samples/AvaloniaVelloBrowserDemo/AvaloniaVelloBrowserDemo -c Debug -o pub-out`
- Confirm native assets under `pub-out/wwwroot/native` and `main.js` imports `native/vello_loader.js`.
- Run a static server pointing to `pub-out/wwwroot` and open in a recent Chromium.

## Future Work
- Implement and test JS marshalling shims for all FFI calls used by the browser swapchain path.
- Add capability logging and diagnostics routed from `__vello` back to Avalonia’s logger.
- CI: Bake wasm artifacts pack step and smoke test into the pipeline (Playwright script is already stubbed).
