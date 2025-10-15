using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using Avalonia;
using Avalonia.Logging;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using VelloSharp;
using VelloSharp.Avalonia.Vello;
using VelloSharp.Avalonia.Vello.Rendering;

namespace VelloSharp.Avalonia.Browser;

[SupportedOSPlatform("browser")]
internal static class VelloBrowserPlatform
{
    private static readonly object s_initLock = new();
    private static bool s_initialized;
    private static VelloPlatformOptions s_options = new();
    private static VelloBrowserRenderInterface? s_renderInterface;
    private static bool s_loggingAttached;
    private static VelloBrowserRenderTimer? s_renderTimer;
    private static bool s_renderLoopRegistered;
    private const string WebGpuLogArea = "Vello.WebGPU.Browser";
    private const string WebGpuCapabilitiesResourceKey = "Vello.WebGpu.Capabilities";
    private static WebGpuRuntime.WebGpuCapabilities? s_webGpuCapabilities;

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
            s_renderInterface = new VelloBrowserRenderInterface(s_options);

            var locator = AvaloniaLocator.CurrentMutable;

            if (AvaloniaLocator.Current.GetService<IRenderTimer>() is null)
            {
                s_renderTimer ??= new VelloBrowserRenderTimer(runsInBackground: false);
                locator.Bind<IRenderTimer>().ToConstant(s_renderTimer);
            }

            var renderTimer = s_renderTimer ?? AvaloniaLocator.Current.GetService<IRenderTimer>();
            EnsureRenderLoopRegistered(locator, renderTimer);

            locator.Bind<IPlatformRenderInterface>().ToConstant(s_renderInterface);

            if (locator.GetService<IFontManagerImpl>() is null)
            {
                locator.Bind<IFontManagerImpl>().ToConstant(new VelloFontManagerImpl());
            }

            if (locator.GetService<ITextShaperImpl>() is null)
            {
                locator.Bind<ITextShaperImpl>().ToConstant(new VelloTextShaper());
            }

            VelloBrowserDispatcherLifecycle.EnsureInitialized();

            if (locator.GetService<Compositor>() is null)
            {
                locator.Bind<Compositor>().ToFunc(() => new Compositor(null));
            }

            AttachWebGpuLogging();
            NativeLibraryLoader.RegisterProbingPath(Path.Combine(AppContext.BaseDirectory, "native"));

            s_initialized = true;
        }
    }

    private static void EnsureRenderLoopRegistered(AvaloniaLocator locator, IRenderTimer? timer)
    {
        if (s_renderLoopRegistered || timer is null)
        {
            return;
        }

        VelloBrowserRenderLoopManager.EnsureRegistered(locator, timer, ref s_renderLoopRegistered);
    }

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

            VelloBrowserDiagnostics.ReportAvailability(false, ex.Message);
            return;
        }

        WebGpuRuntime.LogMessage += OnWebGpuLogMessage;
        WebGpuRuntime.DeviceCapabilitiesChanged += OnWebGpuCapabilitiesChanged;
        if (WebGpuRuntime.TryGetLatestCapabilities(out var capabilities) && capabilities is not null)
        {
            Volatile.Write(ref s_webGpuCapabilities, capabilities);
            PublishCapabilitiesToDiagnostics(capabilities);
            VelloBrowserDiagnostics.ReportAvailability(true, null);
        }
        else
        {
            VelloBrowserDiagnostics.ReportAvailability(false, "Awaiting WebGPU adapter handshake.");
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

    private static void OnWebGpuCapabilitiesChanged(object? sender, WebGpuRuntime.WebGpuCapabilitiesChangedEventArgs e)
    {
        var previous = Volatile.Read(ref s_webGpuCapabilities);
        var current = e.Capabilities;
        Volatile.Write(ref s_webGpuCapabilities, current);

        if (previous is null || !previous.Equals(current))
        {
            if (Logger.TryGet(LogEventLevel.Information, WebGpuLogArea, out var log))
            {
                var limits = current.DeviceLimits;
                log.Log(
                    null,
                    "WebGPU capabilities resolved: device=0x{Device:X}, surface=0x{Surface:X}, format={Format}, maxTexture2D={MaxTexture2D}, maxColorAttachments={MaxColorAttachments}",
                    current.DeviceHandle,
                    current.SurfaceHandle,
                    current.SurfaceTextureFormat,
                    limits.MaxTextureDimension2D,
                    limits.MaxColorAttachments);
            }
        }

        PublishCapabilitiesToDiagnostics(current);
        VelloBrowserDiagnostics.ReportAvailability(true, null);
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

public static class VelloBrowserDiagnostics
{
    private static readonly object s_stateLock = new();
    private static bool s_isAvailable;
    private static string? s_lastExplanation;

    public static event EventHandler<WebGpuAvailabilityChangedEventArgs>? WebGpuAvailabilityChanged;

    public static bool IsWebGpuAvailable
    {
        get
        {
            lock (s_stateLock)
            {
                return s_isAvailable;
            }
        }
    }

    public static string? LastFailureExplanation
    {
        get
        {
            lock (s_stateLock)
            {
                return s_lastExplanation;
            }
        }
    }

    internal static void ReportAvailability(bool isAvailable, string? explanation)
    {
        EventHandler<WebGpuAvailabilityChangedEventArgs>? handlers;
        bool shouldRaise;

        lock (s_stateLock)
        {
            if (s_isAvailable == isAvailable && string.Equals(s_lastExplanation, explanation, StringComparison.Ordinal))
            {
                return;
            }

            s_isAvailable = isAvailable;
            s_lastExplanation = explanation;
            handlers = WebGpuAvailabilityChanged;
            shouldRaise = handlers is not null;
        }

        if (shouldRaise)
        {
            handlers?.Invoke(null, new WebGpuAvailabilityChangedEventArgs(isAvailable, explanation));
        }
    }
}

public sealed class WebGpuAvailabilityChangedEventArgs : EventArgs
{
    public WebGpuAvailabilityChangedEventArgs(bool isAvailable, string? explanation)
    {
        IsAvailable = isAvailable;
        Explanation = explanation;
    }

    public bool IsAvailable { get; }
    public string? Explanation { get; }
}
