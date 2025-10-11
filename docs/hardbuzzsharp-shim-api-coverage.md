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
| `Blob`, `ReleaseDelegate` | `bindings/VelloSharp.HarfBuzzSharp/Blob.cs` | - | Managed-only - pinned memory wrapper with optional release delegate; `FaceCount` fixed at 1 so multi-face collections still TODO. |
| `Tag` | `bindings/VelloSharp.HarfBuzzSharp/Tag.cs` | - | Managed-only - reproduces HarfBuzz tag parsing/formatting so feature and script tags survive round-tripping. |
| `Language` | `bindings/VelloSharp.HarfBuzzSharp/Language.cs` | - | Managed-only - normalises BCP-47 strings, defaulting to `und`; Buffer consumes it purely in managed code. |
| `Script` | `bindings/VelloSharp.HarfBuzzSharp/Script.cs` | - | Managed-only - wraps four-char tags and infers direction for a curated RTL set; scripts outside that list default to LTR today. |
| `GlyphInfo`, `GlyphPosition`, `GlyphExtents`, `FontExtents` | `bindings/VelloSharp.HarfBuzzSharp/GlyphStructs.cs` | - | Managed-only - lightweight structs mirroring HarfBuzz layouts so Buffer and Font can shuttle Vello shaper results. |
| Enums (`BufferFlags`, `SerializeFlag`, `OpenTypeMetricsTag`, etc.) | `bindings/VelloSharp.HarfBuzzSharp/Enums.cs` | - | Managed-only - exposes flag enums; Buffer currently ignores several flags (`RemoveDefaultIgnorables`, `GlyphFlags`, JSON formats) pending parity work. |

## Faces & Fonts

| HarfBuzzSharp API | Shim implementation | VelloSharp usage | Status / notes |
| --- | --- | --- | --- |
| `Face` | `bindings/VelloSharp.HarfBuzzSharp/Face.cs` | `VelloSharp.Font.Load`, `NativeMethods.vello_font_get_metrics` | Partial - loads blob data via Vello to compute units-per-EM and glyph count; `Tables` still throws `ShimNotImplemented` and `ReferenceTable` falls back to the full blob when no provider is supplied. |
| `Font` | `bindings/VelloSharp.HarfBuzzSharp/Font.cs` | `VelloSharp.Text.VelloTextShaperCore.ShapeUtf16`, `VelloSharp.Text.VelloTextShaperOptions`, `NativeMethods.vello_font_get_glyph_index`, `NativeMethods.vello_font_get_glyph_metrics` | Partial - shapes text with the Vello shaper and maps glyph indices/metrics; honours custom delegates for nominal/variation glyphs and horizontal advances, but kerning, origin, contour, and naming delegates are defined yet unused, and variation axis/vertical metrics plumbing is still missing. |
| `Feature` | `bindings/VelloSharp.HarfBuzzSharp/Feature.cs` | `VelloSharp.Text.VelloOpenTypeFeature` | Managed-only - simple value object that converts directly into Vello feature options; semantics match upstream HarfBuzzSharp. |
| `OpenTypeMetrics` | `bindings/VelloSharp.HarfBuzzSharp/OpenTypeMetrics.cs` | `NativeMethods.vello_font_get_metrics` | Partial - horizontal underline/strike metrics and ascender/descender derive from Vello; caret values and variation queries currently throw via `ShimNotImplemented`. |

## Buffer & Shaping Pipeline

| HarfBuzzSharp API | Shim implementation | VelloSharp usage | Status / notes |
| --- | --- | --- | --- |
| `Buffer` | `bindings/VelloSharp.HarfBuzzSharp/Buffer.cs` | `VelloSharp.Text.VelloTextShaperCore.ShapeUtf16`, `NativeMethods.vello_font_get_glyph_index`, `NativeMethods.vello_font_get_glyph_metrics` | Partial - handles UTF-8/16/32 ingestion, cluster reversal, glyph normalisation, and text serialisation; only text serialisation is supported today (`SerializeFormat.Json`/glyph-name flags ignored) and most `BufferFlags`/Unicode delegate hooks are placeholders. |

## Delegates & Unicode Services

| HarfBuzzSharp API | Shim implementation | VelloSharp usage | Status / notes |
| --- | --- | --- | --- |
| `FontFunctions` | `bindings/VelloSharp.HarfBuzzSharp/FontFunctions.cs` | - | Partial - stores custom delegate hooks and exposes immutability toggles, but destroy callbacks passed into setters are ignored and only the nominal/variation/horizontal metrics delegates are currently invoked by `Font`. |
| `UnicodeFunctions` | `bindings/VelloSharp.HarfBuzzSharp/UnicodeFunctions.cs` | - | Partial - retains user-supplied delegates; Buffer currently consults only `Script` for direction guessing while combining/mirroring/compose/decompose remain unused, and destroy callbacks are ignored. |
| `ShimNotImplemented` | `bindings/VelloSharp.HarfBuzzSharp/ShimNotImplemented.cs` | - | Managed-only - DEBUG-only guard that throws for incomplete APIs; presently triggered by `Face.Tables` and `OpenTypeMetrics.GetVariation*`. |
