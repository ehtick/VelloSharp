# Interop Contract Definitions

## Interfaces

```csharp
public interface IVelloSurfaceHost
{
    void AttachSurface(VelloSurfaceDescriptor descriptor);
    void Resize(Size logicalSize, DpiScale dpi);
    void RenderFrame(FrameTime time, RenderFlags flags = RenderFlags.None);
    void DetachSurface();
}

public interface IInputRouter
{
    void Enqueue(InputEvent inputEvent);
}

public interface IChartTelemetrySink
{
    void Record(FrameStats stats);
    void Record(ChartMetric metric);
}
```

## Threading Rules
- `AttachSurface`, `Resize`, and `DetachSurface` are invoked on the UI thread; `RenderFrame` may be called from UI or render thread depending on host requirements.
- `IInputRouter.Enqueue` is thread-safe and may be called from any thread receiving input events (e.g., pointer, keyboard, touch).
- `IChartTelemetrySink` implementations must be thread-safe; metrics may originate from render or data ingestion threads.

## Error Handling
- All methods return success via normal completion; recoverable issues throw framework-specific exceptions (e.g., `GraphicsDeviceLostException`).
- Hosts are responsible for retry policies and surface re-initialisation when GPU loss occurs.
