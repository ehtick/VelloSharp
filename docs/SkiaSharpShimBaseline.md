# Avalonia → VelloSharp Skia API Baseline

_Generated on 2025-10-17T18:38:23Z_

## Summary

| Skia type | Avalonia files | Shim file(s) | Missing members |
| --- | --- | --- | --- |
| SKAlphaType | 4 file(s)<br>bindings/Avalonia.Skia/FramebufferRenderTarget.cs<br>bindings/Avalonia.Skia/SkiaSharpExtensions.cs<br>bindings/Avalonia.Skia/SurfaceRenderTarget.cs<br>… (+1 more) | bindings/VelloSharp.Skia.Core/SKImageInfo.cs | — |
| SKBitmap | 4 file(s)<br>bindings/Avalonia.Skia/DrawingContextImpl.cs<br>bindings/Avalonia.Skia/FramebufferRenderTarget.cs<br>bindings/Avalonia.Skia/ImmutableBitmap.cs<br>… (+1 more) | bindings/VelloSharp.Skia.Core/SKBitmap.cs | — |
| SKBitmapReleaseDelegate | 1 file(s)<br>bindings/Avalonia.Skia/WriteableBitmapImpl.cs | — | — |
| SKBlendMode | 2 file(s)<br>bindings/Avalonia.Skia/DrawingContextImpl.cs<br>bindings/Avalonia.Skia/SkiaSharpExtensions.cs | bindings/VelloSharp.Skia.Core/SKBlendMode.cs | — |
| SKCacheBase | 4 file(s)<br>bindings/Avalonia.Skia/SKCacheBase.cs<br>bindings/Avalonia.Skia/SKPaintCache.cs<br>bindings/Avalonia.Skia/SKRoundRectCache.cs<br>… (+1 more) | — | — |
| SKCanvas | 7 file(s)<br>bindings/Avalonia.Skia/DrawingContextImpl.cs<br>bindings/Avalonia.Skia/Gpu/ISkiaGpu.cs<br>bindings/Avalonia.Skia/Gpu/OpenGl/FboSkiaSurface.cs<br>… (+4 more) | bindings/VelloSharp.Skia.Core/SKCanvas.cs | — |
| SKClipOperation | 1 file(s)<br>bindings/Avalonia.Skia/DrawingContextImpl.cs | bindings/VelloSharp.Skia.Core/SKClipOperation.cs | — |
| SKCodec | 2 file(s)<br>bindings/Avalonia.Skia/ImmutableBitmap.cs<br>bindings/Avalonia.Skia/WriteableBitmapImpl.cs | bindings/VelloSharp.Skia.Core/SKCodec.cs | — |
| SKColor | 4 file(s)<br>bindings/Avalonia.Skia/DrawingContextImpl.Effects.cs<br>bindings/Avalonia.Skia/DrawingContextImpl.cs<br>bindings/Avalonia.Skia/SkiaSharpExtensions.cs<br>… (+1 more) | bindings/VelloSharp.Skia.Core/SKColor.cs | Empty |
| SKColorF | 1 file(s)<br>bindings/Avalonia.Skia/DrawingContextImpl.cs | bindings/VelloSharp.Skia.Core/Primitives/SKPrimitives.cs | — |
| SKColorFilter | 1 file(s)<br>bindings/Avalonia.Skia/DrawingContextImpl.cs | bindings/VelloSharp.Skia.Core/SKColorFilter.cs | — |
| SKColorSpace | 2 file(s)<br>bindings/Avalonia.Skia/Gpu/Vulkan/VulkanSkiaExternalObjectsFeature.cs<br>bindings/Avalonia.Skia/Gpu/Vulkan/VulkanSkiaRenderTarget.cs | bindings/VelloSharp.Skia.Core/SKColorSpace.cs | — |
| SKColorType | 12 file(s)<br>bindings/Avalonia.Skia/FramebufferRenderTarget.cs<br>bindings/Avalonia.Skia/Gpu/Metal/SkiaMetalGpu.cs<br>bindings/Avalonia.Skia/Gpu/OpenGl/FboSkiaSurface.cs<br>… (+9 more) | bindings/VelloSharp.Skia.Core/SKImageInfo.cs | — |
| SKCubicResampler | 1 file(s)<br>bindings/Avalonia.Skia/SkiaSharpExtensions.cs | bindings/VelloSharp.Skia.Core/SKSamplingPrimitives.cs | — |
| SKData | 2 file(s)<br>bindings/Avalonia.Skia/ImmutableBitmap.cs<br>bindings/Avalonia.Skia/WriteableBitmapImpl.cs | bindings/VelloSharp.Skia.Core/IO/SKData.cs | — |
| SKDocument | 1 file(s)<br>bindings/Avalonia.Skia/Helpers/DrawingContextHelper.cs | bindings/VelloSharp.Skia.Core/SKDocument.cs | — |
| SKEncodedImageFormat | 1 file(s)<br>bindings/Avalonia.Skia/Helpers/ImageSavingHelper.cs | bindings/VelloSharp.Skia.Core/SKEncodedImageFormat.cs | — |
| SKFilterMode | 1 file(s)<br>bindings/Avalonia.Skia/SkiaSharpExtensions.cs | bindings/VelloSharp.Skia.Core/SKSamplingPrimitives.cs | — |
| SKFont | 2 file(s)<br>bindings/Avalonia.Skia/GlyphRunImpl.cs<br>bindings/Avalonia.Skia/GlyphTypefaceImpl.cs | bindings/VelloSharp.Skia.Core/SKFont.cs | — |
| SKFontEdging | 1 file(s)<br>bindings/Avalonia.Skia/GlyphRunImpl.cs | bindings/VelloSharp.Skia.Core/SKFont.cs | — |
| SKFontHinting | 2 file(s)<br>bindings/Avalonia.Skia/GlyphRunImpl.cs<br>bindings/Avalonia.Skia/PlatformRenderInterface.cs | bindings/VelloSharp.Skia.Core/SKFont.cs | — |
| SKFontManager | 1 file(s)<br>bindings/Avalonia.Skia/FontManagerImpl.cs | bindings/VelloSharp.Skia.Core/SKFontManager.cs | — |
| SKFontStyle | 1 file(s)<br>bindings/Avalonia.Skia/FontManagerImpl.cs | bindings/VelloSharp.Skia.Core/SKFontManager.cs | — |
| SKFontStyleSlant | 2 file(s)<br>bindings/Avalonia.Skia/FontManagerImpl.cs<br>bindings/Avalonia.Skia/SkiaSharpExtensions.cs | bindings/VelloSharp.Skia.Core/SKFontManager.cs | — |
| SKFontStyleWeight | 1 file(s)<br>bindings/Avalonia.Skia/FontManagerImpl.cs | bindings/VelloSharp.Skia.Core/SKFontManager.cs | — |
| SKFontStyleWidth | 1 file(s)<br>bindings/Avalonia.Skia/FontManagerImpl.cs | bindings/VelloSharp.Skia.Core/SKFontManager.cs | — |
| SKImage | 4 file(s)<br>bindings/Avalonia.Skia/Helpers/ImageSavingHelper.cs<br>bindings/Avalonia.Skia/ImmutableBitmap.cs<br>bindings/Avalonia.Skia/SurfaceRenderTarget.cs<br>… (+1 more) | bindings/VelloSharp.Skia.Core/SKImage.cs | — |
| SKImageCachingHint | 1 file(s)<br>bindings/Avalonia.Skia/FramebufferRenderTarget.cs | bindings/VelloSharp.Skia.Core/SKImage.cs | — |
| SKImageFilter | 2 file(s)<br>bindings/Avalonia.Skia/DrawingContextImpl.Effects.cs<br>bindings/Avalonia.Skia/DrawingContextImpl.cs | bindings/VelloSharp.Skia.Core/SKImageFilter.cs | — |
| SKImageInfo | 7 file(s)<br>bindings/Avalonia.Skia/FramebufferRenderTarget.cs<br>bindings/Avalonia.Skia/Helpers/PixelFormatHelper.cs<br>bindings/Avalonia.Skia/ImmutableBitmap.cs<br>… (+4 more) | bindings/VelloSharp.Skia.Core/SKImageInfo.cs | — |
| SKManagedStream | 2 file(s)<br>bindings/Avalonia.Skia/ImmutableBitmap.cs<br>bindings/Avalonia.Skia/WriteableBitmapImpl.cs | bindings/VelloSharp.Skia.Core/IO/SKManagedStream.cs | — |
| SKMatrix | 3 file(s)<br>bindings/Avalonia.Skia/DrawingContextImpl.cs<br>bindings/Avalonia.Skia/Helpers/ImageSavingHelper.cs<br>bindings/Avalonia.Skia/SkiaSharpExtensions.cs | bindings/VelloSharp.Skia.Core/Primitives/SKPrimitives.cs | — |
| SKMatrix44 | 1 file(s)<br>bindings/Avalonia.Skia/SkiaSharpExtensions.cs | bindings/VelloSharp.Skia.Core/Primitives/SKPrimitives.cs | — |
| SKMatrix4x4 | 1 file(s)<br>bindings/Avalonia.Skia/DrawingContextImpl.cs | bindings/VelloSharp.Skia.Core/Primitives/SKPrimitives.cs | — |
| SKMipmapMode | 1 file(s)<br>bindings/Avalonia.Skia/SkiaSharpExtensions.cs | bindings/VelloSharp.Skia.Core/SKSamplingPrimitives.cs | — |
| SKPaint | 7 file(s)<br>bindings/Avalonia.Skia/DrawingContextImpl.cs<br>bindings/Avalonia.Skia/IDrawableBitmapImpl.cs<br>bindings/Avalonia.Skia/ImmutableBitmap.cs<br>… (+4 more) | bindings/VelloSharp.Skia.Core/SKPaint.cs | — |
| SKPaintCache | 4 file(s)<br>bindings/Avalonia.Skia/DrawingContextImpl.Effects.cs<br>bindings/Avalonia.Skia/DrawingContextImpl.cs<br>bindings/Avalonia.Skia/Helpers/SKPathHelper.cs<br>… (+1 more) | — | Shared |
| SKPaints | 1 file(s)<br>bindings/Avalonia.Skia/SKPaintCache.cs | — | — |
| SKPath | 11 file(s)<br>bindings/Avalonia.Skia/CombinedGeometryImpl.cs<br>bindings/Avalonia.Skia/EllipseGeometryImpl.cs<br>bindings/Avalonia.Skia/GeometryGroupImpl.cs<br>… (+8 more) | bindings/VelloSharp.Skia.Core/SKPath.cs | — |
| SKPathArcSize | 1 file(s)<br>bindings/Avalonia.Skia/StreamGeometryImpl.cs | bindings/VelloSharp.Skia.Core/SKPathPrimitives.cs | — |
| SKPathDirection | 1 file(s)<br>bindings/Avalonia.Skia/StreamGeometryImpl.cs | bindings/VelloSharp.Skia.Core/SKPathPrimitives.cs | — |
| SKPathEffect | 1 file(s)<br>bindings/Avalonia.Skia/Helpers/DrawingContextHelper.cs | bindings/VelloSharp.Skia.Core/SKPathEffect.cs | — |
| SKPathFillType | 2 file(s)<br>bindings/Avalonia.Skia/GeometryGroupImpl.cs<br>bindings/Avalonia.Skia/StreamGeometryImpl.cs | bindings/VelloSharp.Skia.Core/SKPathPrimitives.cs | — |
| SKPathHelper | 2 file(s)<br>bindings/Avalonia.Skia/GeometryImpl.cs<br>bindings/Avalonia.Skia/Helpers/SKPathHelper.cs | — | CreateClosedPath, CreateStrokedPath |
| SKPathMeasure | 1 file(s)<br>bindings/Avalonia.Skia/GeometryImpl.cs | bindings/VelloSharp.Skia.Core/SKPathMeasure.cs | — |
| SKPathOp | 1 file(s)<br>bindings/Avalonia.Skia/CombinedGeometryImpl.cs | bindings/VelloSharp.Skia.Core/SKPathPrimitives.cs | — |
| SKPathVerb | 1 file(s)<br>bindings/Avalonia.Skia/Helpers/SKPathHelper.cs | bindings/VelloSharp.Skia.Core/SKPathPrimitives.cs | — |
| SKPicture | 3 file(s)<br>bindings/Avalonia.Skia/DrawingContextImpl.cs<br>bindings/Avalonia.Skia/Helpers/ImageSavingHelper.cs<br>bindings/Avalonia.Skia/PictureRenderTarget.cs | bindings/VelloSharp.Skia.Core/SKPicture.cs | — |
| SKPictureRecorder | 1 file(s)<br>bindings/Avalonia.Skia/PictureRenderTarget.cs | bindings/VelloSharp.Skia.Core/SKPictureRecorder.cs | — |
| SKPixelGeometry | 4 file(s)<br>bindings/Avalonia.Skia/FramebufferRenderTarget.cs<br>bindings/Avalonia.Skia/Gpu/OpenGl/FboSkiaSurface.cs<br>bindings/Avalonia.Skia/Gpu/OpenGl/GlRenderTarget.cs<br>… (+1 more) | bindings/VelloSharp.Skia.Core/SKPixelGeometry.cs | — |
| SKPoint | 4 file(s)<br>bindings/Avalonia.Skia/GlyphRunImpl.cs<br>bindings/Avalonia.Skia/Helpers/SKPathHelper.cs<br>bindings/Avalonia.Skia/SKRoundRectCache.cs<br>… (+1 more) | bindings/VelloSharp.Skia.Core/SKGeometry.cs | — |
| SKRect | 9 file(s)<br>bindings/Avalonia.Skia/DrawingContextImpl.cs<br>bindings/Avalonia.Skia/GlyphRunImpl.cs<br>bindings/Avalonia.Skia/IDrawableBitmapImpl.cs<br>… (+6 more) | bindings/VelloSharp.Skia.Core/SKGeometry.cs | — |
| SKRectI | 2 file(s)<br>bindings/Avalonia.Skia/SkiaRegionImpl.cs<br>bindings/Avalonia.Skia/SkiaSharpExtensions.cs | bindings/VelloSharp.Skia.Core/Primitives/SKPrimitives.cs | — |
| SKRegion | 1 file(s)<br>bindings/Avalonia.Skia/SkiaRegionImpl.cs | bindings/VelloSharp.Skia.Core/SKRegion.cs | — |
| SKRegionOperation | 1 file(s)<br>bindings/Avalonia.Skia/SkiaRegionImpl.cs | bindings/VelloSharp.Skia.Core/SKRegionOperation.cs | — |
| SKRoundRect | 3 file(s)<br>bindings/Avalonia.Skia/DrawingContextImpl.cs<br>bindings/Avalonia.Skia/SKRoundRectCache.cs<br>bindings/Avalonia.Skia/SkiaSharpExtensions.cs | bindings/VelloSharp.Skia.Core/SKRoundRect.cs | — |
| SKRoundRectCache | 2 file(s)<br>bindings/Avalonia.Skia/DrawingContextImpl.cs<br>bindings/Avalonia.Skia/SKRoundRectCache.cs | — | Shared |
| SKSamplingOptions | 5 file(s)<br>bindings/Avalonia.Skia/IDrawableBitmapImpl.cs<br>bindings/Avalonia.Skia/ImmutableBitmap.cs<br>bindings/Avalonia.Skia/SkiaSharpExtensions.cs<br>… (+2 more) | bindings/VelloSharp.Skia.Core/SKSamplingOptions.cs | — |
| SKShader | 1 file(s)<br>bindings/Avalonia.Skia/DrawingContextImpl.cs | bindings/VelloSharp.Skia.Core/SKShader.cs | CreateBitmap |
| SKShaderTileMode | 2 file(s)<br>bindings/Avalonia.Skia/DrawingContextImpl.cs<br>bindings/Avalonia.Skia/SkiaSharpExtensions.cs | bindings/VelloSharp.Skia.Core/SKShader.cs | — |
| SKSizeI | 1 file(s)<br>bindings/Avalonia.Skia/Helpers/ImageSavingHelper.cs | bindings/VelloSharp.Skia.Core/Primitives/SKPrimitives.cs | — |
| SKStrokeCap | 1 file(s)<br>bindings/Avalonia.Skia/SkiaSharpExtensions.cs | bindings/VelloSharp.Skia.Core/SKPaint.cs | — |
| SKStrokeJoin | 1 file(s)<br>bindings/Avalonia.Skia/SkiaSharpExtensions.cs | bindings/VelloSharp.Skia.Core/SKPaint.cs | — |
| SKSurface | 12 file(s)<br>bindings/Avalonia.Skia/DrawingContextImpl.cs<br>bindings/Avalonia.Skia/FramebufferRenderTarget.cs<br>bindings/Avalonia.Skia/Gpu/ISkiaGpu.cs<br>… (+9 more) | bindings/VelloSharp.Skia.Core/SKSurface.cs | — |
| SKSurfaceProperties | 4 file(s)<br>bindings/Avalonia.Skia/FramebufferRenderTarget.cs<br>bindings/Avalonia.Skia/Gpu/OpenGl/FboSkiaSurface.cs<br>bindings/Avalonia.Skia/Gpu/OpenGl/GlRenderTarget.cs<br>… (+1 more) | bindings/VelloSharp.Skia.Core/SKSurfaceProperties.cs | — |
| SKTextAlign | 1 file(s)<br>bindings/Avalonia.Skia/SkiaSharpExtensions.cs | bindings/VelloSharp.Skia.Core/SKTextAlign.cs | — |
| SKTextBlob | 1 file(s)<br>bindings/Avalonia.Skia/GlyphRunImpl.cs | bindings/VelloSharp.Skia.Core/SKTextBlob.cs | — |
| SKTextBlobBuilder | 1 file(s)<br>bindings/Avalonia.Skia/SKTextBlobBuilderCache.cs | bindings/VelloSharp.Skia.Core/SKTextBlob.cs | — |
| SKTextBlobBuilderCache | 2 file(s)<br>bindings/Avalonia.Skia/GlyphRunImpl.cs<br>bindings/Avalonia.Skia/SKTextBlobBuilderCache.cs | — | Shared |
| SKTypeface | 2 file(s)<br>bindings/Avalonia.Skia/FontManagerImpl.cs<br>bindings/Avalonia.Skia/GlyphTypefaceImpl.cs | bindings/VelloSharp.Skia.Core/SKTypeface.cs | — |

## Detailed Breakdown

### SKAlphaType

- Shim definition: bindings/VelloSharp.Skia.Core/SKImageInfo.cs
- Avalonia usage files (4): bindings/Avalonia.Skia/FramebufferRenderTarget.cs, bindings/Avalonia.Skia/SkiaSharpExtensions.cs, bindings/Avalonia.Skia/SurfaceRenderTarget.cs, bindings/Avalonia.Skia/WriteableBitmapImpl.cs
- Members invoked:
  - `Opaque` — 3 site(s), e.g. bindings/Avalonia.Skia/FramebufferRenderTarget.cs:70 (_shim: present_)
  - `Premul` — 4 site(s), e.g. bindings/Avalonia.Skia/FramebufferRenderTarget.cs:70 (_shim: present_)
  - `Unpremul` — 2 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:234 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 11

### SKBitmap

- Shim definition: bindings/VelloSharp.Skia.Core/SKBitmap.cs
- Avalonia usage files (4): bindings/Avalonia.Skia/DrawingContextImpl.cs, bindings/Avalonia.Skia/FramebufferRenderTarget.cs, bindings/Avalonia.Skia/ImmutableBitmap.cs, bindings/Avalonia.Skia/WriteableBitmapImpl.cs
- Members invoked:
  - `Decode` — 5 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:1329 (_shim: present_)
- Constructor/`new` call sites (4): bindings/Avalonia.Skia/FramebufferRenderTarget.cs:175, bindings/Avalonia.Skia/ImmutableBitmap.cs:132, bindings/Avalonia.Skia/ImmutableBitmap.cs:53, bindings/Avalonia.Skia/WriteableBitmapImpl.cs:104
- Total references captured: 15

### SKBitmapReleaseDelegate

- Shim definition: _missing_
- Avalonia usage files (1): bindings/Avalonia.Skia/WriteableBitmapImpl.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites: _none detected_
- Total references captured: 1

### SKBlendMode

- Shim definition: bindings/VelloSharp.Skia.Core/SKBlendMode.cs
- Avalonia usage files (2): bindings/Avalonia.Skia/DrawingContextImpl.cs, bindings/Avalonia.Skia/SkiaSharpExtensions.cs
- Members invoked:
  - `Color` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:63 (_shim: present_)
  - `ColorBurn` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:55 (_shim: present_)
  - `ColorDodge` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:54 (_shim: present_)
  - `Darken` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:52 (_shim: present_)
  - `Difference` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:58 (_shim: present_)
  - `Dst` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:43 (_shim: present_)
  - `DstATop` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:47 (_shim: present_)
  - `DstIn` — 2 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:826 (_shim: present_)
  - `DstOut` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:45 (_shim: present_)
  - `DstOver` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:46 (_shim: present_)
  - `Exclusion` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:59 (_shim: present_)
  - `HardLight` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:56 (_shim: present_)
  - `Hue` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:61 (_shim: present_)
  - `Lighten` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:53 (_shim: present_)
  - `Luminosity` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:64 (_shim: present_)
  - `Multiply` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:60 (_shim: present_)
  - `Overlay` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:51 (_shim: present_)
  - `Plus` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:49 (_shim: present_)
  - `Saturation` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:62 (_shim: present_)
  - `Screen` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:50 (_shim: present_)
  - `SoftLight` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:57 (_shim: present_)
  - `Src` — 2 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:1345 (_shim: present_)
  - `SrcATop` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:42 (_shim: present_)
  - `SrcIn` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:40 (_shim: present_)
  - `SrcOut` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:41 (_shim: present_)
  - `SrcOver` — 2 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:37 (_shim: present_)
  - `Xor` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:48 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 31

### SKCacheBase

- Shim definition: _missing_
- Avalonia usage files (4): bindings/Avalonia.Skia/SKCacheBase.cs, bindings/Avalonia.Skia/SKPaintCache.cs, bindings/Avalonia.Skia/SKRoundRectCache.cs, bindings/Avalonia.Skia/SKTextBlobBuilderCache.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites: _none detected_
- Total references captured: 5

### SKCanvas

- Shim definition: bindings/VelloSharp.Skia.Core/SKCanvas.cs
- Avalonia usage files (7): bindings/Avalonia.Skia/DrawingContextImpl.cs, bindings/Avalonia.Skia/Gpu/ISkiaGpu.cs, bindings/Avalonia.Skia/Gpu/OpenGl/FboSkiaSurface.cs, bindings/Avalonia.Skia/Helpers/DrawingContextHelper.cs, bindings/Avalonia.Skia/ISkiaSharpApiLeaseFeature.cs, bindings/Avalonia.Skia/SurfaceRenderTarget.cs, bindings/Avalonia.Skia/WriteableBitmapImpl.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites: _none detected_
- Total references captured: 16

### SKClipOperation

- Shim definition: bindings/VelloSharp.Skia.Core/SKClipOperation.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/DrawingContextImpl.cs
- Members invoked:
  - `Difference` — 1 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:340 (_shim: present_)
  - `Intersect` — 2 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:340 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 4

### SKCodec

- Shim definition: bindings/VelloSharp.Skia.Core/SKCodec.cs
- Avalonia usage files (2): bindings/Avalonia.Skia/ImmutableBitmap.cs, bindings/Avalonia.Skia/WriteableBitmapImpl.cs
- Members invoked:
  - `Create` — 2 site(s), e.g. bindings/Avalonia.Skia/ImmutableBitmap.cs:68 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 2

### SKColor

- Shim definition: bindings/VelloSharp.Skia.Core/SKColor.cs
- Avalonia usage files (4): bindings/Avalonia.Skia/DrawingContextImpl.Effects.cs, bindings/Avalonia.Skia/DrawingContextImpl.cs, bindings/Avalonia.Skia/SkiaSharpExtensions.cs, bindings/Avalonia.Skia/WriteableBitmapImpl.cs
- Members invoked:
  - `Empty` — 2 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:1205 (_shim: missing_)
- Constructor/`new` call sites (10): bindings/Avalonia.Skia/DrawingContextImpl.Effects.cs:42, bindings/Avalonia.Skia/DrawingContextImpl.cs:1324, bindings/Avalonia.Skia/DrawingContextImpl.cs:1336, bindings/Avalonia.Skia/DrawingContextImpl.cs:1369, bindings/Avalonia.Skia/DrawingContextImpl.cs:1374, bindings/Avalonia.Skia/DrawingContextImpl.cs:1415, bindings/Avalonia.Skia/DrawingContextImpl.cs:252, bindings/Avalonia.Skia/DrawingContextImpl.cs:333, bindings/Avalonia.Skia/DrawingContextImpl.cs:987, bindings/Avalonia.Skia/SkiaSharpExtensions.cs:189
- Total references captured: 15

### SKColorF

- Shim definition: bindings/VelloSharp.Skia.Core/Primitives/SKPrimitives.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/DrawingContextImpl.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites (2): bindings/Avalonia.Skia/DrawingContextImpl.cs:719, bindings/Avalonia.Skia/DrawingContextImpl.cs:723
- Total references captured: 2

### SKColorFilter

- Shim definition: bindings/VelloSharp.Skia.Core/SKColorFilter.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/DrawingContextImpl.cs
- Members invoked:
  - `CreateTable` — 1 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:1298 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 2

### SKColorSpace

- Shim definition: bindings/VelloSharp.Skia.Core/SKColorSpace.cs
- Avalonia usage files (2): bindings/Avalonia.Skia/Gpu/Vulkan/VulkanSkiaExternalObjectsFeature.cs, bindings/Avalonia.Skia/Gpu/Vulkan/VulkanSkiaRenderTarget.cs
- Members invoked:
  - `CreateSrgb` — 2 site(s), e.g. bindings/Avalonia.Skia/Gpu/Vulkan/VulkanSkiaExternalObjectsFeature.cs:101 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 2

### SKColorType

- Shim definition: bindings/VelloSharp.Skia.Core/SKImageInfo.cs
- Avalonia usage files (12): bindings/Avalonia.Skia/FramebufferRenderTarget.cs, bindings/Avalonia.Skia/Gpu/Metal/SkiaMetalGpu.cs, bindings/Avalonia.Skia/Gpu/OpenGl/FboSkiaSurface.cs, bindings/Avalonia.Skia/Gpu/OpenGl/GlRenderTarget.cs, bindings/Avalonia.Skia/Gpu/OpenGl/GlSkiaExternalObjectsFeature.cs, bindings/Avalonia.Skia/Gpu/Vulkan/VulkanSkiaExternalObjectsFeature.cs, bindings/Avalonia.Skia/Gpu/Vulkan/VulkanSkiaRenderTarget.cs, bindings/Avalonia.Skia/Helpers/PixelFormatHelper.cs, bindings/Avalonia.Skia/ImmutableBitmap.cs, bindings/Avalonia.Skia/RenderTargetBitmapImpl.cs, bindings/Avalonia.Skia/SkiaSharpExtensions.cs, bindings/Avalonia.Skia/WriteableBitmapImpl.cs
- Members invoked:
  - `Bgra8888` — 8 site(s), e.g. bindings/Avalonia.Skia/Gpu/Metal/SkiaMetalGpu.cs:81 (_shim: present_)
  - `Rgb565` — 3 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:195 (_shim: present_)
  - `Rgb888x` — 2 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:201 (_shim: present_)
  - `Rgba8888` — 13 site(s), e.g. bindings/Avalonia.Skia/Gpu/OpenGl/FboSkiaSurface.cs:93 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 32

### SKCubicResampler

- Shim definition: bindings/VelloSharp.Skia.Core/SKSamplingPrimitives.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/SkiaSharpExtensions.cs
- Members invoked:
  - `Mitchell` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:27 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 1

### SKData

- Shim definition: bindings/VelloSharp.Skia.Core/IO/SKData.cs
- Avalonia usage files (2): bindings/Avalonia.Skia/ImmutableBitmap.cs, bindings/Avalonia.Skia/WriteableBitmapImpl.cs
- Members invoked:
  - `Create` — 4 site(s), e.g. bindings/Avalonia.Skia/ImmutableBitmap.cs:26 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 4

### SKDocument

- Shim definition: bindings/VelloSharp.Skia.Core/SKDocument.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/Helpers/DrawingContextHelper.cs
- Members invoked:
  - `BeginPage` — 2 site(s), e.g. bindings/Avalonia.Skia/Helpers/DrawingContextHelper.cs:22 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 2

### SKEncodedImageFormat

- Shim definition: bindings/VelloSharp.Skia.Core/SKEncodedImageFormat.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/Helpers/ImageSavingHelper.cs
- Members invoked:
  - `Png` — 1 site(s), e.g. bindings/Avalonia.Skia/Helpers/ImageSavingHelper.cs:57 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 1

### SKFilterMode

- Shim definition: bindings/VelloSharp.Skia.Core/SKSamplingPrimitives.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/SkiaSharpExtensions.cs
- Members invoked:
  - `Linear` — 3 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:22 (_shim: present_)
  - `Nearest` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:20 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 4

### SKFont

- Shim definition: bindings/VelloSharp.Skia.Core/SKFont.cs
- Avalonia usage files (2): bindings/Avalonia.Skia/GlyphRunImpl.cs, bindings/Avalonia.Skia/GlyphTypefaceImpl.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites: _none detected_
- Total references captured: 2

### SKFontEdging

- Shim definition: bindings/VelloSharp.Skia.Core/SKFont.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/GlyphRunImpl.cs
- Members invoked:
  - `Alias` — 3 site(s), e.g. bindings/Avalonia.Skia/GlyphRunImpl.cs:104 (_shim: present_)
  - `Antialias` — 1 site(s), e.g. bindings/Avalonia.Skia/GlyphRunImpl.cs:107 (_shim: present_)
  - `SubpixelAntialias` — 4 site(s), e.g. bindings/Avalonia.Skia/GlyphRunImpl.cs:110 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 9

### SKFontHinting

- Shim definition: bindings/VelloSharp.Skia.Core/SKFont.cs
- Avalonia usage files (2): bindings/Avalonia.Skia/GlyphRunImpl.cs, bindings/Avalonia.Skia/PlatformRenderInterface.cs
- Members invoked:
  - `Full` — 1 site(s), e.g. bindings/Avalonia.Skia/GlyphRunImpl.cs:139 (_shim: present_)
  - `None` — 1 site(s), e.g. bindings/Avalonia.Skia/PlatformRenderInterface.cs:93 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 2

### SKFontManager

- Shim definition: bindings/VelloSharp.Skia.Core/SKFontManager.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/FontManagerImpl.cs
- Members invoked:
  - `CreateDefault` — 1 site(s), e.g. bindings/Avalonia.Skia/FontManagerImpl.cs:25 (_shim: present_)
  - `Default` — 1 site(s), e.g. bindings/Avalonia.Skia/FontManagerImpl.cs:14 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 2

### SKFontStyle

- Shim definition: bindings/VelloSharp.Skia.Core/SKFontManager.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/FontManagerImpl.cs
- Members invoked:
  - `Bold` — 1 site(s), e.g. bindings/Avalonia.Skia/FontManagerImpl.cs:47 (_shim: present_)
  - `BoldItalic` — 1 site(s), e.g. bindings/Avalonia.Skia/FontManagerImpl.cs:50 (_shim: present_)
  - `Italic` — 1 site(s), e.g. bindings/Avalonia.Skia/FontManagerImpl.cs:44 (_shim: present_)
  - `Normal` — 1 site(s), e.g. bindings/Avalonia.Skia/FontManagerImpl.cs:41 (_shim: present_)
- Constructor/`new` call sites (2): bindings/Avalonia.Skia/FontManagerImpl.cs:53, bindings/Avalonia.Skia/FontManagerImpl.cs:81
- Total references captured: 7

### SKFontStyleSlant

- Shim definition: bindings/VelloSharp.Skia.Core/SKFontManager.cs
- Avalonia usage files (2): bindings/Avalonia.Skia/FontManagerImpl.cs, bindings/Avalonia.Skia/SkiaSharpExtensions.cs
- Members invoked:
  - `Italic` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:309 (_shim: present_)
  - `Oblique` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:310 (_shim: present_)
  - `Upright` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:308 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 6

### SKFontStyleWeight

- Shim definition: bindings/VelloSharp.Skia.Core/SKFontManager.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/FontManagerImpl.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites: _none detected_
- Total references captured: 2

### SKFontStyleWidth

- Shim definition: bindings/VelloSharp.Skia.Core/SKFontManager.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/FontManagerImpl.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites: _none detected_
- Total references captured: 2

### SKImage

- Shim definition: bindings/VelloSharp.Skia.Core/SKImage.cs
- Avalonia usage files (4): bindings/Avalonia.Skia/Helpers/ImageSavingHelper.cs, bindings/Avalonia.Skia/ImmutableBitmap.cs, bindings/Avalonia.Skia/SurfaceRenderTarget.cs, bindings/Avalonia.Skia/WriteableBitmapImpl.cs
- Members invoked:
  - `FromBitmap` — 4 site(s), e.g. bindings/Avalonia.Skia/ImmutableBitmap.cs:107 (_shim: present_)
  - `FromPicture` — 1 site(s), e.g. bindings/Avalonia.Skia/Helpers/ImageSavingHelper.cs:70 (_shim: present_)
  - `FromPixels` — 1 site(s), e.g. bindings/Avalonia.Skia/WriteableBitmapImpl.cs:184 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 13

### SKImageCachingHint

- Shim definition: bindings/VelloSharp.Skia.Core/SKImage.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/FramebufferRenderTarget.cs
- Members invoked:
  - `Disallow` — 1 site(s), e.g. bindings/Avalonia.Skia/FramebufferRenderTarget.cs:226 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 1

### SKImageFilter

- Shim definition: bindings/VelloSharp.Skia.Core/SKImageFilter.cs
- Avalonia usage files (2): bindings/Avalonia.Skia/DrawingContextImpl.Effects.cs, bindings/Avalonia.Skia/DrawingContextImpl.cs
- Members invoked:
  - `CreateBlur` — 2 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.Effects.cs:33 (_shim: present_)
  - `CreateDropShadow` — 1 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.Effects.cs:44 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 6

### SKImageInfo

- Shim definition: bindings/VelloSharp.Skia.Core/SKImageInfo.cs
- Avalonia usage files (7): bindings/Avalonia.Skia/FramebufferRenderTarget.cs, bindings/Avalonia.Skia/Helpers/PixelFormatHelper.cs, bindings/Avalonia.Skia/ImmutableBitmap.cs, bindings/Avalonia.Skia/PlatformRenderInterface.cs, bindings/Avalonia.Skia/RenderTargetBitmapImpl.cs, bindings/Avalonia.Skia/SurfaceRenderTarget.cs, bindings/Avalonia.Skia/WriteableBitmapImpl.cs
- Members invoked:
  - `PlatformColorType` — 3 site(s), e.g. bindings/Avalonia.Skia/Helpers/PixelFormatHelper.cs:19 (_shim: present_)
- Constructor/`new` call sites (11): bindings/Avalonia.Skia/FramebufferRenderTarget.cs:68, bindings/Avalonia.Skia/ImmutableBitmap.cs:135, bindings/Avalonia.Skia/ImmutableBitmap.cs:52, bindings/Avalonia.Skia/ImmutableBitmap.cs:76, bindings/Avalonia.Skia/ImmutableBitmap.cs:90, bindings/Avalonia.Skia/ImmutableBitmap.cs:94, bindings/Avalonia.Skia/SurfaceRenderTarget.cs:192, bindings/Avalonia.Skia/WriteableBitmapImpl.cs:106, bindings/Avalonia.Skia/WriteableBitmapImpl.cs:56, bindings/Avalonia.Skia/WriteableBitmapImpl.cs:67, bindings/Avalonia.Skia/WriteableBitmapImpl.cs:71
- Total references captured: 22

### SKManagedStream

- Shim definition: bindings/VelloSharp.Skia.Core/IO/SKManagedStream.cs
- Avalonia usage files (2): bindings/Avalonia.Skia/ImmutableBitmap.cs, bindings/Avalonia.Skia/WriteableBitmapImpl.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites (4): bindings/Avalonia.Skia/ImmutableBitmap.cs:24, bindings/Avalonia.Skia/ImmutableBitmap.cs:66, bindings/Avalonia.Skia/WriteableBitmapImpl.cs:29, bindings/Avalonia.Skia/WriteableBitmapImpl.cs:46
- Total references captured: 4

### SKMatrix

- Shim definition: bindings/VelloSharp.Skia.Core/Primitives/SKPrimitives.cs
- Avalonia usage files (3): bindings/Avalonia.Skia/DrawingContextImpl.cs, bindings/Avalonia.Skia/Helpers/ImageSavingHelper.cs, bindings/Avalonia.Skia/SkiaSharpExtensions.cs
- Members invoked:
  - `Concat` — 1 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:1120 (_shim: present_)
  - `CreateIdentity` — 1 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:1098 (_shim: present_)
  - `CreateRotationDegrees` — 1 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:1036 (_shim: present_)
  - `CreateScale` — 2 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:1123 (_shim: present_)
  - `CreateTranslation` — 2 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:1097 (_shim: present_)
- Constructor/`new` call sites (1): bindings/Avalonia.Skia/SkiaSharpExtensions.cs:136
- Total references captured: 16

### SKMatrix44

- Shim definition: bindings/VelloSharp.Skia.Core/Primitives/SKPrimitives.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/SkiaSharpExtensions.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites (1): bindings/Avalonia.Skia/SkiaSharpExtensions.cs:154
- Total references captured: 3

### SKMatrix4x4

- Shim definition: bindings/VelloSharp.Skia.Core/Primitives/SKPrimitives.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/DrawingContextImpl.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites: _none detected_
- Total references captured: 2

### SKMipmapMode

- Shim definition: bindings/VelloSharp.Skia.Core/SKSamplingPrimitives.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/SkiaSharpExtensions.cs
- Members invoked:
  - `Linear` — 2 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:24 (_shim: present_)
  - `None` — 2 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:20 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 4

### SKPaint

- Shim definition: bindings/VelloSharp.Skia.Core/SKPaint.cs
- Avalonia usage files (7): bindings/Avalonia.Skia/DrawingContextImpl.cs, bindings/Avalonia.Skia/IDrawableBitmapImpl.cs, bindings/Avalonia.Skia/ImmutableBitmap.cs, bindings/Avalonia.Skia/SKPaintCache.cs, bindings/Avalonia.Skia/SKRoundRectCache.cs, bindings/Avalonia.Skia/SurfaceRenderTarget.cs, bindings/Avalonia.Skia/WriteableBitmapImpl.cs
- Members invoked:
  - `Reset` — 1 site(s), e.g. bindings/Avalonia.Skia/SKPaintCache.cs:17 (_shim: present_)
- Constructor/`new` call sites (2): bindings/Avalonia.Skia/DrawingContextImpl.cs:719, bindings/Avalonia.Skia/DrawingContextImpl.cs:723
- Total references captured: 24

### SKPaintCache

- Shim definition: _missing_
- Avalonia usage files (4): bindings/Avalonia.Skia/DrawingContextImpl.Effects.cs, bindings/Avalonia.Skia/DrawingContextImpl.cs, bindings/Avalonia.Skia/Helpers/SKPathHelper.cs, bindings/Avalonia.Skia/SKPaintCache.cs
- Members invoked:
  - `Shared` — 16 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.Effects.cs:14 (_shim: missing_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 17

### SKPaints

- Shim definition: _missing_
- Avalonia usage files (1): bindings/Avalonia.Skia/SKPaintCache.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites: _none detected_
- Total references captured: 1

### SKPath

- Shim definition: bindings/VelloSharp.Skia.Core/SKPath.cs
- Avalonia usage files (11): bindings/Avalonia.Skia/CombinedGeometryImpl.cs, bindings/Avalonia.Skia/EllipseGeometryImpl.cs, bindings/Avalonia.Skia/GeometryGroupImpl.cs, bindings/Avalonia.Skia/GeometryImpl.cs, bindings/Avalonia.Skia/Helpers/SKPathHelper.cs, bindings/Avalonia.Skia/LineGeometryImpl.cs, bindings/Avalonia.Skia/PlatformRenderInterface.cs, bindings/Avalonia.Skia/RectangleGeometryImpl.cs, bindings/Avalonia.Skia/SkiaSharpExtensions.cs, bindings/Avalonia.Skia/StreamGeometryImpl.cs, bindings/Avalonia.Skia/TransformedGeometryImpl.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites (12): bindings/Avalonia.Skia/EllipseGeometryImpl.cs:16, bindings/Avalonia.Skia/GeometryGroupImpl.cs:18, bindings/Avalonia.Skia/GeometryGroupImpl.cs:39, bindings/Avalonia.Skia/GeometryImpl.cs:100, bindings/Avalonia.Skia/GeometryImpl.cs:150, bindings/Avalonia.Skia/Helpers/SKPathHelper.cs:20, bindings/Avalonia.Skia/Helpers/SKPathHelper.cs:62, bindings/Avalonia.Skia/LineGeometryImpl.cs:17, bindings/Avalonia.Skia/PlatformRenderInterface.cs:95, bindings/Avalonia.Skia/RectangleGeometryImpl.cs:16, bindings/Avalonia.Skia/SkiaSharpExtensions.cs:318, bindings/Avalonia.Skia/StreamGeometryImpl.cs:72
- Total references captured: 49

### SKPathArcSize

- Shim definition: bindings/VelloSharp.Skia.Core/SKPathPrimitives.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/StreamGeometryImpl.cs
- Members invoked:
  - `Large` — 2 site(s), e.g. bindings/Avalonia.Skia/StreamGeometryImpl.cs:127 (_shim: present_)
  - `Small` — 2 site(s), e.g. bindings/Avalonia.Skia/StreamGeometryImpl.cs:127 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 2

### SKPathDirection

- Shim definition: bindings/VelloSharp.Skia.Core/SKPathPrimitives.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/StreamGeometryImpl.cs
- Members invoked:
  - `Clockwise` — 2 site(s), e.g. bindings/Avalonia.Skia/StreamGeometryImpl.cs:129 (_shim: present_)
  - `CounterClockwise` — 2 site(s), e.g. bindings/Avalonia.Skia/StreamGeometryImpl.cs:130 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 4

### SKPathEffect

- Shim definition: bindings/VelloSharp.Skia.Core/SKPathEffect.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/Helpers/DrawingContextHelper.cs
- Members invoked:
  - `CreateDash` — 1 site(s), e.g. bindings/Avalonia.Skia/Helpers/DrawingContextHelper.cs:73 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 2

### SKPathFillType

- Shim definition: bindings/VelloSharp.Skia.Core/SKPathPrimitives.cs
- Avalonia usage files (2): bindings/Avalonia.Skia/GeometryGroupImpl.cs, bindings/Avalonia.Skia/StreamGeometryImpl.cs
- Members invoked:
  - `EvenOdd` — 3 site(s), e.g. bindings/Avalonia.Skia/GeometryGroupImpl.cs:15 (_shim: present_)
  - `Winding` — 2 site(s), e.g. bindings/Avalonia.Skia/GeometryGroupImpl.cs:15 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 3

### SKPathHelper

- Shim definition: _missing_
- Avalonia usage files (2): bindings/Avalonia.Skia/GeometryImpl.cs, bindings/Avalonia.Skia/Helpers/SKPathHelper.cs
- Members invoked:
  - `CreateClosedPath` — 1 site(s), e.g. bindings/Avalonia.Skia/GeometryImpl.cs:96 (_shim: missing_)
  - `CreateStrokedPath` — 2 site(s), e.g. bindings/Avalonia.Skia/GeometryImpl.cs:200 (_shim: missing_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 4

### SKPathMeasure

- Shim definition: bindings/VelloSharp.Skia.Core/SKPathMeasure.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/GeometryImpl.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites (1): bindings/Avalonia.Skia/GeometryImpl.cs:20
- Total references captured: 2

### SKPathOp

- Shim definition: bindings/VelloSharp.Skia.Core/SKPathPrimitives.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/CombinedGeometryImpl.cs
- Members invoked:
  - `Difference` — 1 site(s), e.g. bindings/Avalonia.Skia/CombinedGeometryImpl.cs:41 (_shim: present_)
  - `Intersect` — 1 site(s), e.g. bindings/Avalonia.Skia/CombinedGeometryImpl.cs:39 (_shim: present_)
  - `Union` — 1 site(s), e.g. bindings/Avalonia.Skia/CombinedGeometryImpl.cs:42 (_shim: present_)
  - `Xor` — 1 site(s), e.g. bindings/Avalonia.Skia/CombinedGeometryImpl.cs:40 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 4

### SKPathVerb

- Shim definition: bindings/VelloSharp.Skia.Core/SKPathPrimitives.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/Helpers/SKPathHelper.cs
- Members invoked:
  - `Close` — 1 site(s), e.g. bindings/Avalonia.Skia/Helpers/SKPathHelper.cs:27 (_shim: present_)
  - `Conic` — 1 site(s), e.g. bindings/Avalonia.Skia/Helpers/SKPathHelper.cs:33 (_shim: present_)
  - `Cubic` — 1 site(s), e.g. bindings/Avalonia.Skia/Helpers/SKPathHelper.cs:31 (_shim: present_)
  - `Done` — 1 site(s), e.g. bindings/Avalonia.Skia/Helpers/SKPathHelper.cs:21 (_shim: present_)
  - `Line` — 1 site(s), e.g. bindings/Avalonia.Skia/Helpers/SKPathHelper.cs:25 (_shim: present_)
  - `Move` — 1 site(s), e.g. bindings/Avalonia.Skia/Helpers/SKPathHelper.cs:23 (_shim: present_)
  - `Quad` — 1 site(s), e.g. bindings/Avalonia.Skia/Helpers/SKPathHelper.cs:29 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 8

### SKPicture

- Shim definition: bindings/VelloSharp.Skia.Core/SKPicture.cs
- Avalonia usage files (3): bindings/Avalonia.Skia/DrawingContextImpl.cs, bindings/Avalonia.Skia/Helpers/ImageSavingHelper.cs, bindings/Avalonia.Skia/PictureRenderTarget.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites: _none detected_
- Total references captured: 4

### SKPictureRecorder

- Shim definition: bindings/VelloSharp.Skia.Core/SKPictureRecorder.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/PictureRenderTarget.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites (1): bindings/Avalonia.Skia/PictureRenderTarget.cs:33
- Total references captured: 1

### SKPixelGeometry

- Shim definition: bindings/VelloSharp.Skia.Core/SKPixelGeometry.cs
- Avalonia usage files (4): bindings/Avalonia.Skia/FramebufferRenderTarget.cs, bindings/Avalonia.Skia/Gpu/OpenGl/FboSkiaSurface.cs, bindings/Avalonia.Skia/Gpu/OpenGl/GlRenderTarget.cs, bindings/Avalonia.Skia/SurfaceRenderTarget.cs
- Members invoked:
  - `RgbHorizontal` — 6 site(s), e.g. bindings/Avalonia.Skia/FramebufferRenderTarget.cs:129 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 6

### SKPoint

- Shim definition: bindings/VelloSharp.Skia.Core/SKGeometry.cs
- Avalonia usage files (4): bindings/Avalonia.Skia/GlyphRunImpl.cs, bindings/Avalonia.Skia/Helpers/SKPathHelper.cs, bindings/Avalonia.Skia/SKRoundRectCache.cs, bindings/Avalonia.Skia/SkiaSharpExtensions.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites (6): bindings/Avalonia.Skia/GlyphRunImpl.cs:44, bindings/Avalonia.Skia/GlyphRunImpl.cs:55, bindings/Avalonia.Skia/Helpers/SKPathHelper.cs:19, bindings/Avalonia.Skia/SKRoundRectCache.cs:34, bindings/Avalonia.Skia/SkiaSharpExtensions.cs:71, bindings/Avalonia.Skia/SkiaSharpExtensions.cs:76
- Total references captured: 12

### SKRect

- Shim definition: bindings/VelloSharp.Skia.Core/SKGeometry.cs
- Avalonia usage files (9): bindings/Avalonia.Skia/DrawingContextImpl.cs, bindings/Avalonia.Skia/GlyphRunImpl.cs, bindings/Avalonia.Skia/IDrawableBitmapImpl.cs, bindings/Avalonia.Skia/ImmutableBitmap.cs, bindings/Avalonia.Skia/PictureRenderTarget.cs, bindings/Avalonia.Skia/SKRoundRectCache.cs, bindings/Avalonia.Skia/SkiaSharpExtensions.cs, bindings/Avalonia.Skia/SurfaceRenderTarget.cs, bindings/Avalonia.Skia/WriteableBitmapImpl.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites (4): bindings/Avalonia.Skia/DrawingContextImpl.cs:1263, bindings/Avalonia.Skia/PictureRenderTarget.cs:34, bindings/Avalonia.Skia/SkiaSharpExtensions.cs:81, bindings/Avalonia.Skia/SkiaSharpExtensions.cs:86
- Total references captured: 18

### SKRectI

- Shim definition: bindings/VelloSharp.Skia.Core/Primitives/SKPrimitives.cs
- Avalonia usage files (2): bindings/Avalonia.Skia/SkiaRegionImpl.cs, bindings/Avalonia.Skia/SkiaSharpExtensions.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites (3): bindings/Avalonia.Skia/SkiaRegionImpl.cs:52, bindings/Avalonia.Skia/SkiaSharpExtensions.cs:91, bindings/Avalonia.Skia/SkiaSharpExtensions.cs:96
- Total references captured: 7

### SKRegion

- Shim definition: bindings/VelloSharp.Skia.Core/SKRegion.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/SkiaRegionImpl.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites: _none detected_
- Total references captured: 2

### SKRegionOperation

- Shim definition: bindings/VelloSharp.Skia.Core/SKRegionOperation.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/SkiaRegionImpl.cs
- Members invoked:
  - `Union` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaRegionImpl.cs:23 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 1

### SKRoundRect

- Shim definition: bindings/VelloSharp.Skia.Core/SKRoundRect.cs
- Avalonia usage files (3): bindings/Avalonia.Skia/DrawingContextImpl.cs, bindings/Avalonia.Skia/SKRoundRectCache.cs, bindings/Avalonia.Skia/SkiaSharpExtensions.cs
- Members invoked:
  - `SetEmpty` — 1 site(s), e.g. bindings/Avalonia.Skia/SKRoundRectCache.cs:77 (_shim: present_)
- Constructor/`new` call sites (4): bindings/Avalonia.Skia/DrawingContextImpl.cs:510, bindings/Avalonia.Skia/SKRoundRectCache.cs:28, bindings/Avalonia.Skia/SKRoundRectCache.cs:64, bindings/Avalonia.Skia/SkiaSharpExtensions.cs:102
- Total references captured: 14

### SKRoundRectCache

- Shim definition: _missing_
- Avalonia usage files (2): bindings/Avalonia.Skia/DrawingContextImpl.cs, bindings/Avalonia.Skia/SKRoundRectCache.cs
- Members invoked:
  - `Shared` — 10 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:384 (_shim: missing_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 12

### SKSamplingOptions

- Shim definition: bindings/VelloSharp.Skia.Core/SKSamplingOptions.cs
- Avalonia usage files (5): bindings/Avalonia.Skia/IDrawableBitmapImpl.cs, bindings/Avalonia.Skia/ImmutableBitmap.cs, bindings/Avalonia.Skia/SkiaSharpExtensions.cs, bindings/Avalonia.Skia/SurfaceRenderTarget.cs, bindings/Avalonia.Skia/WriteableBitmapImpl.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites (5): bindings/Avalonia.Skia/SkiaSharpExtensions.cs:20, bindings/Avalonia.Skia/SkiaSharpExtensions.cs:22, bindings/Avalonia.Skia/SkiaSharpExtensions.cs:24, bindings/Avalonia.Skia/SkiaSharpExtensions.cs:27, bindings/Avalonia.Skia/SkiaSharpExtensions.cs:28
- Total references captured: 11

### SKShader

- Shim definition: bindings/VelloSharp.Skia.Core/SKShader.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/DrawingContextImpl.cs
- Members invoked:
  - `CreateBitmap` — 1 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:1331 (_shim: missing_)
  - `CreateColor` — 3 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:1014 (_shim: present_)
  - `CreateCompose` — 3 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:1013 (_shim: present_)
  - `CreateLinearGradient` — 2 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:902 (_shim: present_)
  - `CreateRadialGradient` — 2 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:954 (_shim: present_)
  - `CreateSweepGradient` — 1 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:1049 (_shim: present_)
  - `CreateTwoPointConicalGradient` — 2 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:1016 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 18

### SKShaderTileMode

- Shim definition: bindings/VelloSharp.Skia.Core/SKShader.cs
- Avalonia usage files (2): bindings/Avalonia.Skia/DrawingContextImpl.cs, bindings/Avalonia.Skia/SkiaSharpExtensions.cs
- Members invoked:
  - `Clamp` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:256 (_shim: present_)
  - `Decal` — 4 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:1102 (_shim: present_)
  - `Mirror` — 5 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:1104 (_shim: present_)
  - `Repeat` — 6 site(s), e.g. bindings/Avalonia.Skia/DrawingContextImpl.cs:1105 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 20

### SKSizeI

- Shim definition: bindings/VelloSharp.Skia.Core/Primitives/SKPrimitives.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/Helpers/ImageSavingHelper.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites (1): bindings/Avalonia.Skia/Helpers/ImageSavingHelper.cs:67
- Total references captured: 1

### SKStrokeCap

- Shim definition: bindings/VelloSharp.Skia.Core/SKPaint.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/SkiaSharpExtensions.cs
- Members invoked:
  - `Butt` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:279 (_shim: present_)
  - `Round` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:277 (_shim: present_)
  - `Square` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:278 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 4

### SKStrokeJoin

- Shim definition: bindings/VelloSharp.Skia.Core/SKPaint.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/SkiaSharpExtensions.cs
- Members invoked:
  - `Bevel` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:287 (_shim: present_)
  - `Miter` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:289 (_shim: present_)
  - `Round` — 1 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:288 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 4

### SKSurface

- Shim definition: bindings/VelloSharp.Skia.Core/SKSurface.cs
- Avalonia usage files (12): bindings/Avalonia.Skia/DrawingContextImpl.cs, bindings/Avalonia.Skia/FramebufferRenderTarget.cs, bindings/Avalonia.Skia/Gpu/ISkiaGpu.cs, bindings/Avalonia.Skia/Gpu/ISkiaGpuRenderSession.cs, bindings/Avalonia.Skia/Gpu/Metal/SkiaMetalGpu.cs, bindings/Avalonia.Skia/Gpu/OpenGl/FboSkiaSurface.cs, bindings/Avalonia.Skia/Gpu/OpenGl/GlRenderTarget.cs, bindings/Avalonia.Skia/Gpu/OpenGl/GlSkiaExternalObjectsFeature.cs, bindings/Avalonia.Skia/Gpu/Vulkan/VulkanSkiaExternalObjectsFeature.cs, bindings/Avalonia.Skia/Gpu/Vulkan/VulkanSkiaRenderTarget.cs, bindings/Avalonia.Skia/ISkiaSharpApiLeaseFeature.cs, bindings/Avalonia.Skia/SurfaceRenderTarget.cs
- Members invoked:
  - `Create` — 11 site(s), e.g. bindings/Avalonia.Skia/FramebufferRenderTarget.cs:128 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 35

### SKSurfaceProperties

- Shim definition: bindings/VelloSharp.Skia.Core/SKSurfaceProperties.cs
- Avalonia usage files (4): bindings/Avalonia.Skia/FramebufferRenderTarget.cs, bindings/Avalonia.Skia/Gpu/OpenGl/FboSkiaSurface.cs, bindings/Avalonia.Skia/Gpu/OpenGl/GlRenderTarget.cs, bindings/Avalonia.Skia/SurfaceRenderTarget.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites (6): bindings/Avalonia.Skia/FramebufferRenderTarget.cs:129, bindings/Avalonia.Skia/FramebufferRenderTarget.cs:187, bindings/Avalonia.Skia/Gpu/OpenGl/FboSkiaSurface.cs:94, bindings/Avalonia.Skia/Gpu/OpenGl/GlRenderTarget.cs:16, bindings/Avalonia.Skia/SurfaceRenderTarget.cs:87, bindings/Avalonia.Skia/SurfaceRenderTarget.cs:88
- Total references captured: 6

### SKTextAlign

- Shim definition: bindings/VelloSharp.Skia.Core/SKTextAlign.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/SkiaSharpExtensions.cs
- Members invoked:
  - `Center` — 2 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:268 (_shim: present_)
  - `Left` — 2 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:267 (_shim: present_)
  - `Right` — 2 site(s), e.g. bindings/Avalonia.Skia/SkiaSharpExtensions.cs:269 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 8

### SKTextBlob

- Shim definition: bindings/VelloSharp.Skia.Core/SKTextBlob.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/GlyphRunImpl.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites (1): bindings/Avalonia.Skia/GlyphRunImpl.cs:24
- Total references captured: 2

### SKTextBlobBuilder

- Shim definition: bindings/VelloSharp.Skia.Core/SKTextBlob.cs
- Avalonia usage files (1): bindings/Avalonia.Skia/SKTextBlobBuilderCache.cs
- Members invoked: _none captured (likely property/field usage only)_
- Constructor/`new` call sites: _none detected_
- Total references captured: 2

### SKTextBlobBuilderCache

- Shim definition: _missing_
- Avalonia usage files (2): bindings/Avalonia.Skia/GlyphRunImpl.cs, bindings/Avalonia.Skia/SKTextBlobBuilderCache.cs
- Members invoked:
  - `Shared` — 2 site(s), e.g. bindings/Avalonia.Skia/GlyphRunImpl.cs:118 (_shim: missing_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 3

### SKTypeface

- Shim definition: bindings/VelloSharp.Skia.Core/SKTypeface.cs
- Avalonia usage files (2): bindings/Avalonia.Skia/FontManagerImpl.cs, bindings/Avalonia.Skia/GlyphTypefaceImpl.cs
- Members invoked:
  - `Default` — 1 site(s), e.g. bindings/Avalonia.Skia/FontManagerImpl.cs:18 (_shim: present_)
  - `FromStream` — 1 site(s), e.g. bindings/Avalonia.Skia/FontManagerImpl.cs:110 (_shim: present_)
- Constructor/`new` call sites: _none detected_
- Total references captured: 4

