using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using Avalonia;
using Avalonia.Platform;
using VelloSharp;
using VelloSharp.Integration.Avalonia;

namespace AvaloniaWinitDemo.Controls;

public sealed class VelatoPlayerView : VelloSurfaceView
{
    public static readonly StyledProperty<Uri?> SourceProperty =
        AvaloniaProperty.Register<VelatoPlayerView, Uri?>(nameof(Source));

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<VelatoPlayerView, bool>(nameof(IsPlaying), true);

    public static readonly StyledProperty<double> PlaybackSpeedProperty =
        AvaloniaProperty.Register<VelatoPlayerView, double>(nameof(PlaybackSpeed), 1.0);

    private readonly VelatoRenderer _renderer = new();
    private VelatoComposition? _composition;
    private VelatoCompositionInfo _compositionInfo;
    private double _frameCursor;
    private double _frameSpan;

    public VelatoPlayerView()
    {
        RenderParameters = RenderParameters with
        {
            BaseColor = RgbaColor.FromBytes(10, 16, 32),
            Antialiasing = AntialiasingMode.Area,
            Format = RenderFormat.Rgba8,
        };

        IsLoopEnabled = true;
    }

    public Uri? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public double PlaybackSpeed
    {
        get => GetValue(PlaybackSpeedProperty);
        set => SetValue(PlaybackSpeedProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceProperty)
        {
            LoadComposition(change.GetNewValue<Uri?>());
        }
        else if (change.Property == PlaybackSpeedProperty)
        {
            RequestRender();
        }
        else if (change.Property == IsPlayingProperty)
        {
            RequestRender();
        }
    }

    protected override void OnRenderFrame(VelloRenderFrameContext context)
    {
        base.OnRenderFrame(context);

        var composition = _composition;
        if (composition is null)
        {
            return;
        }

        var info = _compositionInfo;
        var speed = PlaybackSpeed;
        if (!double.IsFinite(speed) || speed <= 0)
        {
            speed = 1.0;
        }

        if (IsPlaying && context.DeltaTime > TimeSpan.Zero)
        {
            var deltaFrames = context.DeltaTime.TotalSeconds * info.FrameRate * speed;
            _frameCursor += deltaFrames;

            if (_frameSpan > 0)
            {
                _frameCursor %= _frameSpan;
                if (_frameCursor < 0)
                {
                    _frameCursor += _frameSpan;
                }
            }
        }

        var currentFrame = info.StartFrame + _frameCursor;
        if (info.EndFrame > info.StartFrame)
        {
            currentFrame = Math.Clamp(currentFrame, info.StartFrame, info.EndFrame);
        }

        var compSize = info.Size;
        if (compSize.X <= 0 || compSize.Y <= 0)
        {
            compSize = new Vector2(context.Width, context.Height);
        }

        var viewport = new Vector2((float)context.Width, (float)context.Height);
        var scale = MathF.Min(viewport.X / MathF.Max(1f, compSize.X), viewport.Y / MathF.Max(1f, compSize.Y));
        if (!float.IsFinite(scale) || scale <= 0)
        {
            scale = 1f;
        }

        var scaledWidth = compSize.X * scale;
        var scaledHeight = compSize.Y * scale;
        var offsetX = (viewport.X - scaledWidth) * 0.5f;
        var offsetY = (viewport.Y - scaledHeight) * 0.5f;

        var transform = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(offsetX, offsetY);

        try
        {
            _renderer.Append(context.Scene, composition, currentFrame, transform);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to render Velato composition: {ex.Message}");
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DisposeComposition();
        _renderer.Dispose();
        base.OnDetachedFromVisualTree(e);
    }

    private void LoadComposition(Uri? uri)
    {
        DisposeComposition();

        if (uri is null)
        {
            return;
        }

        try
        {
            using var stream = AssetLoader.Open(uri);
            if (stream is null)
            {
                return;
            }

            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var data = memory.ToArray();
            if (data.Length == 0)
            {
                return;
            }

            var composition = VelatoComposition.LoadFromUtf8(data);
            _composition = composition;
            _compositionInfo = composition.Info;
            _frameSpan = Math.Max(0.0, _compositionInfo.EndFrame - _compositionInfo.StartFrame);
            _frameCursor = 0.0;

            RequestRender();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load Velato composition from '{uri}': {ex.Message}");
        }
    }

    private void DisposeComposition()
    {
        _composition?.Dispose();
        _composition = null;
        _compositionInfo = default;
        _frameCursor = 0.0;
        _frameSpan = 0.0;
    }
}
