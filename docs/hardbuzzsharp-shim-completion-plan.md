# VelloSharp HarfBuzz Shim Completion Plan

## Vision and Goals
- Deliver a **drop-in replacement** for `HarfBuzzSharp` so Avalonia, Win2D and downstream tooling can swap to Vello without behavioural regressions.
- Reach **100% API surface coverage** while keeping DEBUG-time guard rails (`ShimNotImplemented.Throw`) for newly exposed gaps.
- Build confidence with shaping parity tests, OT-table probes, and integration projects that exercise both managed and native call paths.

## Implementation Conventions
- [x] All new text shaping primitives land in `ffi/vello_ffi` (or a dedicated HarfBuzz bridge crate) with unit coverage (`cargo test`).
- [x] Managed shims continue to live under `bindings/VelloSharp.HarfBuzzSharp`, relying on `VelloSharp.Text.VelloTextShaperCore` or new FFI glue.
- [x] Every completed feature updates `docs/hardbuzzsharp-shim-api-coverage.md` and adds assertions under `tests/VelloTextParityTests`.
- [x] Debug builds must throw via `ShimNotImplemented.Throw`, release builds must no-op safely.

## Phase 0 – API Surface Parity & Diagnostics (1 sprint)
**Objectives**
- Ensure every public type/member from `HarfBuzzSharp` is represented in the shim (even if stubbed) and wire logging/guards that make gaps obvious during development.

**Deliverables**
- [x] Expand enum coverage (`OpenTypeColorPaletteFlags`, OT math/meta tags, Unicode categories) and regenerate `docs/hardbuzzsharp-shim-api-coverage.md`.
- [x] Add `Buffer` instrumentation helpers (dump glyph spans, cluster info) gated behind `DEBUG` for quick diagnosis in integration tests.
- [x] Introduce `tests/HarfBuzzShimSmokeTests` that exercise creation of `Blob`, `Face`, `Font`, `Buffer`, and end-to-end shaping with Latin + RTL samples.
- [x] Capture current behaviour in the Avalonia integration project and archive the logs for later regression comparison (`docs/logs/avalonia-integration/2025-10-12T09-42-33-vello-integration.log`).

## Phase 1 – Delegate & Unicode Hook Integration (1–2 sprints)
**Objectives**
- Honour the delegate-based extensibility (`FontFunctions`, `UnicodeFunctions`) so custom glyph metrics or Unicode overrides flow through the shim.

**Deliverables**
- [x] Implement delegate invocation plumbing inside `Font` for glyph lookups, extents, kerning, and naming – fall back to Vello FFI when delegates return `false`.
- [x] Wire `UnicodeFunctions` into `Buffer.GuessSegmentProperties` and `Font.Shape` so bespoke script/combining-class data affects shaping.
- [x] Extend `Buffer.NormalizeGlyphs` and `ReverseClusters` using delegate-provided cluster info; add regression tests covering mark reordering.
- [x] Update coverage doc entries for delegates from “Stub” to “Partial/Complete” and add scenario tests that mimic HarfBuzz sample callbacks.

## Phase 2 – OpenType Tables, Serialisation, & Variation Support (2 sprints)
**Objectives**
- Expose the richer OpenType data (`hb_ot_metrics`, buffer serialisation, variation axes) required by advanced typography pipelines.

**Deliverables**
- [x] Add FFI exposure for OT metrics variations and table enumeration; update `OpenTypeMetrics.GetVariation`/`GetXVariation`/`GetYVariation`.
- [x] Implement `Buffer.SerializeGlyphs` / `DeserializeGlyphs` and glyph flag propagation so debug tooling can inspect shaping output.
- [x] Surface `Face.Tables`, `Face.ReferenceTable`, and table-provider callbacks via new Rust FFI that returns slices safely.
- [x] Create parity tests comparing the shim’s serialised output to the reference HarfBuzzSharp for a corpus of fonts (Latin, CJK, complex scripts).

## Phase 3 – Release Readiness & Automation (ongoing)
**Objectives**
- Harden the shim for production CI consumption and publish coverage metrics continuously.

**Deliverables**
- [ ] Add a `harfbuzz-shim` lane in CI that runs the parity suite on Windows, macOS, and Linux.
- [ ] Track coverage deltas in `docs/hardbuzzsharp-shim-api-coverage.md` and surface them in PR templates.
- [ ] Produce a migration guide for downstream consumers, highlighting behavioural differences and new debug guardrails.

