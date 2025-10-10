namespace VelloSharp.Editor;

/// <summary>
/// Provides initialization and status checks for the editor native core.
/// </summary>
public static class EditorRuntime
{
    private static readonly object SyncRoot = new();
    private static bool _initialized;

    /// <summary>
    /// Ensures the editor native core library is loaded and initialized.
    /// </summary>
    public static void EnsureInitialized()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The Vello editor native core is only available on Windows.");
        }

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

            if (!NativeMethods.vello_editor_core_initialize())
            {
                throw new InvalidOperationException("Failed to initialize vello_editor_core.");
            }

            _initialized = true;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the native editor core reports initialization.
    /// </summary>
    public static bool IsInitialized => _initialized && NativeMethods.vello_editor_core_is_initialized();
}
