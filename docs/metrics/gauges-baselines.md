# Performance Baselines – Gauge Prototypes

## Overview
- Prototype scenes built with `ffi/experimental/gauges_prototypes` validate that analog dial and vertical bargraph gauges stay within the shared 8 ms CPU frame budget.
- Measurements were captured on a reference workstation (AMD Ryzen 7950X, 64 GB RAM, RTX 3070, Windows 11, Rust 1.86 nightly) using `cargo run -p gauges_prototypes --release`.
- Each scenario renders 240 frames covering swept needle animation and stochastic bar fill updates.

```json
{
  "analog_dial": {
    "frames": 240,
    "avg_cpu_frame_ms": 0.009137083333333301,
    "p99_cpu_frame_ms": 0.0229,
    "max_cpu_frame_ms": 0.0665,
    "encoded_paths": 68
  },
  "vertical_bargraph": {
    "frames": 240,
    "avg_cpu_frame_ms": 0.001709166666666667,
    "p99_cpu_frame_ms": 0.0021,
    "max_cpu_frame_ms": 0.0062,
    "encoded_paths": 13
  }
}
```

## Observations
- Both prototypes remain well below the 8 ms frame target (analog dial averages 0.009 ms, bargraph 0.0017 ms) leaving headroom for integration overhead, input handling, and telemetry processing.
- Analog dial path count peaks at 68 stroked/filled primitives, confirming that polar geometry and alarm arc rendering are inexpensive with current composition scaffolding.
- Bargraph updates encode only 13 paths per frame; most cost stems from pointer math and randomised telemetry updates rather than scene emission.
- Needle sweep and alarm band layering illustrate that existing composition materials and panel primitives can handle industrial gauges without forking new rendering subsystems.

## Next Steps
- Integrate these scenarios into an automated perf harness alongside chart and TDG workloads.
- Extend prototypes with text labels and dynamic alarm banners to validate typography costs.
- Capture GPU timing once dedicated gauge render hooks land in `ffi/gauges-core`.
