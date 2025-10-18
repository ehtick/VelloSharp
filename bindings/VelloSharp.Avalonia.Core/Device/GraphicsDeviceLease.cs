using VelloSharp.Avalonia.Core.Options;

namespace VelloSharp.Avalonia.Core.Device;

/// <summary>
/// Represents a leased rendering device and associated resources.
/// </summary>
public sealed class GraphicsDeviceLease : IDisposable
{
    private readonly IDisposable? _disposeTarget;
    private readonly Action? _disposeAction;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="GraphicsDeviceLease"/>.
    /// </summary>
    /// <param name="backend">Backend that produced the device.</param>
    /// <param name="platformDevice">Backend-specific device object.</param>
    /// <param name="auxiliaryContext">Optional companion context (queue, <c>GRContext</c>, etc.).</param>
    /// <param name="features">Feature set resolved for the device.</param>
    /// <param name="disposeTarget">Disposable invoked when the lease completes.</param>
    /// <param name="disposeAction">Action invoked before disposing <paramref name="disposeTarget"/>.</param>
    public GraphicsDeviceLease(
        GraphicsBackendKind backend,
        object platformDevice,
        object? auxiliaryContext,
        GraphicsFeatureSet features,
        IDisposable? disposeTarget = null,
        Action? disposeAction = null)
    {
        Backend = backend;
        PlatformDevice = platformDevice ?? throw new ArgumentNullException(nameof(platformDevice));
        AuxiliaryContext = auxiliaryContext;
        Features = features ?? throw new ArgumentNullException(nameof(features));
        _disposeTarget = disposeTarget;
        _disposeAction = disposeAction;
    }

    /// <summary>
    /// Gets the backend that produced the device.
    /// </summary>
    public GraphicsBackendKind Backend { get; }

    /// <summary>
    /// Gets the underlying device object (e.g., <see cref="WgpuDevice"/> or <c>ISkiaGpu</c>).
    /// </summary>
    public object PlatformDevice { get; }

    /// <summary>
    /// Gets an optional auxiliary context, such as a command queue or Skia GRContext.
    /// </summary>
    public object? AuxiliaryContext { get; }

    /// <summary>
    /// Gets the feature set resolved for the device.
    /// </summary>
    public GraphicsFeatureSet Features { get; }

    /// <summary>
    /// Attempts to retrieve the platform device as the specified type.
    /// </summary>
    public bool TryGetDevice<TDevice>([NotNullWhen(true)] out TDevice? device)
        where TDevice : class
    {
        device = PlatformDevice as TDevice;
        return device is not null;
    }

    /// <summary>
    /// Attempts to retrieve the auxiliary context as the specified type.
    /// </summary>
    public bool TryGetAuxiliaryContext<TContext>([NotNullWhen(true)] out TContext? context)
        where TContext : class
    {
        context = AuxiliaryContext as TContext;
        return context is not null;
    }

    /// <summary>
    /// Releases resources associated with the lease.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _disposeAction?.Invoke();
        }
        finally
        {
            _disposeTarget?.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
