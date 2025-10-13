# VelloSharp.Core

`VelloSharp.Core` delivers the foundational abstractions, math primitives, and utilities that power the higher-level rendering and visualization stacks.

## Getting Started

1. Install the package: `dotnet add package VelloSharp.Core`.
2. Add `using VelloSharp.Core;` wherever you work with shared types such as geometry helpers, memory utilities, or diagnostic hooks.
3. Reference the core types from your own libraries or applications to remain aligned with the rest of the VelloSharp ecosystem.
4. Keep the package updated alongside the other VelloSharp components to avoid version mismatches.

## Usage Example

```csharp
using System.Numerics;
using VelloSharp;

var brush = new LinearGradientBrush((0, 0), (0, 200),
    GradientStop.At(0f, RgbaColor.FromBytes(255, 0, 0, 255)),
    GradientStop.At(1f, RgbaColor.FromBytes(0, 0, 255, 255)));

var path = new PathBuilder()
    .MoveTo(32, 32)
    .QuadraticTo(128, 0, 224, 128)
    .Close();
```

## Next Steps

- Consult the API reference for details on the math primitives, diagnostics hooks, and configuration extensions.
- Review downstream packages (Charting, Composition, Editor) to see how the core abstractions are consumed in practice.

