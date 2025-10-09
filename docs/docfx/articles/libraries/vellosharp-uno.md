# VelloSharp.Uno

`VelloSharp.Uno` integrates the Vello renderer with Uno Platform applications, letting you reuse the visual stack across Windows, WebAssembly, and mobile.

## Getting Started

1. Install the package: `dotnet add package VelloSharp.Uno`.
2. Add `using VelloSharp.Uno;` in your shared Uno project.
3. Register the provided services during Uno bootstrapping (for example in `App` or platform-specific startup) so that Vello surfaces can be created.
4. Combine the integration with the charting or composition layers to render visuals consistently across Uno-supported targets.

## Usage Example

```csharp
using VelloSharp.Uno.Controls;

var panel = new VelloSwapChainPanel();
((IVelloDiagnosticsProvider)panel).DiagnosticsUpdated += (_, e) =>
{
    Console.WriteLine($"GPU presentations: {e.Diagnostics.SwapChainPresentations}");
};
```

## Next Steps

- Review the API reference for platform-specific configuration switches and lifecycle callbacks.
- Validate the integration on each Uno target, paying special attention to WebAssembly and mobile where WebGL/OpenGL support can vary.

