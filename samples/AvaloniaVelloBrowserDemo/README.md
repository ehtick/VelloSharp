# Avalonia Vello Browser Demo

This sample hosts Avalonia in the browser profile and drives rendering through
the Vello WebGPU pipeline exposed by `VelloSharp.Avalonia.Browser`. The view
displays live diagnostics (logical size, device pixel ratio, swapchain format)
as the canvas resizes and includes a GPU-heavy animated scene that exercises
the WebGPU renderer.

## Prerequisites

- .NET 8 SDK with the WebAssembly workload (`dotnet workload install wasm-tools`)
- Rust toolchain with `wasm32-unknown-unknown` target (`rustup target add wasm32-unknown-unknown`)
- `wasm-bindgen` CLI (`cargo install wasm-bindgen-cli`)
- (Optional) Binaryen `wasm-opt` for post-processing (`brew install binaryen` or equivalent)

## Build the WebGPU runtime

```pwsh
pwsh ./scripts/build-wasm-vello.ps1
```

The script compiles `vello_webgpu_ffi`, produces optimised WASM artefacts under
`artifacts/browser/native`, and mirrors the files into
`samples/AvaloniaVelloBrowserDemo/AvaloniaVelloBrowserDemo.Browser/wwwroot/native`
for the sample to consume.  The cross-platform `build-wasm-vello.sh` script
provides the same behaviour on macOS/Linux.

## Run the browser host

```pwsh
dotnet publish samples/AvaloniaVelloBrowserDemo/AvaloniaVelloBrowserDemo.Browser/AvaloniaVelloBrowserDemo.Browser.csproj `
    -c Release `
    -f net8.0-browserwasm `
    /p:RunAOTCompilation=false
```

Serve the generated `bin/Release/net8.0-browserwasm/publish/wwwroot` directory
with any static file server (for example `npx http-server`) and navigate to the
resulting URL.  When WebGPU initialises successfully the diagnostics panel will
show the resolved adapter and swapchain state; failures surface in the warnings
banner and the UI continues to render static content.

## Smoke verification

Use `scripts/verify-browser-webgpu.ps1` (or the `.sh` counterpart on Linux)
after publishing to run wasm-bindgen tests, validate the published native
assets, and execute a headless Playwright smoke test against the generated
bundle. A screenshot of the GPU scene is captured for inspection; if Node.js or
Playwright tooling is unavailable the smoke test step is skipped.
