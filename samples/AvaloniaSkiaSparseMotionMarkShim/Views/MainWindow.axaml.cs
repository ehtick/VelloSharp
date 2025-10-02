using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Rendering;
using Avalonia.Threading;
using AvaloniaSkiaSparseMotionMarkShim.Controls;

namespace AvaloniaSkiaSparseMotionMarkShim;

public partial class MainWindow : Window
{
    private readonly MotionMarkSkiaControl _motionMark;
    private readonly TextBlock _frameInfo;

    public MainWindow()
    {
        InitializeComponent();

        RendererDiagnostics.DebugOverlays = RendererDebugOverlays.Fps;
        
        _motionMark = this.FindControl<MotionMarkSkiaControl>("MotionMark")
            ?? throw new InvalidOperationException("MotionMark control not found.");
        _frameInfo = this.FindControl<TextBlock>("FrameInfo")
            ?? throw new InvalidOperationException("FrameInfo text block not found.");

        _motionMark.FrameRendered += OnFrameRendered;
    }

    private void OnFrameRendered(object? sender, FrameRenderedEventArgs e)
    {
        Dispatcher.UIThread.Post(
            () => _frameInfo.Text = $"Frame {e.FrameTimeMilliseconds:F2} ms",
            DispatcherPriority.Background);
    }

    private void OnIncreaseComplexity(object? sender, RoutedEventArgs e)
    {
        _motionMark.IncreaseComplexity();
    }

    private void OnDecreaseComplexity(object? sender, RoutedEventArgs e)
    {
        _motionMark.DecreaseComplexity();
    }

    private void OnResetComplexity(object? sender, RoutedEventArgs e)
    {
        _motionMark.ResetComplexity();
    }

    protected override void OnClosed(EventArgs e)
    {
        _motionMark.FrameRendered -= OnFrameRendered;
        base.OnClosed(e);
    }
}
