using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using VelloSharp.ChartData;

namespace VelloSharp.Uno.WinAppSdkSample;

public sealed partial class MainWindow : Window
{
    private readonly DispatcherQueueTimer _timer;
    private readonly ChartSamplePoint[] _buffer = new ChartSamplePoint[1];
    private double _time;

    public MainWindow()
    {
        InitializeComponent();

        var queue = DispatcherQueue.GetForCurrentThread();
        _timer = queue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += OnTick;
        _timer.Start();

        Closed += OnClosed;
    }

    private void OnTick(DispatcherQueueTimer timer, object args)
    {
        _time += 0.016;
        var y = Math.Sin(_time) * 32.0;
        _buffer[0] = new ChartSamplePoint(0, _time, y);
        ChartHost.PublishSamples(_buffer);
    }

    private void OnClosed(object sender, WindowEventArgs e)
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
