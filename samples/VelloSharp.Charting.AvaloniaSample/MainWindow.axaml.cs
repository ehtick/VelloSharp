using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using VelloSharp.ChartDiagnostics;
using VelloSharp.ChartData;
using VelloSharp.ChartEngine;
using VelloSharp.Charting.Avalonia;
using VelloSharp.Charting.AvaloniaSample.Infrastructure;
using VelloSharp.Charting.AvaloniaSample.Models;
using VelloSharp.Charting.AvaloniaSample.Services;
using VelloSharp.Charting.Styling;
using VelloSharp.Charting.Styling.Configuration;

namespace VelloSharp.Charting.AvaloniaSample;

public sealed partial class MainWindow : Window
{
    private readonly ChartEngine.ChartEngine _chartEngine;
    private readonly SampleTelemetrySink _telemetrySink;
    private readonly BinanceTradeFeed _feed = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly ChartSamplePoint[] _seriesScratch = new ChartSamplePoint[4];
    private readonly ChartSeriesOverride[] _overrideBuffer = new ChartSeriesOverride[1];
    private readonly List<ChartTheme> _availableThemes;
    private ChartTheme _activeTheme = ChartTheme.Dark;
    private bool _suppressThemeSelection;

    private string _seriesLabel = "Live price";
    private double? _seriesStrokeWidthOverride = 2.0;
    private ChartColor? _seriesColorOverride;
    private bool _suppressSliderEvent;
    private bool _suppressStyleUpdates;
    private double? _lastPrice;

    private const double ScatterThreshold = 0.35;
    private const double BarScale = 120.0;

    public MainWindow()
    {
        InitializeComponent();

        _availableThemes = LoadThemes();
        _activeTheme = _availableThemes
            .FirstOrDefault(t => string.Equals(t.Name, "Dark", StringComparison.OrdinalIgnoreCase))
            ?? _availableThemes.First();

        _chartEngine = new ChartEngine.ChartEngine(new ChartEngineOptions
        {
            VisibleDuration = TimeSpan.FromMinutes(3),
            FrameBudget = TimeSpan.FromMilliseconds(8),
            StrokeWidth = 2.0,
            ShowAxes = true,
            Palette = ToChartColors(_activeTheme.Palette.Series),
        });
        _chartEngine.ConfigureSeries(new ChartSeriesDefinition[]
        {
            new LineSeriesDefinition(0)
            {
                StrokeWidth = 2.0,
                FillOpacity = 0.18,
            },
            new AreaSeriesDefinition(1)
            {
                Baseline = 0,
                FillOpacity = 0.45,
            },
            new ScatterSeriesDefinition(2)
            {
                MarkerSize = 6.0,
            },
            new BarSeriesDefinition(3)
            {
                Baseline = 0,
                BarWidthSeconds = 8.0,
            },
        });
        _chartEngine.ApplySeriesOverrides(new[]
        {
            new ChartSeriesOverride(1)
                .WithLabel("Latency (ms)")
                .WithColor(new ChartColor(0x3A, 0xB8, 0xFF, 0xFF)),
            new ChartSeriesOverride(2)
                .WithLabel("Price spikes")
                .WithColor(new ChartColor(0xFF, 0x9E, 0x7B, 0xFF)),
            new ChartSeriesOverride(3)
                .WithLabel("Δ price (scaled)")
                .WithColor(new ChartColor(0x47, 0xE2, 0xC2, 0xFF)),
        });
        _telemetrySink = new SampleTelemetrySink(this);
        _chartEngine.SetTelemetrySink(_telemetrySink);

        ChartHost.OwnsEngine = false;
        ChartHost.ChartEngine = _chartEngine;
        ChartHost.Theme = _activeTheme;

        ThemeSelector.ItemsSource = _availableThemes;
        ThemeSelector.SelectionChanged += OnThemeSelectionChanged;
        _suppressThemeSelection = true;
        ThemeSelector.SelectedItem = _activeTheme;
        _suppressThemeSelection = false;

        StrokeSlider.ValueChanged += OnStrokeSliderValueChanged;
        HighlightToggle.IsCheckedChanged += OnHighlightToggleChanged;
        ResetStyleButton.Click += OnResetStyleClicked;

        ApplyTheme(_activeTheme, updateSelector: false);
        ApplySeriesStyle();

        _feed.TradeReceived += OnTradeReceived;
        _feed.StatusChanged += OnStatusChanged;

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        _ = Task.Run(() => _feed.RunAsync(_cts.Token));
    }

    private void OnStatusChanged(string status)
    {
        Dispatcher.UIThread.Post(() => StatusText.Text = $"Status: {status}");
    }

    private void OnTradeReceived(TradeUpdate update)
    {
        var timestampSeconds = update.EventTimeUnixMilliseconds / 1000.0;
        var count = 0;

        _seriesScratch[count++] = new ChartSamplePoint(0, timestampSeconds, update.Price);
        _seriesScratch[count++] = new ChartSamplePoint(1, timestampSeconds, update.LatencyMilliseconds);

        var lastPrice = _lastPrice;
        var delta = lastPrice.HasValue ? update.Price - lastPrice.Value : 0d;
        var magnitude = Math.Abs(delta) * BarScale;
        _seriesScratch[count++] = new ChartSamplePoint(3, timestampSeconds, magnitude);

        if (lastPrice is not null && Math.Abs(delta) >= ScatterThreshold)
        {
            _seriesScratch[count++] = new ChartSamplePoint(2, timestampSeconds, update.Price);
        }

        _lastPrice = update.Price;

        ChartHost.PublishSamples(_seriesScratch.AsSpan(0, count));

        if (!string.IsNullOrWhiteSpace(update.Symbol))
        {
            var label = $"{update.Symbol} price";
            if (!string.Equals(label, _seriesLabel, StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _seriesLabel = label;
                    ApplySeriesStyle();
                });
            }
        }

        var deltaDisplay = delta;
        Dispatcher.UIThread.Post(() =>
        {
            PriceText.Text = $"Last trade: {update.Price:0.00} {update.Symbol} (Δ {deltaDisplay:+0.000;-0.000;0.000})";
            LatencyText.Text = $"Latency: {update.LatencyMilliseconds:0.0} ms";
        });
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        _cts.Cancel();
        await _feed.DisposeAsync();
        _chartEngine.SetTelemetrySink(null);
        _chartEngine.Dispose();
        _cts.Dispose();
    }

    private void OnThemeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressThemeSelection)
        {
            return;
        }

        if (ThemeSelector.SelectedItem is ChartTheme theme)
        {
            ApplyTheme(theme, updateSelector: false);
            ApplySeriesStyle();
        }
    }

    private void OnStrokeSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppressSliderEvent)
        {
            return;
        }

        _seriesStrokeWidthOverride = e.NewValue;
        if (!_suppressStyleUpdates)
        {
            ApplySeriesStyle();
        }
    }

    private void OnHighlightToggleChanged(object? sender, RoutedEventArgs e)
    {
        _seriesColorOverride = HighlightToggle.IsChecked == true
            ? ChartColor.FromRgb(0xFF, 0xA5, 0x2C)
            : null;
        if (!_suppressStyleUpdates)
        {
            ApplySeriesStyle();
        }
    }

    private void OnResetStyleClicked(object? sender, RoutedEventArgs e)
    {
        _suppressStyleUpdates = true;
        _suppressSliderEvent = true;
        _suppressThemeSelection = true;

        StrokeSlider.Value = 2.0;
        HighlightToggle.IsChecked = false;

        var defaultTheme = _availableThemes
            .FirstOrDefault(t => string.Equals(t.Name, "Dark", StringComparison.OrdinalIgnoreCase))
            ?? _availableThemes.First();
        ThemeSelector.SelectedItem = defaultTheme;

        _suppressThemeSelection = false;
        _suppressSliderEvent = false;
        _suppressStyleUpdates = false;

        _seriesStrokeWidthOverride = null;
        _seriesColorOverride = null;
        ApplyTheme(defaultTheme, updateSelector: false);
        ApplySeriesStyle();
    }

    private void ApplyTheme(ChartTheme theme, bool updateSelector = true)
    {
        ArgumentNullException.ThrowIfNull(theme);

        _activeTheme = theme;
        ChartHost.Theme = theme;

        _chartEngine.UpdatePalette(ToChartColors(theme.Palette.Series));

        if (updateSelector)
        {
            _suppressThemeSelection = true;
            ThemeSelector.SelectedItem = theme;
            _suppressThemeSelection = false;
        }

        ChartContainer.Background = BrushFromColor(theme.Palette.Background);
        ChartContainer.BorderBrush = BrushFromColor(theme.Palette.LegendBorder);
        StatusBar.Background = BrushFromColor(theme.Palette.LegendBackground);

        var statusForeground = BrushFromColor(theme.Palette.AxisTick);
        var priceForeground = BrushFromColor(theme.Palette.Foreground);

        StatusText.Foreground = statusForeground;
        LatencyText.Foreground = statusForeground;
        FrameStatsText.Foreground = statusForeground;
        PriceText.Foreground = priceForeground;
    }

    private void ApplySeriesStyle()
    {
        if (_suppressStyleUpdates)
        {
            return;
        }

        var style = new ChartSeriesOverride(0);

        if (!string.IsNullOrWhiteSpace(_seriesLabel))
        {
            style = style.WithLabel(_seriesLabel);
        }
        else
        {
            style = style.ClearLabel();
        }

        if (_seriesStrokeWidthOverride.HasValue)
        {
            style = style.WithStrokeWidth(_seriesStrokeWidthOverride.Value);
        }
        else
        {
            style = style.ClearStrokeWidth();
        }

        if (_seriesColorOverride.HasValue)
        {
            style = style.WithColor(_seriesColorOverride.Value);
        }
        else
        {
            style = style.ClearColor();
        }

        _overrideBuffer[0] = style;
        _chartEngine.ApplySeriesOverrides(_overrideBuffer);
    }

    private static IBrush BrushFromColor(VelloSharp.Charting.Styling.RgbaColor color) =>
        new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.FromArgb(color.A, color.R, color.G, color.B));

    private static ChartColor[] ToChartColors(IReadOnlyList<VelloSharp.Charting.Styling.RgbaColor> series)
    {
        if (series.Count == 0)
        {
            return Array.Empty<ChartColor>();
        }

        var colors = new ChartColor[series.Count];
        for (var i = 0; i < series.Count; i++)
        {
            var color = series[i];
            colors[i] = new ChartColor(color.R, color.G, color.B, color.A);
        }

        return colors;
    }

    private static List<ChartTheme> LoadThemes()
    {
        var themes = new List<ChartTheme>(ChartThemeRegistry.GetThemes());
        var themePath = Path.Combine(AppContext.BaseDirectory, "themes.json");
        if (!File.Exists(themePath))
        {
            return themes;
        }

        try
        {
            var loaded = ChartThemeLoader.LoadManyFromFile(themePath);
            if (loaded.Count > 0)
            {
                ChartThemeRegistry.RegisterThemes(loaded);
                themes = new List<ChartTheme>(ChartThemeRegistry.GetThemes());
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load themes from '{themePath}': {ex.Message}");
        }

        return themes;
    }

    public void UpdateFrameStats(FrameStats stats)
    {
        FrameStatsText.Text =
            $"Frame: CPU {stats.CpuTime.TotalMilliseconds:0.0} ms | GPU {stats.GpuTime.TotalMilliseconds:0.0} ms | Queue {stats.QueueLatency.TotalMilliseconds:0.0} ms | Paths {stats.EncodedPaths}";
    }
}
