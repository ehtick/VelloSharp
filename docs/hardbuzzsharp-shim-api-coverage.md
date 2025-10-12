# HarfBuzzSharp Shim API Coverage Matrix

This document enumerates every HarfBuzzSharp surface exposed by the `VelloSharp.HarfBuzzSharp` shim layer and outlines which VelloSharp primitives or services power that implementation. Each table row indicates the original HarfBuzzSharp type, the shim file that backs it, the VelloSharp APIs consumed internally, and the current coverage status.

Legend:

- **Complete** - behaviour matches the HarfBuzzSharp contract relied on by Avalonia and other current consumers.
- **Partial** - implemented subset; remaining gaps are called out in notes.
- **Managed-only** - helper that does not call into Vello, included for completeness.
- **Stub** - placeholder with explicit TODOs.

## Core Objects & Data

| HarfBuzzSharp API | Shim implementation | VelloSharp usage | Status / notes |
| --- | --- | --- | --- |
| `NativeObject` | `bindings/VelloSharp.HarfBuzzSharp/NativeObject.cs` | - | Managed-only - shared dispose pattern for shim handles; clears managed/Vello references without calling native code. |
| `Blob`, `ReleaseDelegate` | `bindings/VelloSharp.HarfBuzzSharp/Blob.cs` | `NativeMethods.vello_font_count_faces` | Managed-only - pinned memory wrapper with optional release delegate; now queries the Vello FFI for TTC face counts so multi-face collections surface every member. |
| `Tag` | `bindings/VelloSharp.HarfBuzzSharp/Tag.cs` | - | Managed-only - reproduces HarfBuzz tag parsing/formatting so feature and script tags survive round-tripping. |
| `Language` | `bindings/VelloSharp.HarfBuzzSharp/Language.cs` | - | Managed-only - normalises BCP-47 strings, defaulting to `und`; Buffer consumes it purely in managed code. |
| `Script` | `bindings/VelloSharp.HarfBuzzSharp/Script.cs` | - | Managed-only - wraps four-char tags and infers direction for a curated RTL set; scripts outside that list default to LTR today. |
| `GlyphInfo`, `GlyphPosition`, `GlyphExtents`, `FontExtents` | `bindings/VelloSharp.HarfBuzzSharp/GlyphStructs.cs` | - | Managed-only - lightweight structs mirroring HarfBuzz layouts so Buffer and Font can shuttle Vello shaper results. |
| Enums (`BufferFlags`, `SerializeFlag`, `OpenTypeMetricsTag`, etc.) | `bindings/VelloSharp.HarfBuzzSharp/Enums.cs` | - | Managed-only - exposes flag enums; Buffer now honours default-ignorable removal, mirroring, and combining-class delegates while glyph flag serialisation/JSON formats remain pending. |

## Faces & Fonts

| HarfBuzzSharp API | Shim implementation | VelloSharp usage | Status / notes |
| --- | --- | --- | --- |
| `Face` | `bindings/VelloSharp.HarfBuzzSharp/Face.cs` | `VelloSharp.Font.Load`, `NativeMethods.vello_font_get_metrics`, `NativeMethods.vello_font_get_table_tags`, `NativeMethods.vello_font_reference_table`, `NativeMethods.vello_font_table_data_destroy`, `NativeMethods.vello_font_get_variation_axes` | Partial - loads blob data via Vello, enumerates OpenType tables, wraps borrowed table slices in immutable blobs, and exposes variation axis metadata via the new FFI; provider-driven caches still need validation/diagnostics parity. |
| `Font` | `bindings/VelloSharp.HarfBuzzSharp/Font.cs` | `VelloSharp.Text.VelloTextShaperCore.ShapeUtf16`, `VelloSharp.Text.VelloTextShaperOptions`, `NativeMethods.vello_font_get_glyph_index`, `NativeMethods.vello_font_get_glyph_metrics`, `NativeMethods.vello_font_get_variation_axes` | Partial - shapes text with the Vello shaper, applies registered font-function delegates (glyph lookups, advances, kerning, origins, names), and forwards variation settings into shaping options; vertical metrics/contour fallbacks still rely on Vello defaults and MVAR-specific deltas remain pending. |
| `Feature` | `bindings/VelloSharp.HarfBuzzSharp/Feature.cs` | `VelloSharp.Text.VelloOpenTypeFeature` | Managed-only - simple value object that converts directly into Vello feature options; semantics match upstream HarfBuzzSharp. |
| `OpenTypeMetrics` | `bindings/VelloSharp.HarfBuzzSharp/OpenTypeMetrics.cs` | `NativeMethods.vello_font_get_ot_metric`, `NativeMethods.vello_font_get_ot_variation*` | Complete - caret metrics and variation deltas surface through dedicated FFI; parity validated against reference HarfBuzz tests. |

## Buffer & Shaping Pipeline

| HarfBuzzSharp API | Shim implementation | VelloSharp usage | Status / notes |
| --- | --- | --- | --- |
| `Buffer` | `bindings/VelloSharp.HarfBuzzSharp/Buffer.cs` | `VelloSharp.Text.VelloTextShaperCore.ShapeUtf16`, `NativeMethods.vello_font_get_glyph_index`, `NativeMethods.vello_font_get_glyph_metrics` | Partial - handles UTF-8/16/32 ingestion, cluster reversal, glyph normalisation, text serialisation (text/JSON formats), and honours default-ignorable removal, mirroring, and combining-class reordering via Unicode delegates. `NormalizeGlyphs`/`ReverseClusters` now mirror HarfBuzz semantics, with parity verified in `BufferClusterNormalizationTests`. Remaining gaps include JSON glyph-name parity and richer debug dumping (eg, extents round-tripping). |

## Delegates & Unicode Services

| HarfBuzzSharp API | Shim implementation | VelloSharp usage | Status / notes |
| --- | --- | --- | --- |
| `FontFunctions` | `bindings/VelloSharp.HarfBuzzSharp/FontFunctions.cs` | - | Partial - stores custom delegate hooks, enforces immutability, and runs destroy callbacks; `Font` consults glyph lookup/advance/extents, kerning, naming, and contour delegates before deferring to Vello. Vertical metrics/origin fallbacks and broader multi-glyph pathways still need parity coverage. |
| `UnicodeFunctions` | `bindings/VelloSharp.HarfBuzzSharp/UnicodeFunctions.cs` | - | Partial - retains user-supplied delegates and respects destroy callbacks; `Buffer` consumes combining class, mirroring, script, and general-category hooks, while compose/decompose remain unused pending canonical decomposition support. |
| `ShimNotImplemented` | `bindings/VelloSharp.HarfBuzzSharp/ShimNotImplemented.cs` | - | Managed-only - DEBUG-only guard reserved for remaining gaps; no longer used by `OpenTypeMetrics`. |

## Next Steps Toward Full Coverage

- **Face (`bindings/VelloSharp.HarfBuzzSharp/Face.cs`)** - Add diagnostics/tests around variation axis metadata and TTC-aware providers, ensuring cached lists stay in sync with user-supplied tables.
- **Font (`bindings/VelloSharp.HarfBuzzSharp/Font.cs`)** – ✅ delegate pathways now fall back to Vello metrics when custom hooks return `false`, with regression tests covering kerning/extents/origin plumbing. Outstanding work: vertical metrics/origin fallbacks and broader multi-glyph delegate pathways.
- **OpenTypeMetrics (`bindings/VelloSharp.HarfBuzzSharp/OpenTypeMetrics.cs`)** – ✅ caret and variation metrics now flow through dedicated FFI, with parity assertions captured in `tests/VelloTextParityTests`.
- **Buffer (`bindings/VelloSharp.HarfBuzzSharp/Buffer.cs`)** – Support glyph-name to ID mapping on JSON/text deserialisation, surface glyph extents for round-tripped serialised data, and extend debug dumps to include cluster spans.
- **FontFunctions & UnicodeFunctions (`bindings/VelloSharp.HarfBuzzSharp/FontFunctions.cs`, `UnicodeFunctions.cs`)** - Explore compose/decompose integration, plumb remaining vertical/origin fallbacks through Vello, and validate canonical decomposition paths.
- **Documentation & Diagnostics** - Regenerate this matrix after each milestone, expand DEBUG-time instrumentation helpers for glyph span inspection, and capture logs from Avalonia integration runs to track behavioural deltas.

## Implementation Notes

- `Face.ReferenceTable` returns immutable `Blob` wrappers around borrowed Vello slices; disposing those blobs releases the underlying handle via `vello_font_table_data_destroy`, so consumers must observe blob lifetime semantics just like in upstream HarfBuzz.
- `NativeMethods.vello_font_get_variation_axes` now materialises fvar metadata through Skrifa, giving Parley and other consumers the same axis ranges exposed by HarfBuzzSharp.
- `cargo test -p vello_ffi` now exercises the HarfBuzz bridge: glyph index/metrics lookups, OT metric scaling, variation deltas, and table enumeration are validated against Skrifa for Roboto and Vazirmatn test fonts.
- `BufferClusterNormalizationTests` compares the shim’s cluster normalisation and reversal behaviour against reference HarfBuzz outputs for Latin combining-mark scenarios.
- Avalonia integration console output is archived under `docs/logs/avalonia-integration/` for regression comparisons (latest capture: `2025-10-12T09-42-33-vello-integration.log`).
