# Performance Baselines – SCADA Dashboard Prototype

## Overview
- The `ffi/experimental/scada_dashboard` prototype composes a trend chart, analog gauge, bargraph, and alarm grid using shared composition primitives.
- Metrics captured on the reference workstation (AMD Ryzen 7950X, 64 GB RAM, RTX 3070, Windows 11, Rust 1.86 nightly) with `cargo run -p scada_dashboard --release`.
- 240 frames simulate mixed telemetry updates (needle sweep, bar jitter, chart reseeding, rotating alarm highlight) to validate sub-8 ms budgets.

```json
{
  "scada_dashboard": {
    "frames": 240,
    "avg_cpu_frame_ms": 0.32327333333333336,
    "p99_cpu_frame_ms": 0.8104,
    "max_cpu_frame_ms": 0.8495,
    "encoded_paths": 100
  }
}
```

## Observations
- CPU frame cost stays well under the 8 ms target (0.32 ms average, 0.81 ms p99), leaving ample headroom for live telemetry ingestion and historian playback.
- Scene complexity (≈100 encoded paths) demonstrates that combining chart, gauge, and TDG surfaces does not regress composition or material registries.
- Grid panel highlighting and gauge animations confirm that shared `TelemetryHub`-style value cycling can drive multiple widgets without redundant scene allocations.
- Prototype uses only shared primitives (panels, rectangles, strokes), validating reuse of the composition toolkit across SCADA dashboards.

## Next Steps
- Integrate historian scrubber simulation to measure playback overhead.
- Extend prototype with annunciator command buttons wired through `CommandBroker`.
- Capture GPU timings once platform hosts are available to ensure end-to-end latency budgets.
