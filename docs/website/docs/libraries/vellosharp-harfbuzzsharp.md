# VelloSharp.HarfBuzzSharp

`VelloSharp.HarfBuzzSharp` provides a HarfBuzzSharp-compatible façade backed by the managed Vello text pipeline. Existing
code that depends on `HarfBuzzSharp` can continue to shape text, but glyph discovery and layout now flow through
`VelloSharp.Text` and the shared font infrastructure.

## Getting Started

1. Install with `dotnet add package VelloSharp.HarfBuzzSharp`.
2. Reference both `VelloSharp.HarfBuzzSharp` and `VelloSharp.Text` so fonts and glyph metadata resolve correctly.
3. Replace direct `HarfBuzzSharp` package references with the shim, keeping namespaces (`HarfBuzzSharp`) unchanged.
4. Pair the shim with platform integrations (Avalonia, Skia, WinForms, or WPF) to render shaped glyph runs.

## Usage Example

```csharp
using HarfBuzzSharp;

using var blob = Blob.FromFile("Assets/Fonts/Roboto-Regular.ttf");
using var face = new Face(blob, 0);
using var font = new Font(face);

using var buffer = new Buffer();
buffer.AddUtf8("VelloSharp");
buffer.GuessSegmentProperties();

font.Shape(buffer);
```

## Next Steps

- Browse the API reference to see which HarfBuzzSharp entry points the shim covers today.
- Combine the shim with `VelloSharp.Integration` controls or the charting libraries to render complex text in real UI scenarios.
- Review the `VelloSharp.Text` guide for details on advanced OpenType features, fallback fonts, and glyph caches.

