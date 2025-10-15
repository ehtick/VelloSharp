using System;
using System.Threading;
using System.Threading;
using Avalonia;
using Avalonia.Logging;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using VelloSharp.Avalonia.Vello.Rendering;
using VelloSharp;

namespace VelloSharp.Avalonia.Vello;

internal static class VelloPlatform
{
    private static readonly object s_initLock = new();
    private static bool s_initialized;
    private static VelloPlatformOptions s_options = new();
    private static VelloPlatformRenderInterface? s_renderInterface;
    private static VelloGraphicsDevice? s_device;
    private static bool s_loggingAttached;
    private static WebGpuRuntime.WebGpuCapabilities? s_webGpuCapabilities;
    private const string WebGpuLogArea = "Vello.WebGPU";
    private const string WebGpuCapabilitiesResourceKey = "Vello.WebGpu.Capabilities";

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

            var compositionOptions = locator.GetService<CompositionOptions>();
            if (compositionOptions is null)
            {
                compositionOptions = new CompositionOptions();
                locator.Bind<CompositionOptions>().ToConstant(compositionOptions);
            }

            compositionOptions.UseRegionDirtyRectClipping = true;
            compositionOptions.UseSaveLayerRootClip ??= false;

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

            AttachWebGpuLogging();

            s_initialized = true;
        }
    }

    public static VelloGraphicsDevice GraphicsDevice =>
        s_device ?? throw new InvalidOperationException("Vello platform has not been initialized.");

    public static WebGpuRuntime.WebGpuCapabilities? LatestWebGpuCapabilities =>
        Volatile.Read(ref s_webGpuCapabilities);

    private static void AttachWebGpuLogging()
    {
        if (s_loggingAttached)
        {
            return;
        }

        try
        {
            WebGpuRuntime.EnsureInitialized();
        }
        catch (Exception ex) when (ex is NotSupportedException or WebGpuInteropException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            if (Logger.TryGet(LogEventLevel.Warning, WebGpuLogArea, out var log))
            {
                log.Log(null, "Unable to initialize WebGPU logging: {0}", ex.Message);
            }

            return;
        }

        WebGpuRuntime.LogMessage += OnWebGpuLogMessage;
        WebGpuRuntime.DeviceCapabilitiesChanged += OnWebGpuCapabilitiesChanged;
        if (WebGpuRuntime.TryGetLatestCapabilities(out var capabilities) && capabilities is not null)
        {
            Volatile.Write(ref s_webGpuCapabilities, capabilities);
            PublishCapabilitiesToDiagnostics(capabilities);
        }

        s_loggingAttached = true;
    }

    private static void OnWebGpuLogMessage(WebGpuRuntime.WebGpuLogLevel level, string message)
    {
        var avaloniaLevel = ConvertLogLevel(level);
        if (!Logger.TryGet(avaloniaLevel, WebGpuLogArea, out var log))
        {
            return;
        }

        log.Log(null, "{Message}", message);
    }

    private static void OnWebGpuCapabilitiesChanged(object? sender, WebGpuRuntime.WebGpuCapabilitiesChangedEventArgs e)
    {
        var previous = Volatile.Read(ref s_webGpuCapabilities);
        var current = e.Capabilities;
        Volatile.Write(ref s_webGpuCapabilities, current);

        if (previous is not null && previous.Equals(current))
        {
            return;
        }

        if (!Logger.TryGet(LogEventLevel.Information, WebGpuLogArea, out var log))
        {
            return;
        }

        var limits = current.DeviceLimits;
        log.Log(
            null,
            "WebGPU capabilities resolved: device=0x{Device:X}, surface=0x{Surface:X}, format={Format}, maxTexture2D={MaxTexture2D}, maxColorAttachments={MaxColorAttachments}",
            current.DeviceHandle,
            current.SurfaceHandle,
            current.SurfaceTextureFormat,
            limits.MaxTextureDimension2D,
            limits.MaxColorAttachments);

        PublishCapabilitiesToDiagnostics(current);
    }

    private static LogEventLevel ConvertLogLevel(WebGpuRuntime.WebGpuLogLevel level)
    {
        return level switch
        {
            WebGpuRuntime.WebGpuLogLevel.Trace => LogEventLevel.Verbose,
            WebGpuRuntime.WebGpuLogLevel.Debug => LogEventLevel.Debug,
            WebGpuRuntime.WebGpuLogLevel.Info => LogEventLevel.Information,
            WebGpuRuntime.WebGpuLogLevel.Warn => LogEventLevel.Warning,
            WebGpuRuntime.WebGpuLogLevel.Error => LogEventLevel.Error,
            _ => LogEventLevel.Debug,
        };
    }

    private static void PublishCapabilitiesToDiagnostics(WebGpuRuntime.WebGpuCapabilities? capabilities)
    {
        if (Application.Current is null)
        {
            return;
        }

        void UpdateResources()
        {
            var resources = Application.Current!.Resources;
            if (capabilities is null)
            {
                resources.Remove(WebGpuCapabilitiesResourceKey);
            }
            else
            {
                resources[WebGpuCapabilitiesResourceKey] = WebGpuCapabilityHelpers.BuildSummary(capabilities);
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            UpdateResources();
        }
        else
        {
            Dispatcher.UIThread.Post(UpdateResources, DispatcherPriority.Background);
        }
    }
}
