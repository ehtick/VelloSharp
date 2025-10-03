using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using VelloSharp;
using VelloSharp.Scenes;

namespace VelloSharp.WithWinit;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var options = SampleOptions.Parse(args);
            using var app = new WinitSampleApp(options);
            return app.Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

internal sealed class SampleOptions
{
    private SampleOptions(
        bool useTestScenes,
        int? initialScene,
        int complexity,
        string? assetRoot,
        bool vsync,
        AntialiasingMode antialiasing)
    {
        UseTestScenes = useTestScenes;
        InitialScene = initialScene;
        Complexity = complexity;
        AssetRoot = assetRoot;
        Vsync = vsync;
        Antialiasing = antialiasing;
    }

    public bool UseTestScenes { get; }
    public int? InitialScene { get; }
    public int Complexity { get; }
    public string? AssetRoot { get; }
    public bool Vsync { get; }
    public AntialiasingMode Antialiasing { get; }

    public static SampleOptions Parse(ReadOnlySpan<string> args)
    {
        bool useTestScenes = false;
        int? initialScene = null;
        int complexity = 1;
        string? assetRoot = null;
        bool vsync = true;
        AntialiasingMode aa = AntialiasingMode.Area;

        var index = 0;
        while (index < args.Length)
        {
            var arg = args[index];
            switch (arg)
            {
                case "--help":
                case "-h":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
                case "--test-scenes":
                    useTestScenes = true;
                    index++;
                    break;
                case "--scene":
                    index++;
                    RequireArgument(args, index, "--scene");
                    if (!int.TryParse(args[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sceneIndex))
                    {
                        throw new ArgumentException("--scene expects an integer value.");
                    }
                    initialScene = sceneIndex;
                    index++;
                    break;
                case "--complexity":
                    index++;
                    RequireArgument(args, index, "--complexity");
                    if (!int.TryParse(args[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var complexityValue) || complexityValue < 1)
                    {
                        throw new ArgumentException("--complexity expects an integer >= 1.");
                    }
                    complexity = complexityValue;
                    index++;
                    break;
                case "--asset-root":
                    index++;
                    RequireArgument(args, index, "--asset-root");
                    assetRoot = args[index];
                    index++;
                    break;
                case "--vsync-off":
                    vsync = false;
                    index++;
                    break;
                case "--aa":
                    index++;
                    RequireArgument(args, index, "--aa");
                    aa = args[index].ToLowerInvariant() switch
                    {
                        "area" => AntialiasingMode.Area,
                        "msaa8" => AntialiasingMode.Msaa8,
                        "msaa16" => AntialiasingMode.Msaa16,
                        _ => throw new ArgumentException("--aa expects one of: area, msaa8, msaa16"),
                    };
                    index++;
                    break;
                default:
                    throw new ArgumentException($"Unrecognized argument '{arg}'. Use --help for usage information.");
            }
        }

        return new SampleOptions(useTestScenes, initialScene, complexity, assetRoot, vsync, aa);
    }

    private static void RequireArgument(ReadOnlySpan<string> args, int index, string option)
    {
        if (index >= args.Length)
        {
            throw new ArgumentException($"{option} expects a value.");
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("VelloSharp.WithWinit");
        Console.WriteLine("Options:");
        Console.WriteLine("  --test-scenes           Load the standard test scene collection (default).");
        Console.WriteLine("  --scene <index>         Select the initial scene by zero-based index.");
        Console.WriteLine("  --complexity <value>    Set the initial complexity multiplier (default: 1).");
        Console.WriteLine("  --asset-root <path>     Override the root directory for assets (fonts, images).");
        Console.WriteLine("  --vsync-off             Start with vsync disabled (default: on).");
        Console.WriteLine("  --aa <mode>             Choose antialiasing: area, msaa8, msaa16 (default: area).");
        Console.WriteLine();
        Console.WriteLine("Keyboard shortcuts:");
        Console.WriteLine("  Arrow Left/Right   Cycle scenes");
        Console.WriteLine("  Arrow Up/Down      Adjust complexity");
        Console.WriteLine("  Space              Reset view transform");
        Console.WriteLine("  Q / E              Rotate around cursor");
        Console.WriteLine("  S                  Toggle overlay stats");
        Console.WriteLine("  C                  Reset stats extrema");
        Console.WriteLine("  M                  Cycle antialiasing (Shift+M for reverse)");
        Console.WriteLine("  V                  Toggle vsync");
    }
}

internal sealed class WinitSampleApp : IWinitEventHandler, IDisposable
{
    private const int DefaultWidth = 1280;
    private const int DefaultHeight = 720;
    private const int MaxComplexity = 64;

    private readonly SampleOptions _options;
    private readonly WinitEventLoop _eventLoop = new();
    private readonly Scene _scene = new();
    private readonly FrameStats _frameStats = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Stopwatch _frameTimer = Stopwatch.StartNew();
    private readonly PathBuilder _overlayPath = new();
    private readonly TouchTracker _touchState = new();
    private readonly HashSet<ulong> _navigationFingers = new();

    private ManagedSceneHost? _host;
    private IReadOnlyList<ExampleScene> _scenes = Array.Empty<ExampleScene>();

    private readonly AntialiasingMode[] _supportedAaModes;

    private WgpuInstance? _wgpuInstance;
    private WgpuAdapter? _wgpuAdapter;
    private WgpuDevice? _wgpuDevice;
    private WgpuQueue? _wgpuQueue;
    private WgpuSurface? _wgpuSurface;
    private WgpuRenderer? _wgpuRenderer;
    private WgpuPipelineCache? _pipelineCache;
    private string? _pipelineCachePath;
    private WgpuSurfaceConfiguration _surfaceConfig;
    private WgpuTextureFormat _surfaceFormat = WgpuTextureFormat.Bgra8Unorm;
    private bool _requiresSurfaceBlit;
    private bool _surfaceValid;
    private WgpuFeature _deviceFeatures;
    private WgpuAdapterInfo? _adapterInfo;

    private WinitWindow? _window;

    private uint _width = DefaultWidth;
    private uint _height = DefaultHeight;
    private double _scaleFactor = 1d;
    private bool _vsyncOn;
    private AntialiasingMode _aaMode;

    private int _sceneIndex;
    private int _complexity;
    private bool _statsVisible = true;
    private bool _needsRedraw = true;

    private bool _pointerDown;
    private Vector2? _pointerPosition;
    private Matrix3x2 _viewTransform = Matrix3x2.Identity;
    private WinitModifiers _modifiers;

    private Vector2? _sceneResolution;
    private RgbaColor _baseColor = RgbaColor.FromBytes(12, 16, 20);
    private bool _gpuProfilingEnabled;
    private bool _gpuProfilerSupported = true;
    private GpuProfilerFrame? _gpuProfilerFrame;

    public WinitSampleApp(SampleOptions options)
    {
        _options = options;
        _supportedAaModes = BuildSupportedAaModes();
        _complexity = Math.Clamp(options.Complexity, 1, MaxComplexity);
        _aaMode = ClampAaMode(options.Antialiasing);
        _vsyncOn = options.Vsync;
        InitializeScenes(options);
    }

    public int Run()
    {
        var configuration = new WinitRunConfiguration
        {
            CreateWindow = true,
            Window = new WinitWindowOptions
            {
                Width = DefaultWidth,
                Height = DefaultHeight,
                Title = BuildWindowTitle(),
                Visible = true,
                Decorations = true,
                Resizable = true,
            },
        };

        var status = _eventLoop.Run(configuration, this);
        EnsureSuccess(status, "Event loop execution failed");
        return 0;
    }

    public void HandleEvent(WinitEventLoopContext context, in WinitEventArgs args)
    {
        switch (args.Kind)
        {
            case WinitEventKind.WindowCreated:
                HandleWindowCreated(context, args);
                break;
            case WinitEventKind.WindowResized:
                HandleWindowResized(args.Width, args.Height);
                break;
            case WinitEventKind.WindowScaleFactorChanged:
                if (args.ScaleFactor > 0)
                {
                    _scaleFactor = args.ScaleFactor;
                    UpdateWindowTitle();
                }
                break;
            case WinitEventKind.WindowRedrawRequested:
                RenderFrame();
                break;
            case WinitEventKind.WindowCloseRequested:
                context.Exit();
                break;
            case WinitEventKind.WindowDestroyed:
                DestroySurface();
                _window = null;
                break;
            case WinitEventKind.AboutToWait:
                ProcessTouchGestures();
                if (_needsRedraw && _surfaceValid && _window is not null)
                {
                    _window.RequestRedraw();
                    _needsRedraw = false;
                }
                break;
            case WinitEventKind.MouseInput:
                if (args.MouseButton == WinitMouseButton.Left)
                {
                    _pointerDown = args.ElementState == WinitElementState.Pressed;
                }
                break;
            case WinitEventKind.MouseWheel:
                HandleMouseWheel(args);
                break;
            case WinitEventKind.CursorMoved:
                HandleCursorMoved(args);
                break;
            case WinitEventKind.CursorLeft:
                _pointerPosition = null;
                break;
            case WinitEventKind.KeyboardInput:
                HandleKeyboard(args);
                break;
            case WinitEventKind.ModifiersChanged:
                _modifiers = args.Modifiers;
                break;
            case WinitEventKind.MemoryWarning:
                _frameStats.Clear();
                break;
            case WinitEventKind.Touch:
                HandleTouch(args);
                break;
        }
    }

    private void HandleWindowCreated(WinitEventLoopContext context, in WinitEventArgs args)
    {
        _window = context.GetWindow() ?? throw new InvalidOperationException("Window handle unavailable.");
        context.SetControlFlow(WinitControlFlow.Poll);

        _width = Math.Max(1u, args.Width);
        _height = Math.Max(1u, args.Height);
        _scaleFactor = args.ScaleFactor > 0d ? args.ScaleFactor : _scaleFactor;

        var descriptor = CreateSurfaceDescriptor(_window, _width, _height);
        EnsureGpuContext(descriptor);
        ConfigureSurface(_width, _height);
        UpdateWindowTitle();
        RequestRedraw();
    }

    private void HandleWindowResized(uint width, uint height)
    {
        if (_wgpuSurface is null)
        {
            return;
        }

        if (width == 0 || height == 0)
        {
            _surfaceValid = false;
            return;
        }

        _width = width;
        _height = height;
        ConfigureSurface(width, height);
        UpdateWindowTitle();
        RequestRedraw();
    }

    private void HandleCursorMoved(in WinitEventArgs args)
    {
        var position = new Vector2((float)args.MouseX, (float)args.MouseY);
        if (_pointerDown && _pointerPosition is Vector2 previous)
        {
            var delta = position - previous;
            if (delta.LengthSquared() > 0f)
            {
                ApplyTranslation(delta);
            }
        }

        _pointerPosition = position;
    }

    private void HandleMouseWheel(in WinitEventArgs args)
    {
        if (_pointerPosition is not Vector2 position)
        {
            return;
        }

        const double baseFactor = 1.05;
        const double pixelsPerLine = 20.0;
        double exponent = args.ScrollDeltaKind switch
        {
            WinitMouseScrollDeltaKind.LineDelta => args.DeltaY,
            WinitMouseScrollDeltaKind.PixelDelta => args.DeltaY / pixelsPerLine,
            _ => 0.0,
        };

        var scale = (float)Math.Pow(baseFactor, exponent);
        if (float.IsFinite(scale) && scale > 0f)
        {
            ApplyScale(scale, position);
        }
    }

    private void HandleKeyboard(in WinitEventArgs args)
    {
        if (args.ElementState != WinitElementState.Pressed)
        {
            return;
        }

        var code = (uint)args.KeyCode;
        switch (code)
        {
            case HidCodes.ArrowLeft:
                ChangeScene(-1);
                break;
            case HidCodes.ArrowRight:
                ChangeScene(1);
                break;
            case HidCodes.ArrowUp:
                ChangeComplexity(1);
                break;
            case HidCodes.ArrowDown:
                ChangeComplexity(-1);
                break;
            case HidCodes.Space:
                ResetView();
                break;
            case HidCodes.KeyQ:
            case HidCodes.KeyE:
                if (_pointerPosition is Vector2 pivot)
                {
                    var angle = code == HidCodes.KeyE ? -0.05f : 0.05f;
                    ApplyRotation(angle, pivot);
                }
                break;
            case HidCodes.KeyS:
                _statsVisible = !_statsVisible;
                RequestRedraw();
                break;
            case HidCodes.KeyC:
                _frameStats.ClearExtrema();
                RequestRedraw();
                break;
            case HidCodes.KeyG:
                ToggleGpuProfiler();
                break;
            case HidCodes.KeyM:
                CycleAaMode((_modifiers & WinitModifiers.Shift) != 0);
                break;
            case HidCodes.KeyV:
                ToggleVsync();
                break;
        }
    }

    private void HandleTouch(in WinitEventArgs args)
    {
        switch (args.TouchPhase)
        {
            case WinitTouchPhaseKind.Started:
                if (_surfaceValid && _surfaceConfig.Height > 0 && _surfaceConfig.Width > 0)
                {
                    var touchY = args.MouseY;
                    var height = _surfaceConfig.Height;
                    if (touchY > height * 2d / 3d)
                    {
                        _navigationFingers.Add(args.TouchId);
                        var touchX = args.MouseX;
                        var width = _surfaceConfig.Width;
                        if (touchX < width / 3d)
                        {
                            ChangeSceneSaturating(-1);
                        }
                        else if (touchX > 2d * width / 3d)
                        {
                            ChangeSceneSaturating(1);
                        }
                    }
                }
                break;
            case WinitTouchPhaseKind.Ended:
            case WinitTouchPhaseKind.Cancelled:
                _navigationFingers.Remove(args.TouchId);
                break;
        }

        if (!_navigationFingers.Contains(args.TouchId))
        {
            _touchState.AddEvent(args);
        }
    }

    private void ProcessTouchGestures()
    {
        var gesture = _touchState.EndFrame();
        if (!gesture.HasValue)
        {
            return;
        }

        ApplyTouchGesture(gesture.Value);
    }

    private void ApplyTouchGesture(in MultiTouchInfo gesture)
    {
        var translation = gesture.TranslationDelta;
        var centre = gesture.ZoomCenter;

        if (!float.IsFinite(translation.X) || !float.IsFinite(translation.Y))
        {
            translation = Vector2.Zero;
        }

        if (!float.IsFinite(centre.X) || !float.IsFinite(centre.Y))
        {
            centre = Vector2.Zero;
        }

        var scale = (float)gesture.ZoomDelta;
        if (!float.IsFinite(scale) || scale <= 0f)
        {
            scale = 1f;
        }

        var rotation = (float)gesture.RotationDelta;
        if (!float.IsFinite(rotation))
        {
            rotation = 0f;
        }

        _viewTransform =
            Matrix3x2.CreateTranslation(translation) *
            Matrix3x2.CreateTranslation(centre) *
            Matrix3x2.CreateScale(scale) *
            Matrix3x2.CreateRotation(rotation) *
            Matrix3x2.CreateTranslation(-centre) *
            _viewTransform;

        RequestRedraw();
    }

    private void EnsureGpuContext(SurfaceDescriptor descriptor)
    {
        if (_wgpuInstance is null)
        {
            _wgpuInstance = new WgpuInstance();
        }

        _wgpuSurface?.Dispose();
        _wgpuSurface = WgpuSurface.Create(_wgpuInstance, descriptor);

        if (_wgpuAdapter is null)
        {
            var adapterOptions = new WgpuRequestAdapterOptions
            {
                PowerPreference = WgpuPowerPreference.HighPerformance,
                CompatibleSurface = _wgpuSurface,
            };
            _wgpuAdapter = _wgpuInstance.RequestAdapter(adapterOptions);
            _adapterInfo = TryGetAdapterInfo(_wgpuAdapter);

            var adapterFeatures = TryGetAdapterFeatures(_wgpuAdapter);
            var desiredFeatures = WgpuFeature.None;
            if (HasFeature(adapterFeatures, WgpuFeature.ClearTexture))
            {
                desiredFeatures |= WgpuFeature.ClearTexture;
            }
            if (HasFeature(adapterFeatures, WgpuFeature.PipelineCache))
            {
                desiredFeatures |= WgpuFeature.PipelineCache;
            }
            if (HasFeature(adapterFeatures, WgpuFeature.TimestampQuery))
            {
                desiredFeatures |= WgpuFeature.TimestampQuery;
            }
            if (HasFeature(adapterFeatures, WgpuFeature.PipelineStatisticsQuery))
            {
                desiredFeatures |= WgpuFeature.PipelineStatisticsQuery;
            }

            var deviceDescriptor = new WgpuDeviceDescriptor
            {
                Label = "with_winit.device",
                RequiredFeatures = desiredFeatures,
                Limits = WgpuLimitsPreset.Default,
            };

            try
            {
                _wgpuDevice = _wgpuAdapter.RequestDevice(deviceDescriptor);
            }
            catch (InvalidOperationException ex) when (desiredFeatures != WgpuFeature.None && IsUnsupportedFeatureRequest(ex))
            {
                Console.Error.WriteLine("[with_winit] Requested adapter features not supported; retrying without optional features.");
                desiredFeatures = WgpuFeature.None;
                deviceDescriptor = deviceDescriptor with { RequiredFeatures = desiredFeatures };
                _wgpuDevice = _wgpuAdapter.RequestDevice(deviceDescriptor);
            }

            _deviceFeatures = _wgpuDevice.GetFeatures();
            _wgpuQueue = _wgpuDevice.GetQueue();
            _gpuProfilerSupported = HasFeature(_deviceFeatures, WgpuFeature.TimestampQuery)
                                     && HasFeature(_deviceFeatures, WgpuFeature.PipelineStatisticsQuery);
            InitializePipelineCache();
            _wgpuRenderer = new WgpuRenderer(_wgpuDevice, BuildRendererOptions());
            if (_gpuProfilingEnabled)
            {
                if (!_wgpuRenderer.SetProfilerEnabled(true))
                {
                    _gpuProfilingEnabled = false;
                    _gpuProfilerSupported = false;
                    Console.WriteLine("[with_winit] GPU profiling is not supported on this platform/build.");
                }
            }
        }
    }

    private void ConfigureSurface(uint width, uint height)
    {
        if (_wgpuSurface is null || _wgpuAdapter is null || _wgpuDevice is null)
        {
            return;
        }

        _surfaceFormat = _wgpuSurface.GetPreferredFormat(_wgpuAdapter);
        _requiresSurfaceBlit = RequiresSurfaceBlit(_surfaceFormat);

        _surfaceConfig = new WgpuSurfaceConfiguration
        {
            Usage = WgpuTextureUsage.RenderAttachment,
            Format = _surfaceFormat,
            Width = width,
            Height = height,
            PresentMode = _vsyncOn ? PresentMode.AutoVsync : PresentMode.AutoNoVsync,
            AlphaMode = WgpuCompositeAlphaMode.Auto,
            ViewFormats = null,
        };

        _wgpuSurface.Configure(_wgpuDevice, _surfaceConfig);
        _surfaceValid = true;
    }

    private RendererOptions BuildRendererOptions()
    {
        var initThreads = OperatingSystem.IsMacOS() ? 1 : 0;
        var supportMsaa8 = SupportsAa(AntialiasingMode.Msaa8);
        var supportMsaa16 = SupportsAa(AntialiasingMode.Msaa16);
        return RendererOptionsExtensions.CreateGpuOptions(
            useCpu: false,
            supportArea: SupportsAa(AntialiasingMode.Area),
            supportMsaa8: supportMsaa8,
            supportMsaa16: supportMsaa16,
            initThreads: initThreads == 0 ? null : initThreads,
            pipelineCache: _pipelineCache);
    }

    private SurfaceDescriptor CreateSurfaceDescriptor(WinitWindow window, uint width, uint height)
    {
        var handle = window.GetVelloWindowHandle();
        return new SurfaceDescriptor
        {
            Width = width,
            Height = height,
            PresentMode = _vsyncOn ? PresentMode.AutoVsync : PresentMode.AutoNoVsync,
            Handle = SurfaceHandle.FromVelloHandle(handle),
        };
    }

    private void RenderFrame()
    {
        if (_wgpuSurface is null || _wgpuRenderer is null || _wgpuDevice is null || !_surfaceValid || _host is null || _window is null)
        {
            return;
        }

        var elapsedMs = _frameTimer.Elapsed.TotalMilliseconds;
        _frameTimer.Restart();
        if (elapsedMs > 0.0)
        {
            _frameStats.AddSample(elapsedMs);
        }

        var elapsedSeconds = _clock.Elapsed.TotalSeconds;
        if (_scenes.Count == 0)
        {
            return;
        }

        var index = Math.Clamp(_sceneIndex, 0, _scenes.Count - 1);
        var result = _host.Render(index, _scene, elapsedSeconds, true, _complexity, _viewTransform);
        _sceneResolution = result.Resolution;
        if (result.BaseColor.HasValue)
        {
            _baseColor = result.BaseColor.Value;
        }

        if (_statsVisible)
        {
            DrawOverlay();
        }

        WgpuSurfaceTexture? surfaceTexture = null;
        WgpuTextureView? textureView = null;
        try
        {
            surfaceTexture = _wgpuSurface.AcquireNextTexture();
            textureView = surfaceTexture.CreateView();

        var renderFormat = _requiresSurfaceBlit
            ? RenderFormat.Rgba8
            : _surfaceFormat switch
            {
                WgpuTextureFormat.Rgba8Unorm or WgpuTextureFormat.Rgba8UnormSrgb => RenderFormat.Rgba8,
                _ => RenderFormat.Bgra8,
            };

        var renderParams = new RenderParams(_surfaceConfig.Width, _surfaceConfig.Height, _baseColor)
        {
            Antialiasing = _aaMode,
            Format = renderFormat,
        };

            if (_requiresSurfaceBlit)
            {
                _wgpuRenderer.RenderSurface(_scene, textureView, renderParams, _surfaceFormat);
            }
            else
            {
                _wgpuRenderer.Render(_scene, textureView, renderParams);
            }
            if (_gpuProfilingEnabled)
            {
                UpdateGpuProfilerFrame();
            }
            surfaceTexture.Present();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[with_winit] Render failed: {ex.Message}");
            _surfaceValid = false;
        }
        finally
        {
            textureView?.Dispose();
            surfaceTexture?.Dispose();
        }

        UpdateWindowTitle();
        if (_scenes[index].Animated || _statsVisible)
        {
            RequestRedraw();
        }
    }

    private void DrawOverlay()
    {
        if (_host is null)
        {
            return;
        }

        var stats = _frameStats.Snapshot;
        var lines = new List<string>
        {
            $"Scene: {_scenes[_sceneIndex].Name} ({_sceneIndex + 1}/{Math.Max(1, _scenes.Count)})",
            $"FPS: {stats.Fps:0.0}",
            $"Frame: {stats.FrameTimeMs:0.00} ms (min {stats.MinFrameTimeMs:0.00}, max {stats.MaxFrameTimeMs:0.00})",
            $"Complexity: {_complexity}",
            $"Antialiasing: {DescribeAa(_aaMode)}",
            $"VSync: {(_vsyncOn ? "On" : "Off")}",
            $"Viewport: {_width}x{_height}",
        };

        if (_sceneResolution is Vector2 resolution)
        {
            lines.Add($"Scene bounds: {resolution.X:0}x{resolution.Y:0}");
        }

        if (_adapterInfo is WgpuAdapterInfo info)
        {
            lines.Add($"Adapter: {info.Backend} (0x{info.Vendor:X4}:0x{info.Device:X4})");
        }

        if (_gpuProfilingEnabled)
        {
            if (!_gpuProfilerSupported)
            {
                lines.Add("GPU profiler unavailable");
            }
            else if (_gpuProfilerFrame is GpuProfilerFrame frame)
            {
                lines.Add($"GPU frame: {frame.TotalMilliseconds:0.000} ms");
                foreach (var slice in frame.Slices)
                {
                    if (!slice.HasTime)
                    {
                        continue;
                    }

                    var indentDepth = (int)Math.Min(slice.Depth, 8);
                    var indent = indentDepth > 0 ? new string(' ', indentDepth * 2) : string.Empty;
                    lines.Add($"{indent}{slice.Label}: {slice.DurationMilliseconds:0.000} ms");
                    if (lines.Count >= 18)
                    {
                        break;
                    }
                }
            }
        }

        const float margin = 24f;
        const float padding = 16f;
        const float lineHeight = 28f;
        const float boxWidth = 380f;
        var boxHeight = padding * 2f + lineHeight * lines.Count;

        _overlayPath.Clear();
        _overlayPath.MoveTo(margin, margin)
            .LineTo(margin + boxWidth, margin)
            .LineTo(margin + boxWidth, margin + boxHeight)
            .LineTo(margin, margin + boxHeight)
            .Close();

        var overlayBg = new SolidColorBrush(RgbaColor.FromBytes(12, 12, 16, 200));
        var overlayFg = new SolidColorBrush(RgbaColor.FromBytes(235, 235, 235));
        _scene.FillPath(_overlayPath, FillRule.NonZero, Matrix3x2.Identity, overlayBg);

        for (var i = 0; i < lines.Count; i++)
        {
            var y = margin + padding + i * lineHeight;
            var transform = Matrix3x2.CreateTranslation(margin + padding, y + lineHeight * 0.8f);
            _host.Text.Add(_scene, 18f, overlayFg.Color, transform, null, lines[i]);
        }
    }

    private void InitializeScenes(SampleOptions options)
    {
        var assetRoot = ResolveAssetRoot(options.AssetRoot);
        if (!options.UseTestScenes)
        {
            Console.WriteLine("[with_winit] Only the test scene collection is currently available; defaulting to --test-scenes.");
        }

        _host = ManagedSceneHost.Create(assetRoot);
        _scenes = _host.Scenes;
        if (_scenes.Count > 0)
        {
            _sceneIndex = options.InitialScene.HasValue
                ? Math.Clamp(options.InitialScene.Value, 0, _scenes.Count - 1)
                : 0;
        }
    }

    private static string? ResolveAssetRoot(string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested) && Directory.Exists(requested))
        {
            return requested;
        }

        var candidate = Path.Combine(AppContext.BaseDirectory, "Assets", "vello");
        return Directory.Exists(candidate) ? candidate : null;
    }

    private void InitializePipelineCache()
    {
        if (_wgpuDevice is null || _wgpuAdapter is null)
        {
            return;
        }

        if (_adapterInfo is not WgpuAdapterInfo info)
        {
            return;
        }

        if (info.Backend != WgpuBackendType.Vulkan || (_deviceFeatures & WgpuFeature.PipelineCache) == 0)
        {
            return;
        }

        var directory = GetPipelineCacheDirectory();
        if (directory is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(directory);
            var fileName = $"wgpu_pipeline_cache_vulkan_{info.Vendor}_{info.Device}";
            var path = Path.Combine(directory, fileName);
            byte[]? bytes = null;
            if (File.Exists(path))
            {
                try
                {
                    bytes = File.ReadAllBytes(path);
                    Console.WriteLine($"[with_winit] Loaded pipeline cache from '{path}'.");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[with_winit] Failed to load pipeline cache: {ex.Message}");
                }
            }

            var data = bytes is null ? ReadOnlyMemory<byte>.Empty : new ReadOnlyMemory<byte>(bytes);
            var descriptor = new WgpuPipelineCacheDescriptor("with_winit.pipeline_cache", data, fallback: true);
            _pipelineCache = _wgpuDevice.CreatePipelineCache(descriptor);
            _pipelineCachePath = path;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[with_winit] Pipeline cache initialization failed: {ex.Message}");
            _pipelineCache = null;
            _pipelineCachePath = null;
        }
    }

    private static string? GetPipelineCacheDirectory()
    {
        try
        {
            if (OperatingSystem.IsAndroid())
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(appData))
                {
                    return Path.Combine(appData, "VelloSharp", "PipelineCache");
                }
            }
            else
            {
                var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(baseDir))
                {
                    return Path.Combine(baseDir, "VelloSharp", "PipelineCache");
                }
            }
        }
        catch
        {
            // Ignore directory resolution failures.
        }

        return null;
    }

    private void SavePipelineCache()
    {
        if (_pipelineCache is null || string.IsNullOrEmpty(_pipelineCachePath))
        {
            return;
        }

        try
        {
            var data = _pipelineCache.GetData();
            if (data.Length == 0)
            {
                return;
            }

            var tempPath = _pipelineCachePath + ".new";
            Directory.CreateDirectory(Path.GetDirectoryName(_pipelineCachePath)!);
            File.WriteAllBytes(tempPath, data);
            File.Move(tempPath, _pipelineCachePath!, overwrite: true);
            Console.WriteLine($"[with_winit] Saved pipeline cache to '{_pipelineCachePath}'.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[with_winit] Failed to persist pipeline cache: {ex.Message}");
        }
    }

    private void ToggleVsync()
    {
        if (_wgpuSurface is null)
        {
            return;
        }

        _vsyncOn = !_vsyncOn;
        ConfigureSurface(_width, _height);
        UpdateWindowTitle();
        RequestRedraw();
    }

    private void ToggleGpuProfiler()
    {
        if (_wgpuRenderer is null)
        {
            return;
        }

        if (!_gpuProfilerSupported && !_gpuProfilingEnabled)
        {
            Console.WriteLine("[with_winit] GPU profiling is not supported on this platform/build.");
            return;
        }

        var next = !_gpuProfilingEnabled;
        var supported = _wgpuRenderer.SetProfilerEnabled(next);
        if (!supported)
        {
            if (next)
            {
                Console.WriteLine("[with_winit] GPU profiling is not supported on this platform/build.");
            }
            _gpuProfilingEnabled = false;
            _gpuProfilerSupported = false;
            _gpuProfilerFrame = null;
            return;
        }

        _gpuProfilingEnabled = next;
        if (!next)
        {
            _gpuProfilerFrame = null;
        }
    }

    private void UpdateGpuProfilerFrame()
    {
        if (!_gpuProfilingEnabled || _wgpuRenderer is null)
        {
            return;
        }

        try
        {
            var frame = _wgpuRenderer.TryGetProfilerFrame();
            if (frame.HasValue)
            {
                _gpuProfilerFrame = frame.Value;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[with_winit] Failed to read GPU profiler results: {ex.Message}");
            _gpuProfilingEnabled = false;
            _gpuProfilerFrame = null;
        }
    }

    private void ChangeSceneSaturating(int delta)
    {
        if (_scenes.Count == 0)
        {
            return;
        }

        var next = Math.Clamp(_sceneIndex + delta, 0, _scenes.Count - 1);
        if (next != _sceneIndex)
        {
            _sceneIndex = next;
            _sceneResolution = null;
            UpdateWindowTitle();
            RequestRedraw();
        }
    }

    private void ChangeScene(int delta)
    {
        if (_scenes.Count == 0)
        {
            return;
        }

        var next = (_sceneIndex + delta) % _scenes.Count;
        if (next < 0)
        {
            next += _scenes.Count;
        }
        if (_sceneIndex != next)
        {
            _sceneIndex = next;
            _sceneResolution = null;
            UpdateWindowTitle();
            RequestRedraw();
        }
    }

    private void ChangeComplexity(int delta)
    {
        var next = Math.Clamp(_complexity + delta, 1, MaxComplexity);
        if (next != _complexity)
        {
            _complexity = next;
            RequestRedraw();
        }
    }

    private void ResetView()
    {
        _viewTransform = Matrix3x2.Identity;
        RequestRedraw();
    }

    private void CycleAaMode(bool reverse)
    {
        if (_supportedAaModes.Length <= 1)
        {
            return;
        }

        var index = Array.IndexOf(_supportedAaModes, _aaMode);
        if (index < 0)
        {
            index = 0;
        }
        index = reverse ? index - 1 : index + 1;
        if (index < 0)
        {
            index = _supportedAaModes.Length - 1;
        }
        else if (index >= _supportedAaModes.Length)
        {
            index = 0;
        }

        _aaMode = _supportedAaModes[index];
        UpdateWindowTitle();
        RequestRedraw();
    }

    private void ApplyTranslation(Vector2 delta)
    {
        _viewTransform = Matrix3x2.CreateTranslation(delta) * _viewTransform;
        RequestRedraw();
    }

    private void ApplyScale(float scale, Vector2 origin)
    {
        var translation = Matrix3x2.CreateTranslation(-origin) *
                           Matrix3x2.CreateScale(scale) *
                           Matrix3x2.CreateTranslation(origin);
        _viewTransform = translation * _viewTransform;
        RequestRedraw();
    }

    private void ApplyRotation(float angle, Vector2 origin)
    {
        var rotation = Matrix3x2.CreateTranslation(-origin) *
                       Matrix3x2.CreateRotation(angle) *
                       Matrix3x2.CreateTranslation(origin);
        _viewTransform = rotation * _viewTransform;
        RequestRedraw();
    }

    private void DestroySurface()
    {
        _wgpuSurface?.Dispose();
        _wgpuSurface = null;
        _surfaceValid = false;
    }

    private void DestroyAllGpuResources()
    {
        DestroySurface();
        _wgpuRenderer?.Dispose();
        _wgpuRenderer = null;
        SavePipelineCache();
        _pipelineCache?.Dispose();
        _pipelineCache = null;
        _wgpuQueue?.Dispose();
        _wgpuQueue = null;
        _wgpuDevice?.Dispose();
        _wgpuDevice = null;
        _wgpuAdapter?.Dispose();
        _wgpuAdapter = null;
        _wgpuInstance?.Dispose();
        _wgpuInstance = null;
        _adapterInfo = null;
    }

    private void RequestRedraw() => _needsRedraw = true;

    private static void EnsureSuccess(WinitStatus status, string message)
    {
        if (status == WinitStatus.Success)
        {
            return;
        }

        throw new InvalidOperationException($"{message}: {status}");
    }

    private void UpdateWindowTitle()
    {
        if (_window is null)
        {
            return;
        }

        try
        {
            _window.SetTitle(BuildWindowTitle());
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[with_winit] Failed to update window title: {ex.Message}");
        }
    }

    private AntialiasingMode ClampAaMode(AntialiasingMode desired)
        => Array.IndexOf(_supportedAaModes, desired) >= 0 ? desired : _supportedAaModes[0];

    private static AntialiasingMode[] BuildSupportedAaModes()
    {
        if (OperatingSystem.IsAndroid())
        {
            return new[] { AntialiasingMode.Area };
        }

        return new[]
        {
            AntialiasingMode.Area,
            AntialiasingMode.Msaa8,
            AntialiasingMode.Msaa16,
        };
    }

    private bool SupportsAa(AntialiasingMode mode)
        => Array.IndexOf(_supportedAaModes, mode) >= 0;

    private static WgpuAdapterInfo? TryGetAdapterInfo(WgpuAdapter adapter)
    {
        if (adapter is null)
        {
            return null;
        }

        var method = adapter.GetType().GetMethod("GetInfo", Type.EmptyTypes);
        if (method is null)
        {
            return null;
        }

        try
        {
            var result = method.Invoke(adapter, null);
            return result is WgpuAdapterInfo info ? info : (WgpuAdapterInfo?)null;
        }
        catch
        {
            return null;
        }
    }

    private static WgpuFeature TryGetAdapterFeatures(WgpuAdapter adapter)
    {
        if (adapter is null)
        {
            return WgpuFeature.None;
        }

        var method = adapter.GetType().GetMethod("GetFeatures", Type.EmptyTypes);
        if (method is null)
        {
            return WgpuFeature.None;
        }

        try
        {
            var result = method.Invoke(adapter, null);
            return result is WgpuFeature feature ? feature : WgpuFeature.None;
        }
        catch
        {
            return WgpuFeature.None;
        }
    }

    private static bool HasFeature(WgpuFeature features, WgpuFeature flag)
        => (features & flag) == flag;

    private static bool IsUnsupportedFeatureRequest(Exception ex)
        => ex.Message.Contains("Unsupported feature request", StringComparison.OrdinalIgnoreCase);

    private static bool RequiresSurfaceBlit(WgpuTextureFormat format) => format switch
    {
        WgpuTextureFormat.Rgba8Unorm => false,
        _ => true,
    };

    private string BuildWindowTitle()
    {
        var sceneName = _scenes.Count > 0 ? _scenes[_sceneIndex].Name : "No scenes";
        var resolution = _sceneResolution.HasValue
            ? $"{_sceneResolution.Value.X:0}x{_sceneResolution.Value.Y:0}"
            : $"{_width}x{_height}";
        var aaName = DescribeAa(_aaMode);
        return $"VelloSharp With Winit - {sceneName} ({_sceneIndex + 1}/{Math.Max(1, _scenes.Count)}) - {resolution} - AA: {aaName} - VSync: {(_vsyncOn ? "On" : "Off")}";
    }

    private static string DescribeAa(AntialiasingMode mode) => mode switch
    {
        AntialiasingMode.Msaa8 => "8xMSAA",
        AntialiasingMode.Msaa16 => "16xMSAA",
        _ => "Area",
    };

    private readonly struct MultiTouchInfo
    {
        public MultiTouchInfo(
            int touchCount,
            double zoomDelta,
            Vector2 zoomDelta2D,
            double rotationDelta,
            Vector2 translationDelta,
            Vector2 zoomCenter)
        {
            TouchCount = touchCount;
            ZoomDelta = zoomDelta;
            ZoomDelta2D = zoomDelta2D;
            RotationDelta = rotationDelta;
            TranslationDelta = translationDelta;
            ZoomCenter = zoomCenter;
        }

        public int TouchCount { get; }
        public double ZoomDelta { get; }
        public Vector2 ZoomDelta2D { get; }
        public double RotationDelta { get; }
        public Vector2 TranslationDelta { get; }
        public Vector2 ZoomCenter { get; }
    }

    private sealed class TouchTracker
    {
        private readonly Dictionary<ulong, ActiveTouch> _activeTouches = new();
        private GestureState? _gestureState;
        private bool _touchCountChanged;

        public void AddEvent(in WinitEventArgs args)
        {
            var position = new TouchVector(args.MouseX, args.MouseY);
            switch (args.TouchPhase)
            {
                case WinitTouchPhaseKind.Started:
                    _activeTouches[args.TouchId] = new ActiveTouch(position);
                    _touchCountChanged = true;
                    break;
                case WinitTouchPhaseKind.Moved:
                    if (_activeTouches.TryGetValue(args.TouchId, out var touch))
                    {
                        touch.Position = position;
                    }
                    break;
                case WinitTouchPhaseKind.Ended:
                case WinitTouchPhaseKind.Cancelled:
                    if (_activeTouches.Remove(args.TouchId))
                    {
                        _touchCountChanged = true;
                    }
                    break;
            }
        }

        public MultiTouchInfo? EndFrame()
        {
            UpdateGesture();

            if (_touchCountChanged)
            {
                if (_gestureState is { } state)
                {
                    state.Previous = null;
                    state.PinchType = ClassifyPinch(_activeTouches);
                }

                _touchCountChanged = false;
            }

            return BuildInfo();
        }

        private void UpdateGesture()
        {
            if (TryCalculateDynamicState(out var dynamicState))
            {
                if (_gestureState is { } state)
                {
                    state.Previous = state.Current;
                    state.Current = dynamicState;
                }
                else
                {
                    _gestureState = new GestureState
                    {
                        PinchType = ClassifyPinch(_activeTouches),
                        Current = dynamicState,
                    };
                }
            }
            else
            {
                _gestureState = null;
            }
        }

        private MultiTouchInfo? BuildInfo()
        {
            if (_gestureState is not { } state)
            {
                return null;
            }

            var previous = state.Previous ?? state.Current;
            var touchCount = _activeTouches.Count;

            if (touchCount == 0)
            {
                return null;
            }

            var zoomDelta = touchCount > 1
                ? SafeRatio(state.Current.AvgDistance, previous.AvgDistance)
                : 1.0;

            Vector2 zoomDelta2D;
            if (touchCount > 1)
            {
                zoomDelta2D = state.PinchType switch
                {
                    PinchType.Horizontal => new Vector2(
                        (float)SafeRatio(state.Current.AvgAbsDistance2.X, previous.AvgAbsDistance2.X),
                        1f),
                    PinchType.Vertical => new Vector2(
                        1f,
                        (float)SafeRatio(state.Current.AvgAbsDistance2.Y, previous.AvgAbsDistance2.Y)),
                    _ => new Vector2((float)zoomDelta, (float)zoomDelta),
                };
            }
            else
            {
                zoomDelta2D = Vector2.One;
            }

            var translationDelta = ToVector2(state.Current.AvgPos - previous.AvgPos);
            var rotationDelta = state.Current.Heading - previous.Heading;
            var zoomCentre = ToVector2(state.Current.AvgPos);

            return new MultiTouchInfo(
                touchCount,
                zoomDelta,
                zoomDelta2D,
                rotationDelta,
                translationDelta,
                zoomCentre);
        }

        private bool TryCalculateDynamicState(out DynamicGestureState state)
        {
            var count = _activeTouches.Count;
            if (count == 0)
            {
                state = default;
                return false;
            }

            state = new DynamicGestureState
            {
                AvgDistance = 0.0,
                AvgAbsDistance2 = new TouchVector(0.0, 0.0),
                AvgPos = new TouchVector(0.0, 0.0),
                Heading = 0.0,
            };

            var reciprocal = 1.0 / count;

            foreach (var touch in _activeTouches.Values)
            {
                var pos = touch.Position;
                state.AvgPos.X += pos.X;
                state.AvgPos.Y += pos.Y;
            }

            state.AvgPos.X *= reciprocal;
            state.AvgPos.Y *= reciprocal;

            foreach (var touch in _activeTouches.Values)
            {
                var pos = touch.Position;
                state.AvgDistance += Distance(state.AvgPos, pos);
                state.AvgAbsDistance2.X += Math.Abs(state.AvgPos.X - pos.X);
                state.AvgAbsDistance2.Y += Math.Abs(state.AvgPos.Y - pos.Y);
            }

            state.AvgDistance *= reciprocal;
            state.AvgAbsDistance2.X *= reciprocal;
            state.AvgAbsDistance2.Y *= reciprocal;

            var enumerator = _activeTouches.Values.GetEnumerator();
            if (enumerator.MoveNext())
            {
                var first = enumerator.Current.Position;
                state.Heading = Math.Atan2(state.AvgPos.Y - first.Y, state.AvgPos.X - first.X);
            }

            return true;
        }

        private static double Distance(TouchVector a, TouchVector b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double SafeRatio(double numerator, double denominator)
        {
            if (Math.Abs(denominator) < double.Epsilon)
            {
                return 1.0;
            }

            var value = numerator / denominator;
            return double.IsFinite(value) ? value : 1.0;
        }

        private static Vector2 ToVector2(TouchVector vector)
            => new((float)vector.X, (float)vector.Y);

        private static PinchType ClassifyPinch(Dictionary<ulong, ActiveTouch> touches)
        {
            if (touches.Count == 2)
            {
                var enumerator = touches.Values.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    var first = enumerator.Current.Position;
                    if (enumerator.MoveNext())
                    {
                        var second = enumerator.Current.Position;

                        var dx = Math.Abs(first.X - second.X);
                        var dy = Math.Abs(first.Y - second.Y);

                        if (dx > 3.0 * dy)
                        {
                            return PinchType.Horizontal;
                        }

                        if (dy > 3.0 * dx)
                        {
                            return PinchType.Vertical;
                        }
                    }
                }
            }

            return PinchType.Proportional;
        }

        private sealed class ActiveTouch
        {
            public ActiveTouch(TouchVector position)
            {
                Position = position;
            }

            public TouchVector Position { get; set; }
        }

        private sealed class GestureState
        {
            public PinchType PinchType { get; set; }
            public DynamicGestureState Current { get; set; }
            public DynamicGestureState? Previous { get; set; }
        }

        private struct DynamicGestureState
        {
            public double AvgDistance;
            public TouchVector AvgAbsDistance2;
            public TouchVector AvgPos;
            public double Heading;
        }

        private struct TouchVector
        {
            public TouchVector(double x, double y)
            {
                X = x;
                Y = y;
            }

            public double X;
            public double Y;

            public static TouchVector operator -(TouchVector left, TouchVector right)
                => new(left.X - right.X, left.Y - right.Y);
        }
    }

    private enum PinchType
    {
        Horizontal,
        Vertical,
        Proportional,
    }

    public void Dispose()
    {
        DestroyAllGpuResources();
        _host?.Dispose();
        _host = null;
        _scene.Dispose();
        GC.SuppressFinalize(this);
    }

    ~WinitSampleApp()
    {
        DestroyAllGpuResources();
    }
}

internal static class HidCodes
{
    // Values mirror `winit::keyboard::KeyCode` discriminants (keyboard-types `Code`).
    public const uint ArrowLeft = 80;
    public const uint ArrowRight = 81;
    public const uint ArrowUp = 82;
    public const uint ArrowDown = 79;
    public const uint Space = 62;
    public const uint KeyQ = 35;
    public const uint KeyE = 23;
    public const uint KeyS = 37;
    public const uint KeyC = 21;
    public const uint KeyG = 25;
    public const uint KeyM = 31;
    public const uint KeyV = 40;
}

internal sealed class FrameStats
{
    private const int WindowSize = 120;

    private readonly Queue<double> _samples = new();
    private double _sum;
    private double _min = double.PositiveInfinity;
    private double _max = double.NegativeInfinity;
    private double _last;

    public void AddSample(double frameTimeMs)
    {
        if (!double.IsFinite(frameTimeMs) || frameTimeMs <= 0)
        {
            return;
        }

        _samples.Enqueue(frameTimeMs);
        _sum += frameTimeMs;
        _last = frameTimeMs;
        if (_samples.Count > WindowSize)
        {
            var removed = _samples.Dequeue();
            _sum -= removed;
            if (removed == _min || removed == _max)
            {
                RecalculateExtrema();
            }
        }

        if (frameTimeMs < _min)
        {
            _min = frameTimeMs;
        }
        if (frameTimeMs > _max)
        {
            _max = frameTimeMs;
        }
    }

    public FrameStatsSnapshot Snapshot
    {
        get
        {
            if (_samples.Count == 0)
            {
                return new FrameStatsSnapshot(0, 0, 0, 0, Array.Empty<double>());
            }

            var avg = _sum / _samples.Count;
            var fps = avg > 0 ? 1000.0 / avg : 0;
            return new FrameStatsSnapshot(
                fps,
                _last,
                double.IsPositiveInfinity(_min) ? 0 : _min,
                double.IsNegativeInfinity(_max) ? 0 : _max,
                _samples.ToArray());
        }
    }

    public void Clear()
    {
        _samples.Clear();
        _sum = 0;
        _min = double.PositiveInfinity;
        _max = double.NegativeInfinity;
        _last = 0;
    }

    public void ClearExtrema()
    {
        _min = double.PositiveInfinity;
        _max = double.NegativeInfinity;
        RecalculateExtrema();
    }

    private void RecalculateExtrema()
    {
        if (_samples.Count == 0)
        {
            _min = double.PositiveInfinity;
            _max = double.NegativeInfinity;
            return;
        }

        _min = double.PositiveInfinity;
        _max = double.NegativeInfinity;
        foreach (var sample in _samples)
        {
            if (sample < _min)
            {
                _min = sample;
            }
            if (sample > _max)
            {
                _max = sample;
            }
        }
    }
}

internal readonly record struct FrameStatsSnapshot(
    double Fps,
    double FrameTimeMs,
    double MinFrameTimeMs,
    double MaxFrameTimeMs,
    IReadOnlyList<double> Samples);
