# VelloSharp.Gpu

`VelloSharp.Gpu` exposes GPU-specific helpers, resource management primitives, and extension points shared by the rendering backends.

## Getting Started

1. Install via `dotnet add package VelloSharp.Gpu`.
2. Bring the namespace into scope with `using VelloSharp.Gpu;`.
3. Use the package to configure device capabilities, upload buffers, or queue work for execution before handing frames to the renderer.
4. Combine it with `VelloSharp`, `VelloSharp.Skia.Gpu`, or platform integrations to complete the rendering loop.

## Usage Example

```csharp
using VelloSharp;

var request = AccessKitActionRequest.FromJson("{\"type\":\"Focus\",\"action_request_id\":1}");
using var document = request.ToJsonDocument();
Console.WriteLine(document.RootElement.GetProperty("type").GetString());
```

## Next Steps

- Review the API reference to understand the available abstractions for command encoding and resource pooling.
- Inspect the charting and composition packages to see how they consume the GPU helpers in layered architectures.

