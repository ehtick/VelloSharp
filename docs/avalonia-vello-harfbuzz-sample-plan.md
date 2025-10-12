# Avalonia Vello HarfBuzz Sample Plan

## Vision and Goals
- Ship a dedicated `samples/AvaloniaVelloHarfBuzzSample` gallery that exercises every public surface in `VelloSharp.HarfBuzzSharp`, proving the shim is production-ready.
- Pair each HarfBuzz concept with an interactive Vello 2D scene that renders shaped text via the API lease pipeline, covering Latin, CJK, RTL, emoji, and custom delegate scenarios.
- Maintain 100% API coverage by mapping every class and key member to at least one page, mirroring the matrix tracked in `docs/hardbuzzsharp-shim-api-coverage.md`.
- Document the implementation path so downstream contributors can extend or validate the sample without reverse engineering.

## Architecture & Conventions
- Reuse `AvaloniaVelloCommon` for shared assets, binding helpers, and the `IVelloApiLeaseFeature` integration pattern; keep HarfBuzz-specific logic inside the new sample project.
- Expose each demo as a view model + view pair: view models own HarfBuzz state (`Face`, `Font`, `Buffer`, delegates), views host a lease-backed drawing surface and UI controls.
- Centralise font loading, buffer lifecycle management, and Vello glyph tessellation in shared services under `samples/AvaloniaVelloHarfBuzzSample/Rendering`.
- All shaping passes must flow through the shim types (`using HarfBuzzSharp;` with `VelloSharp.HarfBuzzSharp` assembly) to guarantee parity testing.
- Capture serialised buffer dumps, glyph geometry, and timing diagnostics in a `/Diagnostics` panel for developer debugging; persist snapshots under `artifacts/samples/harfbuzz`.

## Dependencies & Assets
- Fonts: reuse Roboto, Inconsolata, Noto Emoji COLR/CBTF, Noto Sans CJK, and Amiri (RTL) from `samples/AvaloniaVelloExamples/Assets`; add missing assets under `samples/AvaloniaVelloHarfBuzzSample/Assets/fonts`.
- Text corpora: add JSON snippets for pangrams, multi-script samples, feature toggles (e.g., discretionary ligatures), and shape permutations under `Assets/data`.
- HarfBuzz shim: reference `bindings/VelloSharp.HarfBuzzSharp` and ensure build outputs copy to the sample’s `bin`.
- Tooling: depend on `AvaloniaVelloCommon`, `MiniMvvm`, and `CommunityToolkit.Diagnostics` (if additional guard clauses are needed).

## Phase 0 – Project Skeleton & Shared Infrastructure
**Objectives**
- Stand up the Avalonia application shell, navigation, and shared services required by all pages.

**Deliverables**
- [ ] Create `samples/AvaloniaVelloHarfBuzzSample/AvaloniaVelloHarfBuzzSample.csproj` targeting net9.0, referencing `AvaloniaVelloCommon`, `VelloSharp.Avalonia`, `VelloSharp.HarfBuzzSharp`, `MiniMvvm`, and copying font assets at build time.
- [ ] Add the new project to `VelloSharp.sln`, `Directory.Build.props`, `Directory.Packages.props` (if new packages are introduced), and update `samples/Samples.slnf` (if present).
- [ ] Scaffold `App.axaml`, `App.axaml.cs`, `Program.cs`, `ViewLocator.cs`, and `AppIcon` following the pattern from `AvaloniaVelloControlsSample`, but pointing to `HarfBuzzGalleryView`.
- [ ] Implement `MainWindow.axaml` with an `AttachedFlyout`-style header plus a `TabControl` using `TabStripPlacement="Left"` for permanent navigation; bind `ItemsSource` to `ObservableCollection<SamplePageViewModel>`.
- [ ] Build `Navigation/ISamplePage` abstractions so each page advertises title, subtitle, glyph, and a lazily-created `UserControl`.
- [ ] Introduce `Rendering/HarfBuzzLeaseSurface.cs` (extends `Control`): handles `OnRender` by requesting `IVelloApiLeaseFeature`, shapes text via view model callbacks, tessellates glyphs into Vello paths, and draws axes/annotations.
- [ ] Add `Services/FontAssetService` for caching `Blob` and `Face` instances (store `Blob.ReleaseDelegate` to dispose pinned memory); expose async loading plus TTC face enumeration.
- [ ] Add `Services/HarfBuzzShapeService` encapsulating buffer creation, shaping, serialization helpers, and conversion into `GlyphRunScene` objects consumable by the lease surface.
- [ ] Define `Diagnostics/ShapeCaptureRecorder` for persisting buffer dumps (`SerializeFormat.Text/Json`) and Vello command stats; ensure it writes to `artifacts/samples/harfbuzz`.

## HarfBuzz API Coverage Map
| API block | Representative types/members | Sample page(s) | Coverage notes |
| --- | --- | --- | --- |
| Core constructs | `NativeObject`, `Blob`, `ReleaseDelegate`, `Tag`, `Language`, `Script`, `GlyphInfo`, `GlyphPosition`, enums | Typeface Explorer, Buffer Playground, Unicode Lab | Demonstrate disposal, tag parsing UI, language/script selectors, live glyph grid. |
| Faces & tables | `Face`, `VariationAxis`, `IFaceTableProvider`, `Face.ReferenceTable`, `Face.CollectUnicodes` (if exposed), `Face.TableTags` | Typeface Explorer | Show table list, raw table hex viewer, custom table provider injection, TTC face navigation. |
| Fonts | `Font`, `Font(Font parent)`, `SetScale`, `SetVariations`, `Variations`, `SetFontFunctions`, `TryGetGlyph*`, `GetHorizontalGlyphAdvance(s)`, `GetKerning`, `TryGetGlyphName`, `TryGetGlyphContourPoint`, `OpenTypeMetrics` | Font Diagnostics, Feature & Variation Lab, Delegates Playground, Metrics Overlay | Provide sliders/inputs for scale/variation, glyph lookup inspector, kerning visualiser, contour debugger. |
| Buffer pipeline | `Buffer` (all add methods), `Direction`, `Language`, `Script`, `BufferFlags`, `ClusterLevel`, `GuessSegmentProperties`, `Reverse*`, `NormalizeGlyphs`, `SerializeGlyphs`/`DeserializeGlyphs`, `DebugDescribeGlyphs`, `DebugDescribeClusters` | Buffer Playground, Serialization Workbench | Interactive shaping across encodings, reversal toggles, serialization diff viewer. |
| Delegates | `FontFunctions` (glyph/advance/extents, kerning, name), `UnicodeFunctions` (combining class, mirroring, script), destroy callbacks | Delegates Playground, Unicode Lab | Allow users to toggle delegate overrides and visualise resulting glyph placement. |
| Metrics | `OpenTypeMetrics.Get*`, `FontExtents`, `GlyphExtents`, `Font.TryGetGlyphExtents` | Metrics Overlay | Render baseline/ascender/descender lines, caret positions, highlight extents per glyph. |
| Diagnostics | `Buffer.DebugDescribeGlyphs`, serialization flags | Serialization Workbench | Text/JSON exports plus Vello command stats with toggles for flags. |
| Variations & features | `FontVariation`, `Feature`, `Tag` utilities | Feature & Variation Lab | Provide slider per axis and toggle per OpenType feature; apply to live text. |

## Sample Page Breakdown

### 1. Typeface & Blob Explorer
**Purpose** – Load fonts into `Blob` objects, create `Face` instances (including TTC multi-face navigation), inspect tables, and display variation axis metadata.

**Implementation Steps**
- [ ] Build `TypefaceExplorerViewModel` with properties for selected asset, `Blob`, `Face`, variation axes, and table descriptors (tag, length, status).
- [ ] Implement `FontAssetService.LoadFontAsync` returning `(Blob blob, Face[] faces)`; ensure release delegate frees file stream pins.
- [ ] Add UI for selecting fonts from asset list or browsing file system; display available faces in a `ListBox`.
- [ ] Implement `TableExplorer` component: call `Face.TableTags`, fetch slices via `Face.ReferenceTable`, and show hex dump (limit to first N bytes with “export” action).
- [ ] Visualise variation axes using `Face.VariationAxes` into slider metadata for later pages; surface min/default/max values.
- [ ] Render glyph atlas preview: request lease and draw first 128 glyphs using current `Face`, painting tag label overlays.

**Coverage Targets**
- `Blob`, `ReleaseDelegate`, `Face`, `Face.ReferenceTable`, `VariationAxis`, `Tag`, core enums.

### 2. Font Diagnostics & Metrics Overlay
**Purpose** – Create `Font` instances, apply scaling/variations, query metrics, and render baseline/extent overlays.

**Implementation Steps**
- [ ] Create `FontDiagnosticsViewModel` that wraps a `Face` and produces primary plus child `Font` instances (demonstrate copy constructor with inherited variations).
- [ ] Surface controls to adjust `SetScale`, toggle parent inheritance, and apply `FontVariation` arrays (persist selection in view model).
- [ ] Render sample text; overlay ascender, descender, baseline, caret metrics using `FontExtents` and `OpenTypeMetrics.Get*`.
- [ ] Add glyph inspector: accept Unicode or glyph name, invoke `Font.TryGetGlyph`, `TryGetGlyphName`, `TryGetGlyphExtents`, `TryGetGlyphHorizontalOrigin`, `TryGetGlyphContourPoint`; display results numerically and visually.
- [ ] Visualise kerning pairs by letting users choose left/right glyphs and showing `Font.GetKerning` deltas via Vello arrows.
- [ ] Persist metric snapshots via `ShapeCaptureRecorder` (store JSON with scale/variation metadata).

**Coverage Targets**
- `Font` constructors, `SetScale`, `SetVariations`, `Variations`, `TryGetGlyph*`, `GetHorizontalGlyphAdvance(s)`, `GetKerning`, `TryGetGlyphExtents`, `OpenTypeMetrics` suite, `FontExtents`.

### 3. Buffer Playground
**Purpose** – Exercise all buffer ingestion paths, direction/script/language inference, cluster operations, and normalization.

**Implementation Steps**
- [ ] Implement `BufferPlaygroundViewModel` encapsulating a reusable `Buffer` plus methods for `AddUtf16`, `AddUtf8`, `AddUtf32`, and manual `Add` with cluster IDs.
- [ ] Provide UI toggles for `Direction`, `Language`, `Script`, `BufferFlags`, `ClusterLevel`, `ReplacementCodepoint`, and `InvisibleGlyph`.
- [ ] Add controls to invoke `GuessSegmentProperties`, `Reverse`, `ReverseRange`, `ReverseClusters`, and `NormalizeGlyphs`; log operations in an observable history.
- [ ] Display glyph infos/positions in a data grid, highlighting clusters and advances; update Vello surface to draw shaped text with per-cluster colouring.
- [ ] Include text area for raw UTF-8 bytes (hex) to feed `AddUtf8(IntPtr, …)` through pinned buffer to prove interop path.
- [ ] Expose serialization panel: call `SerializeGlyphs` with selectable `SerializeFormat` and `SerializeFlag` combinations; allow editing and `DeserializeGlyphs`.

**Coverage Targets**
- Entire `Buffer` API, `GlyphInfo`, `GlyphPosition`, `Serialize*`/`Deserialize*`, `DebugDescribeGlyphs`, `DebugDescribeClusters`.

### 4. Feature & Variation Lab
**Purpose** – Toggle OpenType features, variable font axes, and observe live shaping plus glyph substitution outcomes.

**Implementation Steps**
- [ ] Build `FeatureVariationViewModel` that composes `FontDiagnosticsViewModel` state with feature selections (`Feature` instances built from tags and ranges).
- [ ] Populate feature presets from JSON (standard liga, dlig, salt, ss01–ss20, frac, zero); allow manual tag entry with `Tag.Parse`.
- [ ] Provide slider-driven UI for variation axes; use `Font.SetVariations` and show current `Font.Variations`.
- [ ] Render before/after columns: baseline shaping vs features applied; compute diff overlay (highlight substituted glyphs).
- [ ] Surface `TryGetVariationGlyph` lookups to display glyph substitution triggered by feature/axis combos.
- [ ] Export applied configuration via `ShapeCaptureRecorder` (text dump + JSON).

**Coverage Targets**
- `Feature`, `Tag`, `FontVariation`, `Font.SetVariations`, `Font.Variations`, `Font.TryGetVariationGlyph`, `Tag` helpers.

### 5. Unicode Laboratory
**Purpose** – Explore `Language`, `Script`, and `UnicodeFunctions` overrides for complex scripts, bidi text, and custom combining behaviour.

**Implementation Steps**
- [ ] Craft `UnicodeLabViewModel` exposing script and language pickers populated from `Script`/`Language` helpers and sample corpora.
- [ ] Provide “auto” option calling `Buffer.GuessSegmentProperties` to compare with manual settings; display direction resolution.
- [ ] Implement delegate builder for `UnicodeFunctions`: allow toggling custom combining class, mirroring, general category, script; show the effect on `Buffer.NormalizeGlyphs` and shaping.
- [ ] Visualise bidi reordering: show baseline order vs final glyph order, highlight mirrored characters.
- [ ] Include dataset for Arabic, Hebrew, Indic scripts; ensure Vello render honors directionality by applying transforms.
- [ ] Record delegate invocations in diagnostics log (counts per callback) for transparency.

**Coverage Targets**
- `Language`, `Script`, `UnicodeFunctions` (all delegate properties and destroy), `Buffer.GuessSegmentProperties`, normalization interplay.

### 6. Font Functions Playground
**Purpose** – Override glyph metrics, kerning, naming, and contour retrieval through `FontFunctions` to demonstrate custom shaping pipelines.

**Implementation Steps**
- [ ] Create `FontFunctionsViewModel` with toggles for each delegate (glyph index, advance, extents, kerning, glyph name, glyph contour); provide inline C# expression editor or curated presets.
- [ ] Show baseline vs overridden shaping for the same text, emphasising repositioned glyphs.
- [ ] Provide sample overrides: fake small caps mapping, exaggerated kerning for pair, contour perturbation to demonstrate fallback to Vello when returning `false`.
- [ ] Ensure delegates respect destroy callbacks; log invocation counts and final `bool` results.
- [ ] Bind to Vello surface to draw contour outlines returned by overridden extents/contour point delegates.
- [ ] Add “reset to defaults” action reusing `Font.SetFunctionsOpenType`.

**Coverage Targets**
- `FontFunctions`, delegate setters, `Font.SetFontFunctions` overloads, fallback paths to Vello metrics.

### 7. Serialization & Debugging Workbench
**Purpose** – Provide deep visibility into buffer serialization formats, glyph flags, and shapings round-tripped from text/JSON.

**Implementation Steps**
- [ ] Build `SerializationWorkbenchViewModel` bridging `BufferPlayground`; expose toggles for all `SerializeFlag` combinations and `SerializeFormat` selection.
- [ ] Render serialized output in editors with syntax highlighting (JSON vs text); allow editing and re-import via `DeserializeGlyphs`.
- [ ] Compare original vs deserialized buffer (glyph ID, cluster, advances) and visualise differences in Vello surface.
- [ ] Display `Buffer.DebugDescribeGlyphs` and `DebugDescribeClusters` outputs and cross-link to cluster visualisation.
- [ ] Provide export buttons writing `.hbtext`/`.hbjson` plus `.vello.json` (custom capture) through `ShapeCaptureRecorder`.
- [ ] Integrate quick parity check using reference HarfBuzz (optional if allowed) to prove round-trip fidelity.

**Coverage Targets**
- `SerializeGlyphs` overloads, `SerializeFlag`, `SerializeFormat`, `DeserializeGlyphs`, debug helpers.

### 8. Multi-Script Showcase (Scenes Gallery)
**Purpose** – Present curated scenes combining all features: multi-paragraph layout, emoji runs, variable fonts, custom delegates, demonstrating performance.

**Implementation Steps**
- [ ] Assemble `Scenes/MultiScriptShowcaseScene.cs` using `HarfBuzzShapeService` to compose multiple buffers (Latin, Arabic, Devanagari, emoji, vertical Japanese) into a single Vello scene.
- [ ] Highlight layout guides (grid lines, baseline stack) leveraging outputs from previous pages.
- [ ] Animate feature toggles over time (e.g., gradually enabling ligatures) using dispatcher timer; ensure lease re-renders smoothly.
- [ ] Display performance metrics (frame time, glyph count) from lease to emphasise Vello integration.
- [ ] Provide “capture scene” button storing shapings + Vello commands via diagnostics service.

**Coverage Targets**
- End-to-end use of `Face`, `Font`, `Buffer`, features, variations, delegates, serialization in one orchestrated scene; demonstrates `HarfBuzzSharp` shim parity in real usage.

## Validation & Documentation
- [ ] Add automated sample smoke test in `tests/SamplesSmokeTests` that launches the app headless, navigates to each page, triggers primary actions, and verifies no exceptions (use `Avalonia.Headless` harness if available).
- [ ] Update `README.md` sample matrix and add short description plus screenshot pipeline referencing new app.
- [ ] Extend `docs/hardbuzzsharp-shim-api-coverage.md` with a column indicating “Sample coverage” and link each API block to the corresponding page.
- [ ] Capture animated GIF or MP4 walkthroughs for marketing/demo; store under `docs/media/harfbuzz-sample`.
- [ ] Document known limitations (e.g., JSON glyph-name parity still pending) in the sample’s `README`.

## Milestone Exit Criteria
- All checkboxes above are complete, automated smoke tests pass, and manual verification across Windows/macOS/Linux shows identical shaping output.
- `docs/hardbuzzsharp-shim-api-coverage.md` reflects 100% coverage with direct references to sample pages.
- Artifacts archive contains serialized buffers + capture logs for at least five representative scripts (Latin, Arabic, Hindi, Japanese vertical, Emoji).
- No `ShimNotImplemented` exceptions occur during any sample interaction (validated via DEBUG build run).
