# SkiaSharp Shim API Coverage Matrix

This document enumerates every SkiaSharp surface exposed by the `VelloSharp.Skia.*` shim layer and outlines which VelloSharp primitives or services power that implementation. Each table row indicates the original SkiaSharp type, the shim file that backs it, the VelloSharp APIs consumed internally, and the current coverage status.

Legend:

- **Complete** – behaviour matches the SkiaSharp contract that Avalonia relies on today.
- **Partial** – implemented subset; gaps are called out in notes.
- **Managed-only** – helper that does not call into Vello, included for completeness.
- **Stub** – placeholder with explicit TODOs.

## Surfaces, Canvas & Recording

| SkiaSharp API | Shim implementation | VelloSharp usage | Status / notes |
| --- | --- | --- | --- |
| `SKSurface` | `bindings/VelloSharp.Skia.Core/SKSurface.cs` | `Scene`, `Renderer`, `RenderParams`, `RgbaColor`, `RenderFormat`, `AntialiasingMode` | Complete – surfaces are native Vello scenes snapshotting via `Renderer.Render`. |
| `SKCanvas` | `bindings/VelloSharp.Skia.Core/SKCanvas.cs` | `Scene.FillPath`, `Scene.StrokePath`, `Scene.DrawGlyphRun`, `Scene.DrawImage`, `PathBuilder`, `LayerBlend`, `BrushFactory`, `Glyph`, `ImageBrush`, `Renderer` (indirect via surface snapshots) | Partial – core draw APIs ported; advanced blend modes (`Modulate`, `Screen`, etc.) fall back to SrcOver today. `ClipPath` ignores null/invert combos (throws `ShimNotImplemented`). |
| `SKPictureRecorder` | `bindings/VelloSharp.Skia.Core/SKPictureRecorder.cs` | `Scene` (recording), command log for replay | Complete – records Vello scene commands to replay on any canvas. |
| `SKPicture` | `bindings/VelloSharp.Skia.Core/SKPicture.cs` | Uses `SKSurface` for rasterisation, `SKShader.CreateImageShader` for picture shaders | Partial – picture shaders render through snapshot; scene serialization pending. |
| `SKDrawable` | `bindings/VelloSharp.Skia.Core/SKDrawable.cs` | Delegates to `SKPictureRecorder` | Complete – produces pictures recorded into Vello scenes on demand. |
| `PaintBrush` | `bindings/VelloSharp.Skia.Core/PaintBrush.cs` | `Brush`, `Matrix3x2` | Managed-only – helper struct that packages a Vello brush plus transform. |
| `CanvasCommands` (replay) | `bindings/VelloSharp.Skia.Core/Recording/CanvasCommands.cs` | `SKCanvas` replay to Vello scene underneath | Complete – mirrors Skia picture replay semantics for supported draw ops. |
| `SkiaBackendService` | `bindings/VelloSharp.Skia.Core/SkiaBackend.cs` | Injects CPU/GPU backends providing `Scene`/`Renderer` wiring | Complete – factory registration used by CPU/GPU adapters. |

## Raster Resources & IO

| SkiaSharp API | Shim implementation | VelloSharp usage | Status / notes |
| --- | --- | --- | --- |
| `SKImage` | `bindings/VelloSharp.Skia.Core/SKImage.cs` | `Image` (native handle), `NativeMethods.vello_image_*`, `RenderFormat`, `ImageAlphaMode`, `SkiaImageDecoder` helpers | Partial – PNG decode/resize path wired; encoding beyond RGBA/BGRA pending. |
| `SKBitmap` | `bindings/VelloSharp.Skia.Core/SKBitmap.cs` | — | Managed-only – byte-buffer backed bitmap with decode helpers. |
| `SKPixmap` | `bindings/VelloSharp.Skia.Core/SKPixmap.cs` | — | Managed-only – view over bitmap pixels; used for transfers. |
| `SKImageInfo` / `SKImageInfoExtensions` | `bindings/VelloSharp.Skia.Core/SKImageInfo*.cs` | `SkiaImageDecoder` | Partial – colour-type conversions limited to BGRA/RGBA. |
| `SKCodec` | `bindings/VelloSharp.Skia.Core/SKCodec.cs` | `SkiaImageDecoder` | Partial – PNG only; TODOs reference broader codec support. |
| `SKData`, `SKManagedStream`, `SKStreamAsset` | `bindings/VelloSharp.Skia.Core/IO/*.cs` | — | Managed-only – stream wrappers feeding decoder. |
| `SkiaImageDecoder` | `bindings/VelloSharp.Skia.Core/IO/SkiaImageDecoder.cs` | `NativeMethods.vello_image_decode_png`, `vello_image_resize`, colour conversion helpers | Partial – currently limited to PNG decode; JPEG/WebP pending Vello FFI. |

## Painting, Brushes & Shaders

| SkiaSharp API | Shim implementation | VelloSharp usage | Status / notes |
| --- | --- | --- | --- |
| `SKPaint`, enums (`SKPaintStyle`, `SKStrokeCap`, `SKStrokeJoin`, `SKBlendMode`, `SKClipOperation`, `SKPointMode`) | `bindings/VelloSharp.Skia.Core/SKPaint.cs` | `SolidColorBrush`, `StrokeStyle`, `LineCap`, `LineJoin`, `BrushFactory` | Partial – maps blend modes supported by Vello; advanced Skia blend modes still routed to SrcOver. |
| `SKShader`, `SKShaderTileMode` | `bindings/VelloSharp.Skia.Core/SKShader.cs` | `SolidColorBrush`, `LinearGradientBrush`, `RadialGradientBrush`, `SweepGradientBrush`, `ImageBrush`, `GradientStop`, `ExtendMode`, `RgbaColor` | Partial – linear/radial/two-point/sweep/image gradients mapped; compose shader limited to nested shim shaders. |
| `SKColorFilter`, `SKColorFilters` | `bindings/VelloSharp.Skia.Core/Filters/SKColorFilter.cs` | — | Stub – all creation helpers throw `ShimNotImplemented.Throw`. |
| `SKRuntimeEffect`, `SKRuntimeEffectUniform` | `bindings/VelloSharp.Skia.Core/RuntimeEffect/SKRuntimeEffect.cs` | — | Stub – parsing/validation helpers not yet implemented. |
| `SKRuntimeShaderBuilder` | `bindings/VelloSharp.Skia.Core/RuntimeEffect/SKRuntimeShaderBuilder.cs` | — | Stub – setters and renderer hooks throw `ShimNotImplemented.Throw`. |
| `PaintBrush` helpers | `bindings/VelloSharp.Skia.Core/PaintBrush.cs` | `Brush`, `Matrix3x2` | Managed-only – packages brush + transform for canvas. |
| `SkiaInteropHelpers` (`StrokeInterop`, `BrushInvoker`, `BrushNativeFactory`, `NativeConversionExtensions`, `NativePathElements`) | `bindings/VelloSharp.Skia.Core/SkiaInteropHelpers.cs` | `VelloSharp.VelloBrush*`, `VelloGradientStop`, `VelloPathElement`, `RgbaColor`, `Glyph` | Complete – adapter layer that turns managed brushes/paths into native Vello structures. |
| `SKSamplingOptions` | `bindings/VelloSharp.Skia.Core/SKSamplingOptions.cs` | `VelloSharp.ImageQualityMode` (via decoder) | Partial – translates to Vello resize quality flags. |
| `SKColor`, `SKColors`, `SKColorF` | `bindings/VelloSharp.Skia.Core/SKColor.cs`, `Primitives/SKPrimitives.cs` | `RgbaColor` | Complete – colour conversion to Vello’s RGBA struct. |

## Geometry & Math

| SkiaSharp API | Shim implementation | VelloSharp usage | Status / notes |
| --- | --- | --- | --- |
| `SKPath` | `bindings/VelloSharp.Skia.Core/SKPath.cs` | `PathBuilder` | Complete – builds Vello paths for fill/stroke operations. |
| `SKRect`, `SKRectI`, `SKSizeI` | `bindings/VelloSharp.Skia.Core/SKGeometry.cs`, `Primitives/SKPrimitives.cs` | `PathBuilder` (rect conversion) | Complete – rectangle helpers feed path builders for clipping and fills. |
| `SKRoundRect` | `bindings/VelloSharp.Skia.Core/SKGeometry.cs` | `PathBuilder` | Partial – arc approximation mirrors Skia’s constant but lacks analytic arc join optimisations. |
| `SKPoint`, `SKMatrix`, `SKMatrix44`, `SKMatrix4x4` | `bindings/VelloSharp.Skia.Core/SKGeometry.cs`, `Primitives/SKPrimitives.cs` | `System.Numerics.Matrix3x2` (fed into Vello transforms) | Complete – conversion utilities to/from Vello transforms. |
| `SKGeometry` helpers (`SKPathVerb`, etc.) | `bindings/VelloSharp.Skia.Core/SKGeometry.cs` | `PathBuilder`, `Scene` (via canvas) | Partial – path ops fully mapped; region boolean ops pending work. `SKVertices` factory methods currently stubbed. |

## Text & Typography

| SkiaSharp API | Shim implementation | VelloSharp usage | Status / notes |
| --- | --- | --- | --- |
| `SKTypeface` | `bindings/VelloSharp.Skia.Core/SKTypeface.cs` | `Font.Load`, `Font.TryGetGlyphMetrics`, `GlyphMetrics`, `FontCollection` (via `SKFontManager`) | Complete – wraps Vello font handles, exposes table access, loads embedded Roboto fallback. |
| `SKFont`, hinting/edging enums | `bindings/VelloSharp.Skia.Core/SKFont.cs` | `Font.TryGetGlyphMetrics`, glyph metrics conversions | Partial – hinting/edging properties stored but not yet wired into Vello pipeline. |
| `SKFontManager` | `bindings/VelloSharp.Skia.Core/SKFontManager.cs` | `FontManager`, `FontCollection`, `FontSearchPath`, `VelloSharp.Text` services | Partial – basic match/find API implemented; system font discovery limited. |
| `SKTextBlob`, `SKTextBlobBuilder` | `bindings/VelloSharp.Skia.Core/SKTextBlob.cs` | Replayed through `SKCanvas.DrawTextBlob` into `Scene.DrawGlyphRun` with `GlyphRunOptions` | Partial – intercept calculations stubbed; kerning follows Vello glyph metrics. |
| `SKTextBlobBuilder.PositionedRunBuffer` | `bindings/VelloSharp.Skia.Core/SKTextBlob.cs` | `SKFont.FontSnapshot`, glyph arrays consumed by Vello glyph runs | Complete – used for positioned glyph drawing. |

## Backend Infrastructure

| Component | Shim implementation | VelloSharp usage | Status / notes |
| --- | --- | --- | --- |
| `GpuSkiaBackendFactory`, `GpuSurfaceBackend`, `GpuCanvasBackend`, `GpuPictureRecorderBackend` | `bindings/VelloSharp.Skia.Gpu/GpuSkiaBackend.cs` | `GpuScene`, `GpuRenderer`, `RenderParams`, `LayerBlend`, `Brush`, `GlyphRunOptions` | Complete – module initializer registers GPU backend that drives Vello GPU renderer. |
| `GpuScene`, `GpuRenderer`, interop helpers | `bindings/VelloSharp.Skia.Gpu/GpuInterop.cs` | `VelloSharp.Ffi.Gpu` (`VelloSceneHandle`, `VelloRendererHandle`, `NativeMethods.vello_renderer_render`), `BrushNativeFactory`, `StrokeInterop` | Complete – wraps GPU FFI handles and exposes render pipeline. |
| `CpuSkiaBackendFactory`, `CpuSurfaceBackend`, `CpuCanvasBackend`, `CpuPictureRecorderBackend` | `bindings/VelloSharp.Skia.Cpu/CpuSkiaBackend.cs` | `CpuScene`, `CpuRenderer` (sparse path), `RenderParams` | Complete – CPU fallback uses Vello sparse renderer. |
| `CpuScene`, `CpuRenderer`, sparse interop | `bindings/VelloSharp.Skia.Cpu/CpuSparseInterop.cs` | `VelloSharp.Ffi.Sparse`, `NativeMethods.vello_sparse_render`, `BrushNativeFactory`, `StrokeInterop` | Partial – coverage parity with GPU path; additional tiling optimisations planned. |
| `SkiaBackendService` | `bindings/VelloSharp.Skia.Core/SkiaBackend.cs` | Registers CPU/GPU factories above | Complete – ensures shim selects appropriate backend at startup. |

## Utility & Miscellaneous

| SkiaSharp API | Shim implementation | VelloSharp usage | Status / notes |
| --- | --- | --- | --- |
| `SKPictureRecorder` command list (`ICanvasCommand`) | `bindings/VelloSharp.Skia.Core/Recording/CanvasCommands.cs` | Replays into `Scene` via `SKCanvas` methods | Complete – ensures recorded commands map to Vello draw primitives. |
| `SKPixmap` span helpers | `bindings/VelloSharp.Skia.Core/SKPixmap.cs` | — | Managed-only – provides span access for decode/encode glue. |
| `SKSamplingOptions` high-quality flag | `bindings/VelloSharp.Skia.Core/SKSamplingOptions.cs` | `VelloSharp.ImageQualityMode` | Partial – only toggles between low/high quality resize. |
| `SkiaBackend` interfaces (`ISkiaCanvasBackend`, etc.) | `bindings/VelloSharp.Skia.Core/SkiaBackend.cs` | `Scene`, `RenderParams`, `Brush`, `GlyphRunOptions` | Complete – abstraction consumed by CPU/GPU backends. |
| `SkiaSharp.Graphics.Shaders` (`SkRuntimeEffectGlobals`) | `bindings/VelloSharp.Skia.Core/RuntimeEffect/SkRuntimeEffectGlobals.cs` | — | Stub – shader global helpers are placeholders until runtime effect support lands. |
| `SkiaSharp.IO` namespace helpers | `bindings/VelloSharp.Skia.Core/IO/*.cs` | `NativeMethods.vello_image_*` | Partial – decode only; encode support tracked on TODO list. |
| `Properties/AssemblyInfo.cs` | `bindings/VelloSharp.Skia.Core/Properties` | — | Managed-only metadata (no Vello interaction). |

This matrix is updated whenever new SkiaSharp surface area lands in the shim or when the Vello FFI gains new capabilities that expand coverage. When functionality is added, please update the relevant row with the new Vello dependencies and status.
