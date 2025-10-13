# VelloSharp.Text

`VelloSharp.Text` wraps text shaping, glyph management, and font resolution services so that higher layers can render typographically rich content with Vello.

## Getting Started

1. Install with `dotnet add package VelloSharp.Text`.
2. Import the namespace using `using VelloSharp.Text;` within components that format or render text.
3. Create a text context or resolver using the factories provided by the package, then inject it into your rendering or composition pipeline.
4. Pair the text services with the charting, gauges, or editor packages to display labels, annotations, and UI copy.

## Usage Example

```csharp
using VelloSharp.Text;

var options = VelloTextShaperOptions.CreateDefault(fontSize: 18f, isRightToLeft: false) with
{
    Features = new[] { new VelloOpenTypeFeature("liga", 1) },
    VariationAxes = new[] { new VelloVariationAxisValue("wght", 600f) },
};
```

## Next Steps

- Browse the API reference to learn about glyph caches, font collection management, and layout helpers.
- Check the samples that rely on text rendering (for example the charting demos) to see how the text services integrate end to end.

