# Platform Interop Requirements

## Goals
- Provide a consistent hosting contract for Vello-backed chart surfaces across WPF, WinUI, MAUI, Avalonia, Uno, and WinForms.
- Guarantee deterministic lifecycle management of GPU resources irrespective of UI framework threading models.
- Enable headless rendering scenarios for CI validation and server-side image generation.

## Surface Hosting Contract
| Concern | Requirement |
| --- | --- |
| Surface Attachment | Implement `IVelloChartSurface.AttachSurface(surfaceHandle)` to bind the GPU surface/texture provided by the host framework. |
| Resize | `Resize(LogicalSize size, DpiScale dpi)` must be invoked on layout changes before the next render pass. |
| Render | Hosts call `RenderFrame(FrameTime time)` from UI or dedicated render loops; method returns frame statistics (`FrameStats`). |
| Detach | `DetachSurface()` releases GPU handles and invalidates outstanding command buffers. |

## Dependency Injection
- Engine services accept `ITimeProvider`, `ITelemetrySink`, and `IInputRouter` via constructor injection (no global singletons).
- Platform adapters map native constructs (e.g., `Dispatcher`, `CoreDispatcher`, `SynchronizationContext`) onto shared abstractions consumed by the engine.

## Headless Mode
- All rendering routines must support off-screen surfaces (wgpu headless backend or D3D texture) to enable automated regression tests.
- CLI tooling should expose `--headless` flag wiring `IVelloChartSurface` to an in-memory target with optional PNG export.

## Threading
- Rendering work is scheduled on a dedicated engine thread; adapters marshal UI thread events through the `IInputRouter`.
- Blocking operations on UI threads are prohibited; async channels are used for data ingress to the chart engine.
