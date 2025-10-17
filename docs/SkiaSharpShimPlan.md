# SkiaSharp Shim Alignment Plan

Note: After each task is implemented, mark its checkbox as checked.

1. [x] Codify the API baseline: catalogue every SkiaSharp type/member used by `bindings/Avalonia.Skia` (from build errors and source grep) and map each call site to the corresponding shim type in `bindings/VelloSharp.Skia.Core`.
2. [x] Extend geometry primitives to match SkiaSharp contracts:
   - make `SKPoint` setters writable,
   - implement `SKRect` helpers (`Union`, `Inflate`, `Offset` and overload parity),
   - wire the implementations to the existing Vello-backed structs with unit coverage.
3. [x] Expand `SKRoundRect` support: add constructors, `Radii` accessors, `Inflate`/`Deflate`, and any helper methods Avalonia consumes; ensure data flows cleanly to Vello round-rect primitives.
4. [x] Flesh out `SKPath` functionality to include `AddOval`, `AddRect`, `AddPath`, constructors, and curve helpers (`CubicTo`, `QuadTo` signatures) expected by Avalonia’s geometry pipelines.
5. [x] Implement missing `SKCanvas` members (`DrawLine`, `DrawRoundRect` overloads, `RestoreToCount`, `ClipRect/ClipRoundRect`, `DrawRegion`, `DrawPaint`, etc.) backed by our Vello command stream; add regression tests that exercise the new APIs.
6. [x] Cover image & shader utilities:
   - add `SKShader.CreateBitmap`, `SKImage.ToShader`, `SKImage.ToRasterImage`,
   - surface `SKPaint.ColorF`, `SKColor.Empty`, and align encoding APIs (`SKImage.Encode` overloads, `SKData.SaveTo`).
7. [x] Complete bitmap memory management: supply `SKBitmap` constructors, `InstallPixels` overloads, `CanCopyTo`, `NotifyPixelsChanged`, and ensure `WriteableBitmapImpl` interop works against the shim without manual marshalling.
8. [ ] Restore font/typography features: expose `SKTypeface.IsFixedPitch`, `SKTypeface.GlyphCount`, `SKFont.GetGlyphPath`, and confirm glyph metrics stay in sync with Vello’s font engine.
9. [x] Add GPU interop helpers:
   - mirror SkiaSharp’s GL format helpers (`SKColorType.ToGlSizedFormat`),
   - implement Metal/Vulkan wrapper constructors (`GRMtlTextureInfo`, surface descriptors) and ensure numeric types align (avoid manual casts in consumer code).
10. [x] Provide `SKSurface.Flush` and other surface lifecycle members that Avalonia’s GPU targets require; back them with the correct Vello flush semantics.
11. [x] Harden IO and encoding glue: hook `ImageSavingHelper` by supplying span-friendly overloads (`SKImage.Encode`, `SKData.SaveTo`) and ensure stream-based workflows function.
12. [ ] Run `dotnet build bindings/Avalonia.Skia/Avalonia.Skia.csproj` plus targeted smoke tests after each task, checking off the corresponding item once the code compiles cleanly and the new API surface is covered.
