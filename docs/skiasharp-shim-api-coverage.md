# SkiaSharp Shim API Coverage Matrix

This document enumerates every SkiaSharp surface exposed by the `VelloSharp.Skia.*` shim layer and outlines which VelloSharp primitives or services power that implementation. Each table row indicates the original SkiaSharp type, the shim file that backs it, the VelloSharp APIs consumed internally, and the current coverage status.

Legend:

- **Complete** – behaviour matches the SkiaSharp contract that Avalonia relies on today.
- **Partial** – implemented subset; gaps are called out in notes.
- **Managed-only** – helper that does not call into Vello, included for completeness.
- **Stub** – placeholder with explicit TODOs.

## Surfaces, Canvas & Recording

| SkiaSharp API | Shim implementation | VelloSharp usage | Status / notes | Sample coverage |
| --- | --- | --- | --- | --- |
| `SKSurface` | `bindings/VelloSharp.Skia.Core/SKSurface.cs` | `Scene`, `Renderer`, `RenderParams`, `RgbaColor`, `RenderFormat`, `AntialiasingMode` | Complete – surfaces are native Vello scenes snapshotting via `Renderer.Render`. | Surface Dashboard; Recording & Replay Studio |
| `SKCanvas` | `bindings/VelloSharp.Skia.Core/SKCanvas.cs` | `Scene.FillPath`, `Scene.StrokePath`, `Scene.DrawGlyphRun`, `Scene.DrawImage`, `PathBuilder`, `LayerBlend`, `BrushFactory`, `Glyph`, `ImageBrush`, `Renderer` (indirect via surface snapshots) | Partial – core draw APIs ported; advanced blend modes (`Modulate`, `Screen`, etc.) fall back to SrcOver today. `ClipPath` ignores null/invert combos (throws `ShimNotImplemented`). | Surface Dashboard; Canvas & Paint Studio; Geometry Explorer |
| `SKPictureRecorder` | `bindings/VelloSharp.Skia.Core/SKPictureRecorder.cs` | `Scene` (recording), command log for replay | Complete – records Vello scene commands to replay on any canvas. | Recording & Replay Studio |
| `SKPicture` | `bindings/VelloSharp.Skia.Core/SKPicture.cs` | Uses `SKSurface` for rasterisation, `SKShader.CreateImageShader` for picture shaders | Partial – picture shaders render through snapshot; scene serialization pending. | Recording & Replay Studio |
| `SKDrawable` | `bindings/VelloSharp.Skia.Core/SKDrawable.cs` | Delegates to `SKPictureRecorder` | Complete – produces pictures recorded into Vello scenes on demand. | Recording & Replay Studio |
| `PaintBrush` | `bindings/VelloSharp.Skia.Core/PaintBrush.cs` | `Brush`, `Matrix3x2` | Managed-only – helper struct that packages a Vello brush plus transform. | Canvas & Paint Studio |
| `CanvasCommands` (replay) | `bindings/VelloSharp.Skia.Core/Recording/CanvasCommands.cs` | `SKCanvas` replay to Vello scene underneath | Complete – mirrors Skia picture replay semantics for supported draw ops. | Recording & Replay Studio |
| `SkiaBackendService` | `bindings/VelloSharp.Skia.Core/SkiaBackend.cs` | Injects CPU/GPU backends providing `Scene`/`Renderer` wiring | Complete – factory registration used by CPU/GPU adapters. | Surface Dashboard; Runtime Effect Forge |

## Raster Resources & IO

| SkiaSharp API | Shim implementation | VelloSharp usage | Status / notes | Sample coverage |
| --- | --- | --- | --- | --- |
| `SKImage` | `bindings/VelloSharp.Skia.Core/SKImage.cs` | `Image` (native handle), `NativeMethods.vello_image_*`, `RenderFormat`, `ImageAlphaMode`, `SkiaImageDecoder` helpers | Partial – PNG decode/resize path wired; encoding beyond RGBA/BGRA pending. | Image Workshop; IO & Diagnostics Workbench |
| `SKBitmap` | `bindings/VelloSharp.Skia.Core/SKBitmap.cs` | — | Managed-only – byte-buffer backed bitmap with decode helpers. | Image Workshop |
| `SKPixmap` | `bindings/VelloSharp.Skia.Core/SKPixmap.cs` | — | Managed-only – view over bitmap pixels; used for transfers. | Image Workshop |
| `SKImageInfo` / `SKImageInfoExtensions` | `bindings/VelloSharp.Skia.Core/SKImageInfo*.cs` | `SkiaImageDecoder` | Partial – colour-type conversions limited to BGRA/RGBA. | Surface Dashboard; Image Workshop |
| `SKCodec` | `bindings/VelloSharp.Skia.Core/SKCodec.cs` | `SkiaImageDecoder` | Partial – PNG only; TODOs reference broader codec support. | Image Workshop |
| `SKData`, `SKManagedStream`, `SKStreamAsset` | `bindings/VelloSharp.Skia.Core/IO/*.cs` | — | Managed-only – stream wrappers feeding decoder. | IO & Diagnostics Workbench; Image Workshop |
| `SkiaImageDecoder` | `bindings/VelloSharp.Skia.Core/IO/SkiaImageDecoder.cs` | `NativeMethods.vello_image_decode_png`, `vello_image_resize`, colour conversion helpers | Partial – currently limited to PNG decode; JPEG/WebP pending Vello FFI. | Image Workshop |

## Painting, Brushes & Shaders

| SkiaSharp API | Shim implementation | VelloSharp usage | Status / notes | Sample coverage |
| --- | --- | --- | --- | --- |
| `SKPaint`, enums (`SKPaintStyle`, `SKStrokeCap`, `SKStrokeJoin`, `SKBlendMode`, `SKClipOperation`, `SKPointMode`) | `bindings/VelloSharp.Skia.Core/SKPaint.cs` | `SolidColorBrush`, `StrokeStyle`, `LineCap`, `LineJoin`, `BrushFactory` | Partial – maps blend modes supported by Vello; advanced Skia blend modes still routed to SrcOver. | Canvas & Paint Studio; Geometry Explorer |
| `SKShader`, `SKShaderTileMode` | `bindings/VelloSharp.Skia.Core/SKShader.cs` | `SolidColorBrush`, `LinearGradientBrush`, `RadialGradientBrush`, `SweepGradientBrush`, `ImageBrush`, `GradientStop`, `ExtendMode`, `RgbaColor` | Partial – linear/radial/two-point/sweep/image gradients mapped; compose shader limited to nested shim shaders. | Canvas & Paint Studio; Image Workshop; Runtime Effect Forge |
| `SKColorFilter`, `SKColorFilters` | `bindings/VelloSharp.Skia.Core/Filters/SKColorFilter.cs` | — | Stub – all creation helpers throw `ShimNotImplemented.Throw`. | — |
| `SKRuntimeEffect`, `SKRuntimeEffectUniform` | `bindings/VelloSharp.Skia.Core/RuntimeEffect/SKRuntimeEffect.cs` | Generates Vello runtime shaders and uniform bindings | Partial – SkSL compilation active; child effects and texture uniforms pending. | Runtime Effect Forge |
| `SKRuntimeShaderBuilder` | `bindings/VelloSharp.Skia.Core/RuntimeEffect/SKRuntimeShaderBuilder.cs` | — | Stub – setters and renderer hooks throw `ShimNotImplemented.Throw`. | — |
| `PaintBrush` helpers | `bindings/VelloSharp.Skia.Core/PaintBrush.cs` | `Brush`, `Matrix3x2` | Managed-only – packages brush + transform for canvas. | Canvas & Paint Studio |
| `SkiaInteropHelpers` (`StrokeInterop`, `BrushInvoker`, `BrushNativeFactory`, `NativeConversionExtensions`, `NativePathElements`) | `bindings/VelloSharp.Skia.Core/SkiaInteropHelpers.cs` | `VelloSharp.VelloBrush*`, `VelloGradientStop`, `VelloPathElement`, `RgbaColor`, `Glyph` | Complete – adapter layer that turns managed brushes/paths into native Vello structures. | Canvas & Paint Studio; Image Workshop |
| `SKSamplingOptions` | `bindings/VelloSharp.Skia.Core/SKSamplingOptions.cs` | `VelloSharp.ImageQualityMode` (via decoder) | Partial – translates to Vello resize quality flags. | Image Workshop |
| `SKColor`, `SKColors`, `SKColorF` | `bindings/VelloSharp.Skia.Core/SKColor.cs`, `Primitives/SKPrimitives.cs` | `RgbaColor` | Complete – colour conversion to Vello’s RGBA struct. | Canvas & Paint Studio; Image Workshop; Geometry Explorer |

## Geometry & Math

| SkiaSharp API | Shim implementation | VelloSharp usage | Status / notes | Sample coverage |
| --- | --- | --- | --- | --- |
| `SKPath` | `bindings/VelloSharp.Skia.Core/SKPath.cs` | `PathBuilder` | Complete – builds Vello paths for fill/stroke operations. | Geometry Explorer |
| `SKRect`, `SKRectI`, `SKSizeI` | `bindings/VelloSharp.Skia.Core/SKGeometry.cs`, `Primitives/SKPrimitives.cs` | `PathBuilder` (rect conversion) | Complete – rectangle helpers feed path builders for clipping and fills. | Surface Dashboard; Geometry Explorer |
| `SKRoundRect` | `bindings/VelloSharp.Skia.Core/SKGeometry.cs` | `PathBuilder` | Partial – arc approximation mirrors Skia’s constant but lacks analytic arc join optimisations. | Geometry Explorer; Surface Dashboard |
| `SKPoint`, `SKMatrix`, `SKMatrix44`, `SKMatrix4x4` | `bindings/VelloSharp.Skia.Core/SKGeometry.cs`, `Primitives/SKPrimitives.cs` | `System.Numerics.Matrix3x2` (fed into Vello transforms) | Complete – conversion utilities to/from Vello transforms. | Surface Dashboard; Geometry Explorer; Advanced Utilities |
| `SKGeometry` helpers (`SKPathVerb`, etc.) | `bindings/VelloSharp.Skia.Core/SKGeometry.cs` | `PathBuilder`, `Scene` (via canvas) | Partial – path ops fully mapped; region boolean ops pending work. `SKVertices` factory methods currently stubbed. | Geometry Explorer |

## Text & Typography

| SkiaSharp API | Shim implementation | VelloSharp usage | Status / notes | Sample coverage |
| --- | --- | --- | --- | --- |
| `SKTypeface` | `bindings/VelloSharp.Skia.Core/SKTypeface.cs` | `Font.Load`, `Font.TryGetGlyphMetrics`, `GlyphMetrics`, `FontCollection` (via `SKFontManager`) | Complete – wraps Vello font handles, exposes table access, loads embedded Roboto fallback. | Typography Playground |
| `SKFont`, hinting/edging enums | `bindings/VelloSharp.Skia.Core/SKFont.cs` | `Font.TryGetGlyphMetrics`, glyph metrics conversions | Partial – hinting/edging properties stored but not yet wired into Vello pipeline. | Typography Playground |
| `SKFontManager` | `bindings/VelloSharp.Skia.Core/SKFontManager.cs` | `FontManager`, `FontCollection`, `FontSearchPath`, `VelloSharp.Text` services | Partial – basic match/find API implemented; system font discovery limited. | Typography Playground |
| `SKTextBlob`, `SKTextBlobBuilder` | `bindings/VelloSharp.Skia.Core/SKTextBlob.cs` | Replayed through `SKCanvas.DrawTextBlob` into `Scene.DrawGlyphRun` with `GlyphRunOptions` | Partial – intercept calculations stubbed; kerning follows Vello glyph metrics. | Typography Playground |
| `SKTextBlobBuilder.PositionedRunBuffer` | `bindings/VelloSharp.Skia.Core/SKTextBlob.cs` | `SKFont.FontSnapshot`, glyph arrays consumed by Vello glyph runs | Complete – used for positioned glyph drawing. | Typography Playground |

## Backend Infrastructure

| Component | Shim implementation | VelloSharp usage | Status / notes | Sample coverage |
| --- | --- | --- | --- | --- |
| `GpuSkiaBackendFactory`, `GpuSurfaceBackend`, `GpuCanvasBackend`, `GpuPictureRecorderBackend` | `bindings/VelloSharp.Skia.Gpu/GpuSkiaBackend.cs` | `GpuScene`, `GpuRenderer`, `RenderParams`, `LayerBlend`, `Brush`, `GlyphRunOptions` | Complete – module initializer registers GPU backend that drives Vello GPU renderer. | Surface Dashboard; Runtime Effect Forge |
| `GpuScene`, `GpuRenderer`, interop helpers | `bindings/VelloSharp.Skia.Gpu/GpuInterop.cs` | `VelloSharp.Ffi.Gpu` (`VelloSceneHandle`, `VelloRendererHandle`, `NativeMethods.vello_renderer_render`), `BrushNativeFactory`, `StrokeInterop` | Complete – wraps GPU FFI handles and exposes render pipeline. | Surface Dashboard; Runtime Effect Forge |
| `CpuSkiaBackendFactory`, `CpuSurfaceBackend`, `CpuCanvasBackend`, `CpuPictureRecorderBackend` | `bindings/VelloSharp.Skia.Cpu/CpuSkiaBackend.cs` | `CpuScene`, `CpuRenderer` (sparse path), `RenderParams` | Complete – CPU fallback uses Vello sparse renderer. | Surface Dashboard; Image Workshop |
| `CpuScene`, `CpuRenderer`, sparse interop | `bindings/VelloSharp.Skia.Cpu/CpuSparseInterop.cs` | `VelloSharp.Ffi.Sparse`, `NativeMethods.vello_sparse_render`, `BrushNativeFactory`, `StrokeInterop` | Partial – coverage parity with GPU path; additional tiling optimisations planned. | Surface Dashboard; Image Workshop |
| `SkiaBackendService` | `bindings/VelloSharp.Skia.Core/SkiaBackend.cs` | Registers CPU/GPU factories above | Complete – ensures shim selects appropriate backend at startup. | Surface Dashboard; Runtime Effect Forge |

## Utility & Miscellaneous

| SkiaSharp API | Shim implementation | VelloSharp usage | Status / notes | Sample coverage |
| --- | --- | --- | --- | --- |
| `SKPictureRecorder` command list (`ICanvasCommand`) | `bindings/VelloSharp.Skia.Core/Recording/CanvasCommands.cs` | Replays into `Scene` via `SKCanvas` methods | Complete – ensures recorded commands map to Vello draw primitives. | Recording & Replay Studio |
| `SKPixmap` span helpers | `bindings/VelloSharp.Skia.Core/SKPixmap.cs` | — | Managed-only – provides span access for decode/encode glue. | Image Workshop |
| `SKSamplingOptions` high-quality flag | `bindings/VelloSharp.Skia.Core/SKSamplingOptions.cs` | `VelloSharp.ImageQualityMode` | Partial – only toggles between low/high quality resize. | Image Workshop |
| `SkiaBackend` interfaces (`ISkiaCanvasBackend`, etc.) | `bindings/VelloSharp.Skia.Core/SkiaBackend.cs` | `Scene`, `RenderParams`, `Brush`, `GlyphRunOptions` | Complete – abstraction consumed by CPU/GPU backends. | Surface Dashboard |
| `SkiaSharp.Graphics.Shaders` (`SkRuntimeEffectGlobals`) | `bindings/VelloSharp.Skia.Core/RuntimeEffect/SkRuntimeEffectGlobals.cs` | — | Stub – shader global helpers are placeholders until runtime effect support lands. | Runtime Effect Forge |
| `SkiaSharp.IO` namespace helpers | `bindings/VelloSharp.Skia.Core/IO/*.cs` | `NativeMethods.vello_image_*` | Partial – decode only; encode support tracked on TODO list. | IO & Diagnostics Workbench; Image Workshop |
| `Properties/AssemblyInfo.cs` | `bindings/VelloSharp.Skia.Core/Properties` | — | Managed-only metadata (no Vello interaction). | — |

This matrix is updated whenever new SkiaSharp surface area lands in the shim or when the Vello FFI gains new capabilities that expand coverage. When functionality is added, please update the relevant row with the new Vello dependencies and status.
