# VelloSharp.Editor

`VelloSharp.Editor` powers the unified visual editor experience layered on top of the Vello rendering stack.

## Getting Started

1. Install via `dotnet add package VelloSharp.Editor`.
2. Add `using VelloSharp.Editor;` in the applications that host editing experiences.
3. Initialize the editor shell or document services exposed by the package, wiring in persistence, telemetry, or custom tools as needed.
4. Provide rendering surfaces through the platform integrations so the editor can display live previews of the composed scenes.

## Usage Example

```csharp
using VelloSharp.Editor;

EditorRuntime.EnsureInitialized();
```

## Next Steps

- Review the API reference for document models, command services, and extensibility hooks.
- Check the editor-related plans in the `docs/` folder to understand the roadmap and integration expectations.

