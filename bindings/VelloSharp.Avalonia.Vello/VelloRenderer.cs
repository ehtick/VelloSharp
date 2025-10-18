namespace VelloSharp.Avalonia.Vello;

/// <summary>
/// Public entry point for initializing the Vello Avalonia renderer.
/// </summary>
public static class VelloRenderer
{
    /// <summary>
    /// Initializes the Vello platform with the supplied options.
    /// </summary>
    public static void Initialize(VelloPlatformOptions options)
    {
        VelloPlatform.Initialize(options ?? new VelloPlatformOptions());
    }
}
