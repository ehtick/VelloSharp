# Shared Graphics Abstractions Proposal

## Intent
Unify the way Avalonia integrations obtain devices, manage swapchains, and submit frames so that both the Vello (WebGPU) and Skia bindings can plug into a single pipeline. The abstractions below live in a new shared assembly (e.g., `VelloSharp.Avalonia.Core`) and are additive—`Avalonia.Skia` keeps linking upstream sources, while our custom entry points opt into these contracts.

## Type Overview
```csharp
namespace VelloSharp.Avalonia.Core;

public enum GraphicsBackendKind
{
    VelloWgpu,
    SkiaGpu,
    SkiaCpu,
}

/// <summary>Immutable options passed to device creation.</summary>
public sealed record GraphicsDeviceOptions(
    GraphicsBackendKind Backend,
    GraphicsFeatureSet Features,
    GraphicsPresentationOptions Presentation);

public sealed record GraphicsFeatureSet(
    bool EnableCpuFallback,
    bool EnableMsaa8,
    bool EnableMsaa16,
    bool EnableAreaAa,
    bool EnableOpacityLayers,
    long? MaxGpuResourceBytes);

public sealed record GraphicsPresentationOptions(
    PresentMode PresentMode,
    RgbaColor ClearColor,
    int SwapChainFps);
```

### Device Lifecycle
```csharp
public interface IGraphicsDeviceProvider : IDisposable
{
    GraphicsDeviceLease Acquire(GraphicsDeviceOptions options);
}

public readonly struct GraphicsDeviceLease : IDisposable
{
    public GraphicsBackendKind Backend { get; }
    public object PlatformDevice { get; }      // WgpuDevice, ISkiaGpu, etc.
    public object? AuxiliaryContext { get; }   // WgpuQueue, GRContext, etc.
    public GraphicsFeatureSet Features { get; }
    public void Dispose();
}
```
- Vello wraps its `WgpuInstance/Adapter/Device/Queue/Renderer` inside a lease implementation.
- Skia returns either a GPU `ISkiaGpu` or a CPU sentinel, together with an optional `GRContext`.

### Surface Ownership
```csharp
public readonly record struct SurfaceRequest(
    PixelSize PixelSize,
    double RenderScaling,
    object PlatformSurface); // e.g., INativePlatformHandleSurface or IVelloWinitSurfaceProvider

public interface IRenderSurfaceManager
{
    ValueTask<RenderSurfaceLease?> TryAcquireSurfaceAsync(
        GraphicsDeviceLease device,
        SurfaceRequest request,
        CancellationToken cancellationToken = default);
}

public abstract class RenderSurfaceLease : IDisposable
{
    public PixelSize PixelSize { get; protected init; }
    public double RenderScaling { get; protected init; }
    public abstract object GetRenderTarget(); // WgpuSurfaceTexture, ISkiaGpuRenderTarget, Framebuffer lock, etc.
    public abstract void Dispose();
}
```
- Vello implements an async manager that maps `SurfaceRequest.PlatformSurface` to `SurfaceHandle` instances and configures swapchains (mirrors `VelloSwapchainRenderTarget` logic).
- Skia’s manager adapts the same request into GPU or framebuffer render targets, reusing upstream helpers.
- Async surface creation allows us to honor macOS UI-thread requirements without leaking dispatcher usage into consumers.

### Render Submission
```csharp
public readonly record struct RenderSubmissionContext(
    GraphicsDeviceLease Device,
    RenderSurfaceLease Surface,
    RenderParams RenderParams,
    Matrix Transform);

public interface IRenderSubmission
{
    void SubmitScene(
        RenderSubmissionContext context,
        Scene scene,
        IReadOnlyList<Action<IGraphicsCommandEncoder>>? commandCallbacks = null);
}

public interface IGraphicsCommandEncoder
{
    GraphicsBackendKind Backend { get; }
    bool TryGetContext<TContext>(out TContext context);
}

public readonly struct WgpuCommandEncoderContext
{
    public WgpuDevice Device { get; init; }
    public WgpuQueue Queue { get; init; }
    public WgpuTextureView TargetView { get; init; }
    public WgpuTextureFormat Format { get; init; }
}
```
- `IRenderSubmission` encapsulates the per-frame composition path. Vello’s implementation defers to `WgpuRenderer.Render` and relays callbacks through `WgpuCommandEncoderContext`.
- Skia support is layered via an optional bridge that understands how to retrieve a `SkiaCommandEncoderContext` using the shared `TryGetContext` API.
- `IGraphicsCommandEncoder` exposes backend-specific contexts without forcing the shared assembly to depend on Skia; backend helpers provide strongly-typed extension methods.

## Usage Sketch
1. Avalonia integration resolves `IGraphicsDeviceProvider` and `IRenderSurfaceManager` from the shared module.
2. On frame render, the platform context creates a `SurfaceRequest`, acquires a surface lease (awaiting on macOS).
3. Scene recording stays backend-agnostic; once complete, the render loop calls `IRenderSubmission.SubmitScene`.
4. Optional command callbacks add native draw commands (compute passes, Skia interop) via the encoder API.

These contracts are intentionally slim: they describe *what* needs to happen, leaving each backend to decide *how*. They also keep existing Skia/Vello implementations free to reuse upstream code and comply with the goal of not editing imported sources. Future steps include finalizing namespaces, providing adapters, and writing smoke tests that validate both implementations against these interfaces.

Skia-specific encoder helpers now live in a separate add-on (`VelloSharp.Avalonia.SkiaBridge`) so the shared assembly remains Skia-free. The bridge contributes types such as `SkiaCommandEncoderContext` plus `TryEncodeSkia` extensions that call into the shared `IGraphicsCommandEncoder.TryGetContext` API.
