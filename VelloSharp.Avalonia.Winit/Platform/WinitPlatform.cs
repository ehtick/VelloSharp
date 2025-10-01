using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Metadata;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;

namespace Avalonia.Winit;

/// <summary>
/// Options controlling the Winit-backed windowing platform.
/// </summary>
public sealed class WinitPlatformOptions
{
    /// <summary>
    /// Gets or sets the desired frames-per-second for the render timer.
    /// </summary>
    public int FramesPerSecond { get; set; } = 60;

    /// <summary>
    /// Gets or sets the default Winit window configuration.
    /// </summary>
    public VelloSharp.WinitWindowOptions Window { get; set; } = new VelloSharp.WinitWindowOptions
    {
        Title = "Avalonia Winit"
    };
}

/// <summary>
/// Provides the AppBuilder extension entry point.
/// </summary>
public static class WinitApplicationExtensions
{
    /// <summary>
    /// Configures Avalonia to use the Winit windowing subsystem backed by VelloSharp bindings.
    /// </summary>
    /// <param name="builder">The <see cref="AppBuilder"/> instance.</param>
    /// <param name="options">Optional configuration values for the subsystem.</param>
    /// <returns>The builder instance for fluent configuration.</returns>
    public static AppBuilder UseWinit(this AppBuilder builder, WinitPlatformOptions? options = null)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        return builder
            .UseStandardRuntimePlatformSubsystem()
            .UseWindowingSubsystem(() => WinitPlatform.Initialize(options ?? new WinitPlatformOptions()), "Winit");
    }
}

internal static class WinitPlatform
{
    private static readonly object s_initLock = new();
    private static bool s_initialized;
    private static WinitPlatformOptions s_options = new();
    private static WinitDispatcher? s_dispatcher;
    private static WinitWindowingPlatform? s_windowingPlatform;
    private static Compositor? s_compositor;
    private static WinitScreenManager? s_screenManager;

    public static void Initialize(WinitPlatformOptions options)
    {
        lock (s_initLock)
        {
            if (s_initialized)
            {
                return;
            }

            s_options = options ?? throw new ArgumentNullException(nameof(options));

            s_dispatcher = new WinitDispatcher();
            s_screenManager = new WinitScreenManager();
            s_windowingPlatform = new WinitWindowingPlatform(s_dispatcher, s_screenManager, s_options.Window);

            var clipboard = new WinitClipboard();
            var cursorFactory = new WinitCursorFactory();
            var keyboardDevice = new KeyboardDevice();
            var mouseDevice = new MouseDevice();

            var commandModifiers = OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst()
                ? KeyModifiers.Meta
                : KeyModifiers.Control;
            var wholeWordModifiers = OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst()
                ? KeyModifiers.Alt
                : KeyModifiers.Control;
            var hotkeys = new PlatformHotkeyConfiguration(commandModifiers, KeyModifiers.Shift, wholeWordModifiers);

            var locator = AvaloniaLocator.CurrentMutable;

            locator
                .Bind<IDispatcherImpl>().ToConstant(s_dispatcher)
                .Bind<IClipboard>().ToConstant(clipboard)
                .Bind<ICursorFactory>().ToConstant(cursorFactory)
                .Bind<IScreenImpl>().ToConstant(s_screenManager)
                .Bind<IWindowingPlatform>().ToConstant(s_windowingPlatform)
                .Bind<IKeyboardDevice>().ToConstant(keyboardDevice)
                .Bind<IMouseDevice>().ToConstant(mouseDevice)
                .Bind<PlatformHotkeyConfiguration>().ToConstant(hotkeys)
                .Bind<IPlatformSettings>().ToSingleton<DefaultPlatformSettings>()
                .Bind<IPlatformIconLoader>().ToSingleton<WinitIconLoader>()
                .Bind<IPlatformLifetimeEventsImpl>().ToSingleton<WinitLifetimeEvents>();

            var renderTimer = new DefaultRenderTimer(Math.Max(1, s_options.FramesPerSecond));
            locator.Bind<IRenderTimer>().ToConstant(renderTimer);

            if (AvaloniaLocator.Current.GetService<IRenderTimer>() is null)
            {
                throw new InvalidOperationException("Failed to register IRenderTimer.");
            }

            s_compositor = new Compositor(null);
            locator.Bind<Compositor>().ToConstant(s_compositor);

            s_initialized = true;
        }
    }

    public static WinitWindowingPlatform WindowingPlatform => s_windowingPlatform ?? throw new InvalidOperationException("Winit platform not initialized.");

    public static WinitDispatcher Dispatcher => s_dispatcher ?? throw new InvalidOperationException("Winit platform dispatcher not available.");

    public static Compositor Compositor => s_compositor ?? throw new InvalidOperationException("Winit platform compositor not available.");

    internal static WinitScreenManager Screens => s_screenManager ?? throw new InvalidOperationException("Winit platform screens not available.");
}

internal sealed class WinitIconLoader : IPlatformIconLoader
{
    public IWindowIconImpl LoadIcon(string fileName)
    {
        if (fileName is null)
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        using var stream = File.OpenRead(fileName);
        return LoadIcon(stream);
    }

    public IWindowIconImpl LoadIcon(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return new WinitIconImpl(memory.ToArray());
    }

    public IWindowIconImpl LoadIcon(IBitmapImpl bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        using var memory = new MemoryStream();
        bitmap.Save(memory);
        return new WinitIconImpl(memory.ToArray());
    }
}

internal sealed class WinitIconImpl : IWindowIconImpl
{
    private readonly byte[] _data;

    public WinitIconImpl(byte[] data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public ReadOnlySpan<byte> Data => _data;

    public byte[] GetBytes() => (byte[])_data.Clone();

    public void Save(Stream outputStream)
    {
        ArgumentNullException.ThrowIfNull(outputStream);
        outputStream.Write(_data, 0, _data.Length);
    }
}

[PrivateApi]
internal sealed class WinitLifetimeEvents : IPlatformLifetimeEventsImpl
{
    public event EventHandler<ShutdownRequestedEventArgs>? ShutdownRequested;

    public void RequestShutdown(ShutdownRequestedEventArgs args)
    {
        ShutdownRequested?.Invoke(this, args);
    }
}
