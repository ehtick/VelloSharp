# Performance Baselines – VelloSharp Real-Time Charts

## Overview
Initial benchmarks capture CPU-side scene generation costs using the `poc_line_scene` prototype. Results were obtained on a reference workstation (AMD Ryzen 7950X, 64 GB RAM, RTX 3070, Windows 11) with Rust 1.86 nightly and `cargo run -p chart_benchmarks --release`.

```json
{
  "series_count": 4,
  "points_per_series": 1024,
  "iterations": 120,
  "total_ms": 41.28,
  "avg_ms": 0.34,
  "avg_cpu_frame_ms": 0.29,
  "encoded_paths": 4
}
```

## TreeDataGrid Hybrid Virtualization Baseline (Phase 0)
Prototype timings collected from the shared composition spike (`ffi/composition` preview) rendering a hierarchical grid sample (50k logical rows, depth 5, variable height templates) at 3840×2160. Measurements taken on the same reference workstation using `cargo run -p composition_bench --release --features tdg`.

```json
{
  "scenario": "tdg_virtual_grid",
  "visible_rows": 360,
  "avg_row_height_px": 42.3,
  "row_height_stddev_px": 11.8,
  "hierarchy_depth": 5,
  "column_span": 12,
  "frame_budget_ms": 8.0,
  "avg_cpu_frame_ms": 3.12,
  "avg_gpu_frame_ms": 3.56,
  "p99_frame_ms": 6.74,
  "input_latency_ms": 18.4,
  "cache_hit_ratio": 0.93
}
```

## Observations
- Scene generation remains below the 5 ms soft budget (0.29 ms average CPU per frame) leaving ample headroom for GPU upload and presentation.
- Encoded path counts scale linearly with the number of series; additional series increase the average cost by ~0.08 ms in exploratory runs (not shown).
- Deterministic seeding (`LineSceneConfig::seed`) ensures reproducible geometry for regression testing.
- TDG virtualization prototype sustains the 8 ms frame target (120 Hz) with 50k logical rows by keeping CPU + GPU combined under 6.7 ms at the 99th percentile.
- Cache hit ratio above 90% confirms row/column windowing effectiveness; remaining misses map to expansion bursts captured in telemetry.

## Shared Animation Timeline Microbenchmarks (Phase 3.5)
Rust benchmark (cargo run -p chart_benchmarks --release -- timeline):

```json
{
  "scenario": "timeline_10k_tracks",
  "track_count": 10000,
  "ticks": 480,
  "total_ms": 30.0109,
  "avg_tick_ms": 0.062480208333333336,
  "max_tick_ms": 0.2505,
  "samples_emitted": 4800000
}
```

.NET benchmark (dotnet run --project benchmarks/VelloSharp.Composition.Benchmarks --configuration Release):

```json
{
  "Scenario": "timeline_10k_tracks",
  "TrackCount": 10000,
  "Ticks": 480,
  "TotalMs": 6.501,
  "AvgTickMs": 0.013369583333333332,
  "MaxTickMs": 0.0622
}
```

Both runs keep the timeline sampling cost well below the ≤0.5 ms/frame target for 10k animated properties, confirming the shared runtime can power charting and TDG transitions without breaching the 8 ms frame budget.

## CI Performance Gates
- **Charts**: fail CI if `avg_cpu_frame_ms` exceeds 4.0 ms or `p99_frame_ms` exceeds 8.0 ms for the reference `chart_benchmarks` suite. Alerts published to the shared telemetry dashboard under `FrameStats`.
- **TreeDataGrid**: fail CI if `p99_frame_ms` exceeds 8.0 ms, `avg_gpu_frame_ms` exceeds 4.0 ms, or `cache_hit_ratio` drops below 0.90 for `tdg_virtual_grid`.
- Benchmark runners emit JSON artefacts to `artifacts/metrics/latest/` and surface pass/fail summaries in build logs for quick inspection.
- Thresholds are mirrored in the shared composition contract and reviewed during monthly performance audits.

## Next Steps
- Extend the benchmark harness with GPU timing instrumentation once the chart engine renderer is wired.
- Add stress scenarios covering burst inserts (200k points/sec) and decimation paths to validate Phase 0 targets.
- Automate publication of pass/fail summaries to the observability dashboard and align TDG dataset generators with nightly CI cadence.
