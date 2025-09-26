using System;
using System.Diagnostics;
using System.Numerics;
using VelloSharp;

namespace VelloSharp.WithWinit;

internal static class Program
{
    private static int Main()
    {
        try
        {
            using var app = new WinitSampleApp();
            app.Run();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

internal sealed class WinitSampleApp : IWinitEventHandler, IDisposable
{
    private readonly WinitEventLoop _eventLoop = new();
    private readonly VelloSurfaceContext _surfaceContext = new();
    private readonly Scene _scene = new();
    private readonly PathBuilder _backgroundPath = new();
    private readonly PathBuilder _shapePath = new();
    private readonly StrokeStyle _outlineStyle = new()
    {
        Width = 6.0,
        LineJoin = LineJoin.Round,
        StartCap = LineCap.Round,
        EndCap = LineCap.Round,
    };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly RgbaColor _baseColor = RgbaColor.FromBytes(18, 23, 32);
    private readonly SolidColorBrush _backgroundBrush;
    private readonly SolidColorBrush _outlineBrush = new(RgbaColor.FromBytes(255, 255, 255, 64));
    private readonly GradientStop[] _palette =
    {
        new GradientStop(0f, RgbaColor.FromBytes(255, 126, 64)),
        new GradientStop(0.5f, RgbaColor.FromBytes(189, 76, 233)),
        new GradientStop(1f, RgbaColor.FromBytes(64, 162, 255)),
    };
    private readonly PenikoColorStop[] _penikoStops;

    private VelloSurface? _surface;
    private VelloSurfaceRenderer? _renderer;
    private WinitWindow? _window;
    private uint _width = 1;
    private uint _height = 1;
    private double _scaleFactor = 1.0;
    private bool _isExiting;

    public WinitSampleApp()
    {
        _backgroundBrush = new SolidColorBrush(_baseColor);
        _penikoStops = new PenikoColorStop[_palette.Length];
        for (var i = 0; i < _palette.Length; i++)
        {
            var stop = _palette[i];
            _penikoStops[i] = new PenikoColorStop
            {
                Offset = stop.Offset,
                Color = new VelloColor
                {
                    R = stop.Color.R,
                    G = stop.Color.G,
                    B = stop.Color.B,
                    A = stop.Color.A,
                },
            };
        }
    }

    public void Run()
    {
        var configuration = new WinitRunConfiguration
        {
            CreateWindow = true,
            Window = new WinitWindowOptions
            {
                Width = 1280,
                Height = 720,
                Title = "VelloSharp Winit Sample",
                Visible = true,
                Decorations = true,
                Resizable = true,
            },
        };

        var status = _eventLoop.Run(configuration, this);
        if (status != WinitStatus.Success)
        {
            throw new InvalidOperationException($"Event loop exited with status {status}.");
        }
    }

    public void HandleEvent(WinitEventLoopContext context, in WinitEventArgs args)
    {
        switch (args.Kind)
        {
            case WinitEventKind.WindowCreated:
                OnWindowCreated(context, args);
                break;
            case WinitEventKind.WindowResized:
                OnWindowResized(args);
                break;
            case WinitEventKind.WindowScaleFactorChanged:
                OnScaleFactorChanged(args);
                break;
            case WinitEventKind.WindowRedrawRequested:
                RenderFrame();
                break;
            case WinitEventKind.WindowCloseRequested:
                _isExiting = true;
                context.Exit();
                break;
            case WinitEventKind.WindowDestroyed:
                DestroySurface();
                _window = null;
                break;
            case WinitEventKind.AboutToWait:
                RequestNextFrame();
                break;
            case WinitEventKind.Exiting:
                _isExiting = true;
                break;
        }
    }

    private void OnWindowCreated(WinitEventLoopContext context, in WinitEventArgs args)
    {
        _window = context.GetWindow() ?? throw new InvalidOperationException("Window handle unavailable.");
        context.SetControlFlow(WinitControlFlow.Poll);

        var size = _window.GetSurfaceSize();
        _width = size.Width > 0 ? size.Width : Math.Max(args.Width, 1u);
        _height = size.Height > 0 ? size.Height : Math.Max(args.Height, 1u);
        _scaleFactor = args.ScaleFactor > 0 ? args.ScaleFactor : _scaleFactor;

        CreateSurface(_window, _width, _height);
        UpdateWindowTitle();
        RequestNextFrame();
    }

    private void OnWindowResized(in WinitEventArgs args)
    {
        if (args.Width == 0 || args.Height == 0)
        {
            return;
        }

        _width = args.Width;
        _height = args.Height;
        _surface?.Resize(_width, _height);
        UpdateWindowTitle();
        RequestNextFrame();
    }

    private void OnScaleFactorChanged(in WinitEventArgs args)
    {
        if (args.ScaleFactor > 0)
        {
            _scaleFactor = args.ScaleFactor;
            UpdateWindowTitle();
        }
    }

    private void RenderFrame()
    {
        if (_renderer is null || _surface is null || _window is null)
        {
            return;
        }

        BuildScene(_clock.Elapsed.TotalSeconds);

        var renderParams = new RenderParams(_width, _height, _baseColor)
        {
            // The embedded shaders currently ship without MSAA permutations, so clamp AA to area sampling.
            Antialiasing = AntialiasingMode.Area,
            Format = RenderFormat.Bgra8,
        };

        _renderer.Render(_surface, _scene, renderParams);
        _window.PrePresentNotify();
        RequestNextFrame();
    }

    private void RequestNextFrame()
    {
        if (_isExiting || _window is null)
        {
            return;
        }

        _window.RequestRedraw();
    }

    private void CreateSurface(WinitWindow window, uint width, uint height)
    {
        DestroySurface();

        var handle = window.GetVelloWindowHandle();
        var descriptor = new SurfaceDescriptor
        {
            Width = width,
            Height = height,
            PresentMode = PresentMode.AutoVsync,
            Handle = SurfaceHandle.FromVelloHandle(handle),
        };

        _surface = new VelloSurface(_surfaceContext, descriptor);
        _renderer = new VelloSurfaceRenderer(_surface);
    }

    private void DestroySurface()
    {
        _renderer?.Dispose();
        _renderer = null;

        _surface?.Dispose();
        _surface = null;
    }

    private void UpdateWindowTitle()
    {
        if (_window is null)
        {
            return;
        }

        var scale = _scaleFactor.ToString("0.00");
        _window.SetTitle($"VelloSharp Winit Sample - {_width}x{_height}@{scale}x");
    }

    private void BuildScene(double timeSeconds)
    {
        _scene.Reset();

        _backgroundPath.Clear();
        _backgroundPath.MoveTo(0, 0);
        _backgroundPath.LineTo(_width, 0);
        _backgroundPath.LineTo(_width, _height);
        _backgroundPath.LineTo(0, _height);
        _backgroundPath.Close();
        _scene.FillPath(_backgroundPath, FillRule.NonZero, Matrix3x2.Identity, _backgroundBrush);

        var centerX = _width / 2.0;
        var centerY = _height / 2.0;
        var radius = Math.Min(_width, _height) * 0.35;
        var wobble = radius * 0.18;
        var rotation = timeSeconds * 0.45;

        _shapePath.Clear();
        const int segments = 180;
        for (var i = 0; i <= segments; i++)
        {
            var t = i / (double)segments;
            var angle = t * Math.PI * 2.0 + rotation;
            var pulse = radius + wobble * Math.Sin(timeSeconds * 0.8 + angle * 3.0);
            var x = centerX + Math.Cos(angle) * pulse;
            var y = centerY + Math.Sin(angle) * pulse;
            if (i == 0)
            {
                _shapePath.MoveTo(x, y);
            }
            else
            {
                _shapePath.LineTo(x, y);
            }
        }
        _shapePath.Close();

        var transform =
            Matrix3x2.CreateTranslation(-(float)centerX, -(float)centerY) *
            Matrix3x2.CreateRotation((float)(rotation * 0.5)) *
            Matrix3x2.CreateTranslation((float)centerX, (float)centerY);

        var gradient = new PenikoLinearGradient
        {
            Start = new PenikoPoint
            {
                X = centerX - radius,
                Y = centerY - radius,
            },
            End = new PenikoPoint
            {
                X = centerX + radius,
                Y = centerY + radius,
            },
        };

        using var brush = PenikoBrush.CreateLinear(gradient, PenikoExtend.Pad, _penikoStops);

        _scene.FillPath(_shapePath, FillRule.NonZero, transform, brush);
        _scene.StrokePath(_shapePath, _outlineStyle, transform, _outlineBrush);
    }

    public void Dispose()
    {
        DestroySurface();
        _scene.Dispose();
        _surfaceContext.Dispose();
        _window = null;
        GC.SuppressFinalize(this);
    }
}
