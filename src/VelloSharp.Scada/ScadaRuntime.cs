namespace VelloSharp.Scada;

/// <summary>
/// Provides initialization hooks for the SCADA native runtime.
/// </summary>
public static class ScadaRuntime
{
    private static readonly object SyncRoot = new();
    private static bool _initialized;

    /// <summary>
    /// Ensures the SCADA runtime library has been initialized.
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

            if (!NativeMethods.vello_scada_runtime_initialize())
            {
                throw new InvalidOperationException("Failed to initialize vello_scada_runtime.");
            }

            _initialized = true;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the native SCADA runtime reports initialization.
    /// </summary>
    public static bool IsInitialized => _initialized && NativeMethods.vello_scada_runtime_is_initialized();
}
