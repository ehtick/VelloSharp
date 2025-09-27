using System;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using VelloSharp.Avalonia.Vello.Rendering;

namespace VelloSharp.Avalonia.Vello;

internal static class VelloPlatform
{
    private static readonly object s_initLock = new();
    private static bool s_initialized;
    private static VelloPlatformOptions s_options = new();
    private static VelloPlatformRenderInterface? s_renderInterface;
    private static VelloGraphicsDevice? s_device;

    public static void Initialize(VelloPlatformOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        lock (s_initLock)
        {
            if (s_initialized)
            {
                return;
            }

            s_options = options;
            s_device = new VelloGraphicsDevice();
            s_renderInterface = new VelloPlatformRenderInterface(s_device, s_options);

            var locator = AvaloniaLocator.CurrentMutable;

            locator.Bind<IPlatformRenderInterface>().ToConstant(s_renderInterface);

            if (locator.GetService<IFontManagerImpl>() is null)
            {
                locator.Bind<IFontManagerImpl>().ToConstant(new VelloFontManagerImpl());
            }

            if (locator.GetService<ITextShaperImpl>() is null)
            {
                locator.Bind<ITextShaperImpl>().ToConstant(new VelloTextShaper());
            }

            if (locator.GetService<Compositor>() is null)
            {
                locator.Bind<Compositor>().ToFunc(() => new Compositor(null));
            }

            s_initialized = true;
        }
    }

    public static VelloGraphicsDevice GraphicsDevice =>
        s_device ?? throw new InvalidOperationException("Vello platform has not been initialized.");
}
