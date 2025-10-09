# Performance Baselines – Editor Canvas Prototype

## Overview
- The `ffi/experimental/editor_canvas_prototype` simulates drag/drop operations in the unified visual editor using shared chart, gauge, and panel primitives.
- Measured on reference workstation (AMD Ryzen 7950X, 64 GB RAM, RTX 3070, Windows 11, Rust 1.86 nightly) running `cargo run -p editor_canvas_prototype --release`.
- Scenario performs 24 sequential drop operations, rebuilding the scene each time to validate <16 ms interactive budgets.

```json
{
  "editor_canvas": {
    "operations": 24,
    "avg_cpu_frame_ms": 0.11734583333333337,
    "p99_cpu_frame_ms": 0.1975,
    "max_cpu_frame_ms": 0.1975,
    "encoded_paths": 542
  }
}
```

## Observations
- Scene rebuild cost remains well under the 16 ms target for editor interactions (0.12 ms average, 0.20 ms p99) leaving headroom for UI chrome and telemetry preview.
- 542 encoded paths demonstrate palette items (chart, gauges, panels) compose without stressing the scene cache or material registries.
- Sequential operations confirm deterministic performance as the operation count increases, supporting undo/redo expectations.

## Next Steps
- Integrate historian preview simulation to capture binding inspector overhead.
- Add selection marquee and group operations to the prototype to cover multi-select scenarios.
- Fold metrics into the shared performance gates once editor engine crates mature.
