# Industrial Gauge Requirements

## Accuracy and Resolution
- Gauges must render value pointers with at most 0.25 degree angular error for circular scales and 0.5 pixel error for linear scales within a 300 DPI reference surface.
- Support configurable input ranges (linear and logarithmic) with deterministic snapping rules; value mapping must retain double precision internally.
- Provide calibration offsets per gauge instance with persistence hooks so operators can apply zero/span adjustments without code changes.
- Expose optional deadband and hysteresis settings for jittery signals; defaults align with control room expectations (0.5 percent deadband).

## Rendering Quality
- Enable multi sample anti aliasing for all gauge chrome and needles when GPU supports it; fall back to analytic antialiasing otherwise and document the mode in diagnostics overlays.
- Ensure text labels pass WCAG AA contrast at minimum; prefer AAA for primary readouts.
- Provide tunable shadow/lighting layers via material registry, keeping GPU fragment counts within the shared 8 ms budget.
- Support reduced motion configuration that disables sweep animations while keeping value updates deterministic.

## Alarm Lamination and Annunciation
- Visual states must layer base gauge, warning band, alarm overlay, and acknowledgement banner without reordering scene nodes; layering order is defined in `docs/specs/shared-composition-contract.md`.
- Alarm bands adhere to ISA 5.5: High High (magenta), High (red), Low (yellow), Low Low (blue) with minimum 6:1 contrast against background.
- Provide flashing cadence guidelines (1 Hz default, 50 percent duty cycle) with override for jurisdictions requiring different timing.
- Require command bindings for acknowledge, shelve, and reset actions via `CommandBroker`; UI affordances must reflect command result states.

## Colour and Theme Standards
- Ship default palette aligned to IEC 60073 and ISA 101.00.01; include JSON palette descriptor for downstream tooling.
- Maintain light/dark theme variants with confirmed contrast ratios; all colours must be customizable through shared material registry keys.
- Allow facility-specific overrides while preserving minimum contrast and alarm hues through validation helpers.

## Telemetry and Data Contracts
- Gauges consume `TelemetrySample` values via `GaugeTelemetryConnector`; telemetry quality maps to value styling (Good = solid, Uncertain = dashed, Bad = muted with alarm banner).
- Support last-value hold for intermittent signals with configurable timeout before entering stale state and raising alarm indicator.
- Command requests for setpoint changes, mode toggles, and acknowledgements route through `CommandBroker` using `TargetId` bound to gauge instance id.
- Record per-gauge `FrameStats` including update cadence, last telemetry timestamp, and command latency for diagnostics.

## Accessibility and Ergonomics
- All gauge interactions must expose keyboard and screen reader affordances; radial gauges expose logical `RangeValue` semantics, bargraphs expose `Progress` and `Value` patterns.
- Provide adjustable font scaling up to 200 percent without clipping critical text or values.
- Ensure pointer targets for knobs and buttons meet minimum 44 by 44 pixel guideline; include focus outlines and indicator feedback.

## Environmental Considerations
- Rendering pipeline must tolerate telemetry bursts of 10 updates per gauge per frame without dropping frames or stalling input.
- Offline buffering: retain last 10 seconds of values for each gauge to support annunciation after brief disconnects.
- Document hardware requirements and fallback behaviour for thin clients (e.g., CPU rasterization path with relaxed animation budget).

## Compliance Checklist
- [ ] Accuracy tolerances validated through golden images or analytical comparison.
- [ ] Anti aliasing mode recorded and surfaced in diagnostics overlay.
- [ ] Alarm colour palette verified against ISA/IEC references.
- [ ] Telemetry stale-state timeout configured and tested.
- [ ] Accessibility audit completed (screen reader, keyboard, high contrast, reduced motion).
- [ ] Environmental resilience (burst telemetry, offline buffer) benchmarked and documented.
