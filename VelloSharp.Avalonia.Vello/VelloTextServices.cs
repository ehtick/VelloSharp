using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using VelloSharp.Avalonia.Vello.Rendering;

namespace VelloSharp.Avalonia.Vello;

/// <summary>
/// Provides helpers to register Vello-backed text services with Avalonia's service locator.
/// </summary>
public static class VelloTextServices
{
    private static readonly object s_sync = new();
    private static bool s_initialized;

    /// <summary>
    /// Ensures that Avalonia text services (`IFontManagerImpl`, `ITextShaperImpl`) are backed by the Vello implementations.
    /// Idempotent – repeated calls are safe.
    /// </summary>
    public static void Initialize()
    {
        lock (s_sync)
        {
            if (s_initialized)
            {
                return;
            }

            var locator = AvaloniaLocator.CurrentMutable;

            if (locator.GetService<IFontManagerImpl>() is not VelloFontManagerImpl)
            {
                locator.Bind<IFontManagerImpl>().ToConstant(new VelloFontManagerImpl());
            }

            if (locator.GetService<ITextShaperImpl>() is not VelloTextShaper)
            {
                locator.Bind<ITextShaperImpl>().ToConstant(new VelloTextShaper());
            }

            s_initialized = true;
        }
    }
}
