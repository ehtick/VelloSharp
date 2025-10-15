using System;
using System.Collections.Generic;
using AvaloniaVelloSkiaSharpSample.Diagnostics;
using AvaloniaVelloSkiaSharpSample.Navigation;
using AvaloniaVelloSkiaSharpSample.Rendering;
using AvaloniaVelloSkiaSharpSample.Services;
using MiniMvvm;
using SkiaSharp;
using SampleSkiaBackendDescriptor = AvaloniaVelloSkiaSharpSample.Services.SkiaBackendDescriptor;
using SampleSkiaBackendService = AvaloniaVelloSkiaSharpSample.Services.SkiaBackendService;

namespace AvaloniaVelloSkiaSharpSample.ViewModels.Pages;

public sealed class RecordingStudioViewModel : SamplePageViewModel
{
    private readonly SkiaSceneRecorder _sceneRecorder;
    private readonly List<SKPicture> _frames = new();
    private int _activeIndex;
    private bool _animatePlayback = true;
    private string _status = "Record a frame of the procedural scene to begin playback.";

    public RecordingStudioViewModel(
        SkiaSceneRecorder sceneRecorder,
        SkiaCaptureRecorder? captureRecorder = null,
        SampleSkiaBackendService? backendService = null,
        SkiaResourceService? resourceService = null)
        : base(
            "Recording & Replay Studio",
            "Capture SKCanvas command streams into SKPicture recordings and replay them on demand.",
            null,
            captureRecorder,
            backendService,
            resourceService)
    {
        _sceneRecorder = sceneRecorder ?? throw new ArgumentNullException(nameof(sceneRecorder));
        CaptureFrameCommand = MiniCommand.Create(CaptureFrame);
        NextFrameCommand = MiniCommand.Create(MoveNextFrame);
        ClearCommand = MiniCommand.Create(ClearFrames);

        SetDocumentationLinks(
            new DocumentationLink("SkiaSharp SKPictureRecorder", new Uri("https://docs.microsoft.com/xamarin/graphics-games/skiasharp/pictures")),
            new DocumentationLink("Vello Scene Diagnostics", new Uri("https://github.com/linebender/vello")));
    }

    public MiniCommand CaptureFrameCommand { get; }

    public MiniCommand NextFrameCommand { get; }

    public MiniCommand ClearCommand { get; }

    public int RecordingCount => _frames.Count;

    public bool HasRecording => _frames.Count > 0;

    public bool CanCycle => _frames.Count > 1;

    public bool AnimatePlayback
    {
        get => _animatePlayback;
        set => SetAndRequestRender(ref _animatePlayback, value);
    }

    public string Status
    {
        get => _status;
        private set => RaiseAndSetIfChanged(ref _status, value);
    }

    public override void Render(in SkiaLeaseRenderContext context)
    {
        var canvas = context.Canvas;
        var info = context.ImageInfo;
        canvas.Clear(new SKColor(16, 20, 32, 255));

        if (_frames.Count == 0)
        {
            DrawLiveScene(canvas, info, context.Frame, context.Elapsed);
            Status = "No recordings yet – tap Capture to snapshot the live scene.";
            ProcessCapture(context);
            return;
        }

        canvas.Save();
        canvas.Translate(info.Width * 0.5f, info.Height * 0.52f);
        var scale = MathF.Min(info.Width, info.Height) / 640f;
        canvas.Scale(scale);

        if (AnimatePlayback)
        {
            var wiggle = (float)Math.Sin(context.Elapsed.TotalSeconds * 1.2) * 8f;
            canvas.RotateDegrees(wiggle);
            RequestRender();
        }

        canvas.Translate(-320f, -240f);
        var picture = _frames[_activeIndex];
        canvas.DrawPicture(picture);
        canvas.Restore();

        Status = $"Frame {_activeIndex + 1} of {_frames.Count} · Animate {(AnimatePlayback ? "On" : "Off")}";
        ProcessCapture(context);
    }

    protected override void OnBackendChanged(SampleSkiaBackendDescriptor descriptor)
    {
        base.OnBackendChanged(descriptor);
        Status = $"Backend switched to {descriptor.Title}.";
    }

    private void CaptureFrame()
    {
        var bounds = new SKRect(0, 0, 640, 480);
        var target = _sceneRecorder.BeginRecording(bounds);
        DrawRecordingScene(target, _frames.Count);
        var picture = _sceneRecorder.EndRecording();

        if (picture is null)
        {
            Status = "Recorder did not produce a frame.";
            return;
        }

        _frames.Add(picture);
        _activeIndex = _frames.Count - 1;
        Status = $"Captured frame {_frames.Count}.";
        RaisePropertyChanged(nameof(RecordingCount));
        RaisePropertyChanged(nameof(HasRecording));
        RaisePropertyChanged(nameof(CanCycle));
        RequestRender();
    }

    private void MoveNextFrame()
    {
        if (_frames.Count == 0)
        {
            return;
        }

        _activeIndex = (_activeIndex + 1) % _frames.Count;
        Status = $"Showing frame {_activeIndex + 1} of {_frames.Count}.";
        RequestRender();
    }

    private void ClearFrames()
    {
        foreach (var frame in _frames)
        {
            frame.Dispose();
        }

        _frames.Clear();
        _activeIndex = 0;
        Status = "Cleared recordings.";
        RaisePropertyChanged(nameof(RecordingCount));
        RaisePropertyChanged(nameof(HasRecording));
        RaisePropertyChanged(nameof(CanCycle));
        RequestRender();
    }

    private static void DrawLiveScene(SKCanvas canvas, SKImageInfo info, ulong frame, TimeSpan elapsed)
    {
        using var background = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(info.Width, info.Height),
                new[]
                {
                    new SKColor(34, 50, 88, 255),
                    new SKColor(12, 18, 32, 255),
                },
                null,
                SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(new SKRect(0, 0, info.Width, info.Height), background);

        var rotation = (float)(elapsed.TotalSeconds * 18.0 % 360);
        canvas.Save();
        canvas.Translate(info.Width * 0.5f, info.Height * 0.5f);
        canvas.RotateDegrees(rotation);

        using var ring = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 16f,
            Color = new SKColor(112, 224, 255, 180),
            IsAntialias = true,
        };
        canvas.DrawCircle(0, 0, Math.Min(info.Width, info.Height) * 0.35f, ring);

        using var inner = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateRadialGradient(
                new SKPoint(0, 0),
                Math.Min(info.Width, info.Height) * 0.32f,
                new[]
                {
                    new SKColor(255, 128, 196, 220),
                    new SKColor(72, 180, 255, 255),
                },
                null,
                SKShaderTileMode.Clamp),
            IsAntialias = true,
        };
        canvas.DrawCircle(0, 0, Math.Min(info.Width, info.Height) * 0.28f, inner);
        canvas.Restore();

        using var labelPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true,
            TextSize = info.Width * 0.045f,
        };
        canvas.DrawText($"Live frame {frame}", info.Width * 0.08f, info.Height * 0.85f, labelPaint);
    }

    private static void DrawRecordingScene(SKCanvas canvas, int seed)
    {
        canvas.Clear(SKColors.Transparent);
        var bounds = new SKRect(0, 0, 640, 480);

        using var backdrop = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(bounds.Left, bounds.Top),
                new SKPoint(bounds.Right, bounds.Bottom),
                new[]
                {
                    new SKColor(18, 28, 42, 255),
                    new SKColor(42, 62, 98, 255),
                },
                null,
                SKShaderTileMode.Clamp),
        };
        canvas.DrawRect(bounds, backdrop);

        var centerX = (bounds.Left + bounds.Right) * 0.5f;
        var centerY = (bounds.Top + bounds.Bottom) * 0.5f;
        canvas.Translate(centerX, centerY);
        var angle = seed * 12f;
        canvas.RotateDegrees(angle);
        canvas.Scale(0.9f);

        using var ribbonPaint = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(-220, -180),
                new SKPoint(220, 180),
                new[]
                {
                    new SKColor(255, 120, 210, 240),
                    new SKColor(96, 190, 255, 255),
                    new SKColor(56, 90, 240, 255),
                },
                null,
                SKShaderTileMode.Clamp),
        };

        using var path = new SKPath();
        path.MoveTo(-220, 0);
        path.CubicTo(new SKPoint(-160, -180), new SKPoint(160, -180), new SKPoint(220, 0));
        path.CubicTo(new SKPoint(180, 80), new SKPoint(-40, 120), new SKPoint(-20, 200));
        path.CubicTo(new SKPoint(-120, 120), new SKPoint(-220, 120), new SKPoint(-220, 0));
        path.Close();
        canvas.DrawPath(path, ribbonPaint);

        using var stroke = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 12f,
            Color = new SKColor(255, 255, 255, 48),
            IsAntialias = true,
        };
        canvas.DrawPath(path, stroke);
    }

    public override void OnDeactivated()
    {
        base.OnDeactivated();
        AnimatePlayback = false;
    }
}
