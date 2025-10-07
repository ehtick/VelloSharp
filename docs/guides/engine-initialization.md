# Engine Initialization Guide

This guide captures the initialization flow for the native-backed chart engine and the responsibilities owned by the managed host. The APIs stabilised in this iteration are designed for low-latency, double-buffered rendering with predictable GPU resource reuse.

## Prerequisites

- Copy the `vello_chart_engine` native library into the target application's `runtimes/<rid>/native` directory. The build scripts (`scripts/build-native-*.{ps1,sh}`) produce the binaries and place them under `artifacts/runtimes/`.
- Reference the following projects from your host application:
  - `VelloSharp` (core bindings)
  - `VelloSharp.ChartEngine`
  - `VelloSharp.ChartDiagnostics`
  - `VelloSharp.ChartRuntime`
- Ensure `AllowUnsafeBlocks` is enabled for assemblies that call the FFI surface (already set for `VelloSharp.ChartEngine`).

## Initialization Flow

1. **Instantiate the engine**  
   ```csharp
   var engine = new ChartEngine.ChartEngine(new ChartEngineOptions
   {
       VisibleDuration = TimeSpan.FromMinutes(2),
       VerticalPaddingRatio = 0.08,
       FrameBudget = TimeSpan.FromMilliseconds(8),
        StrokeWidth = 1.5,
        ShowAxes = true, // set to false to render data-only views
        Palette = new[]
        {
            ChartColor.FromRgb(0x3A, 0xB8, 0xFF),
            ChartColor.FromRgb(0xF4, 0x5E, 0x8C),
            ChartColor.FromRgb(0x81, 0xFF, 0xF9),
            ChartColor.FromRgb(0xFF, 0xD1, 0x4F),
        },
   });
   ```
   The constructor forwards options to the Rust engine and allocates the double-buffered scene pool.
   `StrokeWidth` controls the base line thickness for all series, `Palette` overrides the default color ramp, and `ShowAxes` toggles the time/value axes and grid rendering.

2. **Register render scheduling**  
   The `RenderScheduler` remains in managed code to integrate with UI frameworks:
   ```csharp
   engine.ScheduleRender(tick =>
   {
       // Typically call RequestRender on the hosting view
   });
   ```

3. **Publish streaming samples**  
   Call `PumpData` with time-series spans. Samples are forwarded to the native data pipeline using a stack-allocated fast path for small bursts.
   ```csharp
   engine.PumpData(samples); // samples is ReadOnlySpan<ChartSamplePoint>
   ```

4. **Render into the provided scene**  
   When the host view requests a frame, call `Render` with the scene supplied by `VelloView`.
   ```csharp
   engine.Render(context.Scene, context.Width, context.Height);
   ```
   The native engine writes into its double-buffered `Scene` objects and appends the active buffer into the managed scene handle.

5. **Consume diagnostics**  
   `FrameDiagnosticsCollector` is updated with each frame. You can query `engine.LastFrameStats` or subscribe to the recorded histograms for telemetry.

## Runtime Styling Overrides

- Update the palette without tearing down the engine:
  ```csharp
  // Passing Span<T> lets you reuse buffers or slice existing theme data
  engine.UpdatePalette(stackalloc[]
  {
      ChartColor.FromRgb(0x2F, 0x95, 0xFF),
      ChartColor.FromRgb(0xF9, 0x65, 0x7D),
      ChartColor.FromRgb(0x4E, 0xC9, 0x74),
  });

  // Revert to the built-in palette
  engine.UpdatePalette(ReadOnlySpan<ChartColor>.Empty);
  ```
- Apply per-series overrides for labels, thickness, and colors without recreating the engine:
  ```csharp
  Span<ChartSeriesOverride> overrides = stackalloc[]
  {
      new ChartSeriesOverride(seriesId: 1)
          .WithLabel("Bid")
          .WithStrokeWidth(2.0)
          .WithColor(ChartColor.FromRgb(0x3A, 0xB8, 0xFF)),
      new ChartSeriesOverride(seriesId: 2)
          .WithLabel("Ask")
          .ClearStrokeWidth()
          .WithColor(ChartColor.FromRgb(0xF4, 0x5E, 0x8C)),
  };

  engine.ApplySeriesOverrides(overrides);
  ```
  Overrides are tri-state: call `Clear*` helpers to remove an override and fall back to the shared defaults.

## Configuring Series Definitions

- Declare the rendering behaviour for each series up front. Definitions drive legend markers, overlay styling, and how the native engine builds GPU buffers.
  ```csharp
  engine.ConfigureSeries(new ChartSeriesDefinition[]
  {
      new LineSeriesDefinition(0)
      {
          StrokeWidth = 2.0,
          FillOpacity = 0.18,
      },
      new ScatterSeriesDefinition(1)
      {
          MarkerSize = 6.0,
      },
      new BarSeriesDefinition(2)
      {
          Baseline = 0,
          BarWidthSeconds = 6.0,
      },
  });
  ```
- Series that are not explicitly configured fall back to the line rendering defaults.

## GPU Surface Hosting

- `ChartView` now composes `VelloSurfaceView`, so GPU swapchains are attached automatically when supported by the platform window handle.
- Surface resizing and presentation are handled by the shared host; the control transparently falls back to the bitmap path when hardware acceleration is unavailable.
- Surface tuning remains accessible via the regular properties:
  ```csharp
  ChartHost.RendererOptions = ChartHost.RendererOptions with { BaseDpi = new Vector2(96f, 96f) };
  ChartHost.RenderParameters = ChartHost.RenderParameters with { BaseColor = RgbaColor.FromBytes(0x10, 0x15, 0x1F) };
  ```

## Axes and Grid

- The engine now computes a shared viewport for all series and renders labelled time/value axes with adaptive tick spacing.
- Grid lines inherit those ticks, giving trading dashboards quick alignment cues without extra overlay code.
- Axes consume dynamic padding around the plot. When you set `ShowAxes = false`, the chart expands to the full control bounds for sparkline layouts.

## Diagnostics and Tracing

- The Rust engine emits `tracing` events which are bridged to `.NET` via `ChartEngineEventSource`. Consumers can listen with ETW/EventSource listeners or `EventListener`.
- The trace pipeline is best-effort; if the callback registration fails the engine continues without raising exceptions.

## Renderer Pooling

The Avalonia host (`ChartView`) relies on `VelloSurfaceView` for GPU presentation. The Rust engine reuses internal `Scene` buffers, and the managed layer keeps the `VelloSurfaceView` renderer pool hot, ensuring command buffers and textures are recycled every frame.

## Disposal Semantics

Always dispose the engine from the UI thread that owns the scheduler:
```csharp
engine.Dispose();
```
Disposal tears down the scheduler, diagnostics collectors, and the native handle in one pass.

## Next Steps

- Extend the EventSource listener to forward metrics into application-level telemetry.
- Combine runtime palette updates with per-series overrides to drive live theming or interaction-driven callouts.
- Incorporate structured tracing IDs once additional spans are stabilised on the Rust side.
