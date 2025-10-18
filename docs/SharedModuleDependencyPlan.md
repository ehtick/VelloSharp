# Shared Module Dependency Plan

## Naming & Assembly Layout
- New assembly: `bindings/VelloSharp.Avalonia.Core/VelloSharp.Avalonia.Core.csproj`.
- Root namespace: `VelloSharp.Avalonia.Core`.
- Sub-namespaces mirror responsibility:
  - `.Device` → device provider implementations.
  - `.Surface` → surface managers, leases, and platform surface providers.
  - `.Rendering` → submission pipeline abstractions.
  - `.Options` → configuration records (`GraphicsFeatureSet`, etc.).

## Dependency Graph
```
Avalonia Application
        │
        ▼
VelloSharp.Avalonia.Core
   ├── VelloSharp.Avalonia.Vello (adapts WebGPU backend)
   └── Custom Avalonia.Skia bridge (new additive files)
        │
        └─ references extern/Avalonia Skia sources
```

- `VelloSharp.Avalonia.Core` references:
  - `Avalonia` (interfaces: `PixelSize`, `Matrix`, `Scene`).
  - `VelloSharp` (common types like `RgbaColor`, `RenderParams`).
  - `VelloSharp.Gpu` (WebGPU types used by the shared Wgpu encoder context).
- `VelloSharp.Avalonia.SkiaBridge` references the core plus Skia packages to provide Skia-specific contexts and extension helpers.
- `VelloSharp.Avalonia.Vello` references the core to implement adapters. No reverse reference is permitted.
- `bindings/Avalonia.Skia` stays unchanged. Instead, an adjacent project (e.g., `VelloSharp.Avalonia.SkiaBridge`) references both the core and `Avalonia.Skia` to provide the shared implementations, avoiding edits to the linked files.

## Circular Reference Avoidance
- Core assembly contains only abstractions and record types; backend-specific code lives in sibling projects.
- Core must not reference `VelloSharp.Avalonia.Vello` or the Skia bridge. Ensure project files enforce this by keeping the core’s project reference list limited to base packages (`Avalonia`, `VelloSharp`, `VelloSharp.Gpu`).
- Backend modules (`Vello`, `SkiaBridge`) consume the core and expose registrar helpers (`UseVelloBackend`, `UseSkiaBackend`), but they do not reference each other.
- `samples/ControlCatalog` references the desired backend(s) individually; if both are included, they share the core indirectly without a diamond dependency.

## Build & Packaging Notes
- Update `VelloSharp.sln` to include the new core project and its consumers.
- Ensure the core project targets the same frameworks as the consumers (likely `net8.0`) and uses `Nullable` + `ImplicitUsings` consistent with existing bindings.
- Keep Skia dependencies isolated to the bridge so platforms lacking SkiaSharp can consume the core assembly without additional targets; if necessary, guard the bridge with conditional compilation symbols (`USE_SKIA`).
