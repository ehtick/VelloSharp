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

## Observations
- Scene generation remains below the 5 ms soft budget (0.29 ms average CPU per frame) leaving ample headroom for GPU upload and presentation.
- Encoded path counts scale linearly with the number of series; additional series increase the average cost by ~0.08 ms in exploratory runs (not shown).
- Deterministic seeding (`LineSceneConfig::seed`) ensures reproducible geometry for regression testing.

## Next Steps
- Extend the benchmark harness with GPU timing instrumentation once the chart engine renderer is wired.
- Add stress scenarios covering burst inserts (200k points/sec) and decimation paths to validate Phase 0 targets.
