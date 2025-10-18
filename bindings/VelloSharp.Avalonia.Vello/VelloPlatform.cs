using System;
using System.Threading;
using Avalonia;
using Avalonia.Logging;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using VelloSharp.Avalonia.Vello.Rendering;
using VelloSharp;
using System.Runtime.Versioning;
using VelloSharp.Avalonia.Core.Device;
using VelloSharp.Avalonia.Core.Options;

namespace VelloSharp.Avalonia.Vello;

internal static class VelloPlatform
{
    private static readonly object s_initLock = new();
    private static bool s_initialized;
    private static VelloPlatformOptions s_options = new();
    private static VelloPlatformRenderInterface? s_renderInterface;
    private static WgpuGraphicsDeviceProvider? s_deviceProvider;
    private static GraphicsBackendOptions? s_backendOptions;
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
            s_deviceProvider = new WgpuGraphicsDeviceProvider(ResolveRendererOptions);
            s_backendOptions = CreateBackendOptions(s_options);
            s_renderInterface = new VelloPlatformRenderInterface(s_deviceProvider, s_options);

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

            if (OperatingSystem.IsBrowser())
            {
                AttachWebGpuLogging();
            }
            else
            {
                s_loggingAttached = true;
            }

            s_initialized = true;
        }
    }

    public static WgpuGraphicsDeviceProvider GraphicsDeviceProvider =>
        s_deviceProvider ?? throw new InvalidOperationException("Vello platform has not been initialized.");

    public static GraphicsBackendOptions BackendOptions =>
        s_backendOptions ?? throw new InvalidOperationException("Vello platform has not been initialized.");

    [SupportedOSPlatform("browser")]
    public static WebGpuRuntime.WebGpuCapabilities? LatestWebGpuCapabilities =>
        Volatile.Read(ref s_webGpuCapabilities);

    private static RendererOptions ResolveRendererOptions(GraphicsDeviceOptions deviceOptions)
    {
        var baseOptions = s_options.RendererOptions;
        var features = deviceOptions.Features;

        return new RendererOptions(
            useCpu: baseOptions.UseCpu || features.EnableCpuFallback,
            supportArea: baseOptions.SupportArea && features.EnableAreaAa,
            supportMsaa8: baseOptions.SupportMsaa8 && features.EnableMsaa8,
            supportMsaa16: baseOptions.SupportMsaa16 && features.EnableMsaa16,
            initThreads: baseOptions.InitThreads,
            pipelineCache: baseOptions.PipelineCache);
    }

    private static GraphicsBackendOptions CreateBackendOptions(VelloPlatformOptions options)
    {
        var rendererOptions = options.RendererOptions;
        var features = new GraphicsFeatureSet(
            EnableCpuFallback: rendererOptions.UseCpu,
            EnableMsaa8: rendererOptions.SupportMsaa8,
            EnableMsaa16: rendererOptions.SupportMsaa16,
            EnableAreaAa: rendererOptions.SupportArea,
            EnableOpacityLayers: true,
            MaxGpuResourceBytes: null,
            EnableValidationLayers: false);

        var presentation = new GraphicsPresentationOptions(
            options.PresentMode,
            options.ClearColor,
            options.FramesPerSecond);

        return new GraphicsBackendOptions(
            new[] { GraphicsBackendKind.VelloWgpu },
            features,
            presentation);
    }

    [SupportedOSPlatform("browser")]
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

    [SupportedOSPlatform("browser")]
    private static void OnWebGpuLogMessage(WebGpuRuntime.WebGpuLogLevel level, string message)
    {
        var avaloniaLevel = ConvertLogLevel(level);
        if (!Logger.TryGet(avaloniaLevel, WebGpuLogArea, out var log))
        {
            return;
        }

        log.Log(null, "{Message}", message);
    }

    [SupportedOSPlatform("browser")]
    private static void OnWebGpuCapabilitiesChanged(object? sender, WebGpuRuntime.WebGpuCapabilitiesChangedEventArgs e)
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

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

    [SupportedOSPlatform("browser")]
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

    [SupportedOSPlatform("browser")]
    private static void PublishCapabilitiesToDiagnostics(WebGpuRuntime.WebGpuCapabilities? capabilities)
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

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
