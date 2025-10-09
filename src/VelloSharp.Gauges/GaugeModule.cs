namespace VelloSharp.Gauges;

/// <summary>
/// Entry point for initializing the native gauges bridge.
/// </summary>
public static class GaugeModule
{
    private static readonly object SyncRoot = new();
    private static bool _initialized;

    /// <summary>
    /// Ensures the gauges native library has been initialized.
    /// </summary>
    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_initialized)
            {
                return;
            }

            if (!NativeMethods.vello_gauges_initialize())
            {
                throw new InvalidOperationException("Failed to initialize vello_gauges_core.");
            }

            _initialized = true;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the native module reports initialization.
    /// </summary>
    public static bool IsInitialized => _initialized && NativeMethods.vello_gauges_is_initialized();
}
