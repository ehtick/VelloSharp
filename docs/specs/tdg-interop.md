# TreeDataGrid Interop Contract

## Goals
- Provide a unified bridge between host frameworks (Avalonia, WinUI, WPF, MAUI, Uno) and the native TreeDataGrid (TDG) rendering/runtime implemented on Vello.
- Support hybrid virtualization flows: the host owns scroll input and viewport metrics; the native engine owns layout, composition, and cell realization lifetimes.
- Ensure deterministic 120 Hz frame pacing with explicit back-pressure signals when hosts saturate input queues.
- Keep the contract symmetric with the existing chart surface APIs so controls can share scheduling, diagnostics, and resource pools.

## Surface Hosting
```csharp
public interface ITdgSurfaceHost
{
    void AttachSurface(VelloSurfaceDescriptor descriptor, TdgSurfaceCapabilities caps);
    void Resize(ViewMetrics metrics, DpiScale dpi);
    void Present(TdgFrameToken token);
    void DetachSurface();
}
```
- `TdgSurfaceCapabilities` advertises color space, swap chain mode (single/double buffered), and optional off-screen (headless) support.
- `ViewMetrics` carries logical viewport size, scroll offsets (horizontal/vertical), and overscan margins used for predictive virtualization.
- `Present` is invoked by the TDG runtime after issuing a command buffer for the pending frame; hosts are responsible for swapping the surface to the UI thread where required.

## Input Routing
```csharp
public interface ITdgInputRouter
{
    void Enqueue(TdgPointerEvent @event);
    void Enqueue(TdgKeyboardEvent @event);
    void Enqueue(TdgAutomationEvent @event);
}
```
- Pointer events are normalized to physical pixels with high-resolution delta support for precision scrolling.
- Keyboard events expose logical navigation commands (Up/Down, Expand/Collapse, CommitEdit) in addition to raw scan codes to reduce host branching.
- Automation events propagate accessibility focus changes, announce updates for screen readers, and translate platform-specific automation patterns into TDG semantic actions.
- Input queue is lock-free; hosts can drop events if watermark thresholds are crossed to preserve latency (reported via diagnostics).

## Data Virtualization Bridge
```csharp
public interface ITdgDataAdapter
{
    ValueTask<TdgNodeBatch> FetchAsync(TdgFetchRequest request, CancellationToken token);
    ValueTask PrefetchAsync(TdgPrefetchHint hint, CancellationToken token);
    void Invalidate(TdgInvalidationScope scope);
}
```
- Fetch requests specify hierarchical ranges (node path + child window), column slices, and requested cell metadata (templates, styles).
- Batches return a contiguous block of realized nodes including hierarchy metadata, row height hints, and column binding payloads.
- Prefetch hints include scroll velocity vectors enabling the adapter to warm upcoming branches.
- Invalidations allow hosts to notify the runtime of data source changes without tearing down the scene.

## Animation Configuration
- `TreeVirtualizationScheduler.ConfigureRowAnimations(TreeRowAnimationProfile? profile)` lets hosts tune expand/collapse micro-interactions, row glow, and caret motion through managed presets (duration, easing, reduced-motion toggles). Passing `null` restores the framework defaults.
- `TreeRowAnimationProfile` exposes `TreeAnimationTimeline` and `TreeSpringAnimationTrack` descriptors so XAML and code-behind can align TreeDataGrid motion with other VelloSharp surfaces while honouring platform accessibility preferences.

## Diagnostics and Telemetry
```csharp
public interface ITdgTelemetrySink
{
    void Record(in TdgFrameStats stats);
    void Record(in TdgVirtualizationStats stats);
    void Record(in TdgInputBackPressureEvent evt);
}
```
- `TdgFrameStats` aggregates CPU/GPU timings, budget utilisation, cache hit/miss ratios, and command buffer reuse counters.
- `TdgVirtualizationStats` reports window size, realized node counts, eviction events, and prefetch latency.
- Back-pressure events highlight scenarios where input or data adapters fell behind, including recommended mitigation actions (drop, coalesce, extend frame budget).

### Native instrumentation surface
- `vello_tdg_virtualizer_telemetry` exposes per-frame row window lengths, buffer reuse/adopt/allocate counts, pool sizes, and eviction totals so managed telemetry sinks can reason about churn alongside virtualization plans.
- `vello_tdg_renderer_record_gpu_summary` ingests aggregated Vello GPU profiler data (total GPU time, queue time, sample count) and feeds those numbers into the frame stats returned by `vello_tdg_renderer_end_frame`.
- GPU timestamp summaries are written on the render thread **before** `EndFrame` is called; the engine will prefer the submitted summary over ad-hoc timings supplied via the legacy parameters.

## FFI Ownership and Threading Guarantees
- **TreeDataModel**: thread-affine to the engine's data thread. Mutation APIs (`Attach*`, `SetExpanded`, `SetSelected`) may not be invoked concurrently; read APIs (`Drain*`, `GetMetadata`) rely on caller draining diffs before resubmitting work. Callers must dispose the handle once the managed wrapper leaves scope.
- **HybridVirtualizer / TreeVirtualizationScheduler**: thread-affine to the render scheduler. Plans are snapshot-based; after calling `vello_tdg_virtualizer_plan`, hosts should copy row/recycle plans, read telemetry via `vello_tdg_virtualizer_telemetry`, then render. Handles are reusable across frames but not sharable across threads.
- **TreeSceneGraph**: each scene node is owned by the virtualization harness. `Encode*`/`MarkRowDirty` calls must happen on the render thread leading into Vello encoding. Dirty regions are drained via `TakeDirty` prior to presenting.
- **TreeRenderLoop**: single-threaded render pacing primitive. Hosts enqueue GPU summaries, call `BeginFrame`, render, optionally send GPU profiler aggregates, then call `EndFrame` to produce metrics. Handles are reference-counted but should be disposed only after frames stop.
- All FFI entry points zero out the per-thread error buffer before running; if a call fails the managed wrapper will surface the message exposed by `vello_tdg_last_error_message`.

## Threading Model
- Surface attachment/detachment and resize callbacks happen on the UI thread.
- Rendering and data fetch operations execute on dedicated engine threads owned by the TDG runtime.
- Input events may originate from UI threads or background platform channels; the runtime enqueues them for deterministic processing aligned to frame ticks.
- Host frameworks must avoid blocking the render callback; long-running work should occur via `ITdgDataAdapter`.

## Lifecycle
1. Host instantiates managed TDG wrapper and injects `ITdgSurfaceHost`, `ITdgInputRouter`, and data/telemetry adapters.
2. Wrapper requests a surface from the native runtime; once attached, it issues continuous render ticks when the viewport or data changes.
3. Host updates `ViewMetrics` on scroll or resize; the runtime evaluates virtualization windows and schedules data fetches.
4. Upon disposal, the runtime flushes outstanding fetches, releases GPU resources, and signals `DetachSurface`.

## Versioning
- Contracts follow semantic versioning aligned with the `VelloSharp.Composition` crate.
- Breaking changes require bumping the major version and updating compatibility matrices for each host framework.
- Feature probes (`TdgSurfaceCapabilities.Features`) allow hosts to negotiate optional behaviours (e.g., IME composition overlays, custom shader hooks) without breaking older runtimes.
