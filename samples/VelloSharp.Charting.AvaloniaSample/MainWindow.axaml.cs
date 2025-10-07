using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using VelloSharp.ChartDiagnostics;
using VelloSharp.ChartData;
using VelloSharp.ChartEngine;
using VelloSharp.ChartEngine.Annotations;
using VelloSharp.Charting.Avalonia;
using VelloSharp.Charting.AvaloniaSample.Infrastructure;
using VelloSharp.Charting.AvaloniaSample.Models;
using VelloSharp.Charting.AvaloniaSample.Services;
using VelloSharp.Charting.Styling;
using VelloSharp.Charting.Styling.Configuration;

namespace VelloSharp.Charting.AvaloniaSample;

public sealed partial class MainWindow : Window
{
    private enum ChartScenario
    {
        LiveTrades,
        PriceVolume,
        RollingHeatmap,
    }

    private sealed record ScenarioDescriptor(string Name, ChartScenario Scenario);

    private const int PriceSeriesId = 0;
    private const int LatencySeriesId = 1;
    private const int ScatterSeriesId = 2;
    private const int DeltaSeriesId = 3;
    private const int VolumeSeriesId = 4;
    private const int HeatmapSeriesBaseId = 100;
    private const int HeatmapBucketCount = 5;
    private const double ScatterThreshold = 0.35;
    private const double BarScale = 120.0;
    private const double RecentWindowSeconds = 60.0;
    private const double VolumeBucketSeconds = 15.0;
    private const double VolumeWindowSeconds = 180.0;
    private const double VolumeGradientMax = 800.0;
    private const double HeatmapBucketSeconds = 6.0;
    private const double HeatmapWindowSeconds = 120.0;

    private ChartEngine.ChartEngine _chartEngine;
    private readonly SampleTelemetrySink _telemetrySink;
    private readonly BinanceTradeFeed _feed = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly ChartSamplePoint[] _seriesScratch = new ChartSamplePoint[32];
    private readonly ChartSeriesOverride[] _overrideBuffer = new ChartSeriesOverride[1];
    private readonly List<ChartTheme> _availableThemes;
    private readonly IReadOnlyList<ScenarioDescriptor> _scenarioOptions = new[]
    {
        new ScenarioDescriptor("Live trades & latency", ChartScenario.LiveTrades),
        new ScenarioDescriptor("Price + volume histogram", ChartScenario.PriceVolume),
        new ScenarioDescriptor("Rolling heatmap (delta buckets)", ChartScenario.RollingHeatmap),
    };
    private readonly VolumeHistogramAggregator _volumeAggregator = new(VolumeSeriesId, VolumeBucketSeconds, VolumeWindowSeconds);
    private readonly HeatmapDensityAggregator _heatmapAggregator = new(HeatmapSeriesBaseId, HeatmapBucketCount, HeatmapBucketSeconds, HeatmapWindowSeconds);
    private readonly ValueColorGradient _volumeGradient = new(
        new ChartColor(0x24, 0x67, 0x8B, 0xFF),
        new ChartColor(0xF4, 0x5E, 0x8C, 0xFF),
        0.0,
        VolumeGradientMax);
    private ChartTheme _activeTheme = ChartTheme.Dark;
    private bool _suppressThemeSelection;
    private bool _suppressScenarioSelection;
    private ChartScenario _activeScenario = ChartScenario.LiveTrades;

    private string _seriesLabel = "Live price";
    private double? _seriesStrokeWidthOverride = 2.0;
    private ChartColor? _seriesColorOverride;
    private bool _suppressSliderEvent;
    private bool _suppressStyleUpdates;
    private double? _lastPrice;
    private double _lastVolumeBucket;
    private int _lastHeatmapBucketIndex = -1;
    private double _lastHeatmapIntensity;
    private CompositionAnnotationLayer? _priceWindowLayer;

    public MainWindow()
    {
        InitializeComponent();

        _availableThemes = LoadThemes();
        _activeTheme = _availableThemes
            .FirstOrDefault(t => string.Equals(t.Name, "Dark", StringComparison.OrdinalIgnoreCase))
            ?? _availableThemes.First();

        _chartEngine = CreateChartEngine();
        _telemetrySink = new SampleTelemetrySink(this);
        _chartEngine.SetTelemetrySink(_telemetrySink);

        ChartHost.OwnsEngine = false;
        ChartHost.ChartEngine = _chartEngine;
        ChartHost.Theme = _activeTheme;
        ApplyScenario(_activeScenario, resetAggregators: true);

        ThemeSelector.ItemsSource = _availableThemes;
        ThemeSelector.SelectionChanged += OnThemeSelectionChanged;
        _suppressThemeSelection = true;
        ThemeSelector.SelectedItem = _activeTheme;
        _suppressThemeSelection = false;

        ScenarioSelector.ItemTemplate = new FuncDataTemplate<ScenarioDescriptor>(
            (descriptor, _) => new TextBlock { Text = descriptor.Name });
        ScenarioSelector.ItemsSource = _scenarioOptions;
        ScenarioSelector.SelectionChanged += OnScenarioSelectionChanged;
        _suppressScenarioSelection = true;
        ScenarioSelector.SelectedItem = _scenarioOptions[0];
        _suppressScenarioSelection = false;

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
        var span = _seriesScratch.AsSpan();
        var count = 0;

        span[count++] = new ChartSamplePoint(PriceSeriesId, timestampSeconds, update.Price);

        var previousPrice = _lastPrice;
        var delta = previousPrice.HasValue ? update.Price - previousPrice.Value : 0d;
        var deltaPercent = previousPrice is { } prev && prev != 0
            ? (delta / prev) * 100.0
            : 0d;

        if (_activeScenario == ChartScenario.LiveTrades)
        {
            span[count++] = new ChartSamplePoint(LatencySeriesId, timestampSeconds, update.LatencyMilliseconds);
            var magnitude = Math.Abs(delta) * BarScale;
            span[count++] = new ChartSamplePoint(DeltaSeriesId, timestampSeconds, magnitude);

            if (previousPrice is not null && Math.Abs(delta) >= ScatterThreshold)
            {
                span[count++] = new ChartSamplePoint(ScatterSeriesId, timestampSeconds, update.Price);
            }
        }

        if (_activeScenario == ChartScenario.PriceVolume && update.Quantity > 0)
        {
            var written = _volumeAggregator.Accumulate(timestampSeconds, update.Quantity, span[count..]);
            if (written > 0)
            {
                _lastVolumeBucket = span[count].Value;
                count += written;
                ApplyVolumeStyling();
            }
        }

        if (_activeScenario == ChartScenario.RollingHeatmap && update.Quantity > 0)
        {
            var written = _heatmapAggregator.Accumulate(timestampSeconds, update.Quantity, deltaPercent, span[count..]);
            if (written > 0)
            {
                var point = span[count];
                _lastHeatmapBucketIndex = point.SeriesId - HeatmapSeriesBaseId;
                _lastHeatmapIntensity = point.Value;
                count += written;
            }
        }

        if (count > 0)
        {
            ChartHost.PublishSamples(span[..count]);
        }

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

        _lastPrice = update.Price;

        var deltaDisplay = delta;
        var percentDisplay = deltaPercent;
        var latencyDisplay = update.LatencyMilliseconds;
        var price = update.Price;
        var symbol = update.Symbol;
        Dispatcher.UIThread.Post(() =>
        {
            PriceText.Text =
                $"Last trade: {price:0.00} {symbol} (Δ {deltaDisplay:+0.000;-0.000;0.000} | {percentDisplay:+0.###;-0.###;0.###}%)";
            LatencyText.Text = _activeScenario switch
            {
                ChartScenario.LiveTrades => $"Latency: {latencyDisplay:0.0} ms",
                ChartScenario.PriceVolume => FormatVolumeStatus(),
                ChartScenario.RollingHeatmap => FormatHeatmapStatus(),
                _ => LatencyText.Text,
            };
            UpdateOverlayArtifacts(timestampSeconds, price);
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

    private void OnScenarioSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressScenarioSelection)
        {
            return;
        }

        if (ScenarioSelector.SelectedItem is ScenarioDescriptor descriptor)
        {
            SwitchScenario(descriptor.Scenario);
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

    private ChartEngine.ChartEngine CreateChartEngine()
    {
        var engine = new ChartEngine.ChartEngine(new ChartEngineOptions
        {
            VisibleDuration = TimeSpan.FromMinutes(3),
            FrameBudget = TimeSpan.FromMilliseconds(8),
            StrokeWidth = 2.0,
            ShowAxes = true,
            Palette = ToChartColors(_activeTheme.Palette.Series),
        });

        engine.ConfigureSeries(BuildSeriesDefinitions());
        ApplyDefaultOverrides(engine);

        return engine;
    }

    private ChartSeriesDefinition[] BuildSeriesDefinitions()
    {
        var definitions = new List<ChartSeriesDefinition>
        {
            new LineSeriesDefinition(PriceSeriesId)
            {
                StrokeWidth = 2.0,
                FillOpacity = 0.18,
            },
            new AreaSeriesDefinition(LatencySeriesId)
            {
                Baseline = 0,
                FillOpacity = 0.45,
            },
            new ScatterSeriesDefinition(ScatterSeriesId)
            {
                MarkerSize = 6.0,
            },
            new BarSeriesDefinition(DeltaSeriesId)
            {
                Baseline = 0,
                BarWidthSeconds = 8.0,
            },
            new BarSeriesDefinition(VolumeSeriesId)
            {
                Baseline = 0,
                BarWidthSeconds = VolumeBucketSeconds,
                FillOpacity = 0.55,
            },
        };

        for (var i = 0; i < HeatmapBucketCount; i++)
        {
            definitions.Add(new HeatmapSeriesDefinition((uint)(HeatmapSeriesBaseId + i))
            {
                FillOpacity = 0.85,
                BucketIndex = (uint)i,
                BucketCount = HeatmapBucketCount,
            });
        }

        return definitions.ToArray();
    }

    private void ApplyDefaultOverrides(ChartEngine.ChartEngine engine)
    {
        var overrides = new List<ChartSeriesOverride>
        {
            new ChartSeriesOverride(LatencySeriesId)
                .WithLabel("Latency (ms)")
                .WithColor(new ChartColor(0x3A, 0xB8, 0xFF, 0xFF)),
            new ChartSeriesOverride(ScatterSeriesId)
                .WithLabel("Price spikes")
                .WithColor(new ChartColor(0xFF, 0x9E, 0x7B, 0xFF)),
            new ChartSeriesOverride(DeltaSeriesId)
                .WithLabel("Δ price (scaled)")
                .WithColor(new ChartColor(0x47, 0xE2, 0xC2, 0xFF)),
            new ChartSeriesOverride(VolumeSeriesId)
                .WithLabel($"Volume ({VolumeBucketSeconds:0}s)")
                .WithColor(new ChartColor(0x2F, 0xCE, 0x8F, 0xFF)),
        };

        for (var i = 0; i < HeatmapBucketCount; i++)
        {
            overrides.Add(new ChartSeriesOverride(HeatmapSeriesBaseId + i)
                .WithLabel(DescribeHeatmapBucket(i))
                .WithColor(GetHeatmapColor(i)));
        }

        engine.ApplySeriesOverrides(overrides.ToArray());
    }

    private void SwitchScenario(ChartScenario scenario)
    {
        if (_activeScenario == scenario)
        {
            return;
        }

        var previousEngine = _chartEngine;
        _activeScenario = scenario;
        ResetScenarioState();

        _chartEngine = CreateChartEngine();
        _chartEngine.SetTelemetrySink(_telemetrySink);
        ChartHost.ChartEngine = _chartEngine;
        ApplyTheme(_activeTheme, updateSelector: false);
        ApplySeriesStyle();
        ApplyScenario(_activeScenario, resetAggregators: false);

        previousEngine.Dispose();
    }

    private void ApplyScenario(ChartScenario scenario, bool resetAggregators)
    {
        if (resetAggregators)
        {
            ResetScenarioState();
        }

        ChartHost.Composition = BuildComposition(scenario);
        UpdateRecentWindow(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0);
        SetScenarioStatusDefaults();
        ChartHost.RequestRender();

        if (scenario != ChartScenario.PriceVolume)
        {
            _chartEngine.ApplySeriesOverrides(new[]
            {
                new ChartSeriesOverride(VolumeSeriesId).ClearColor(),
            });
        }
    }

    private void ResetScenarioState()
    {
        _lastPrice = null;
        _lastVolumeBucket = 0;
        _lastHeatmapBucketIndex = -1;
        _lastHeatmapIntensity = 0;
        _volumeAggregator.Reset();
        _heatmapAggregator.Reset();
        ChartHost.Annotations = Array.Empty<ChartAnnotation>();
    }

    private void SetScenarioStatusDefaults()
    {
        switch (_activeScenario)
        {
            case ChartScenario.PriceVolume:
                LatencyText.Text = FormatVolumeStatus();
                break;
            case ChartScenario.RollingHeatmap:
                LatencyText.Text = FormatHeatmapStatus();
                break;
            default:
                LatencyText.Text = "Latency: --";
                break;
        }
    }

    private ChartComposition BuildComposition(ChartScenario scenario)
    {
        CompositionAnnotationLayer? priceWindowLayer = null;

        var composition = ChartComposition.Create(builder =>
        {
            switch (scenario)
            {
                case ChartScenario.LiveTrades:
                    builder.Pane("price")
                        .WithSeries(PriceSeriesId, ScatterSeriesId, DeltaSeriesId)
                        .WithHeightRatio(3.0)
                        .ShareXAxisWithPrimary()
                        .Done();

                    builder.Pane("latency")
                        .WithSeries(LatencySeriesId)
                        .WithHeightRatio(1.0)
                        .ShareXAxisWithPrimary()
                        .Done();

                    builder.AnnotationLayer("latency-target", AnnotationZOrder.BelowSeries, layer =>
                    {
                        layer.ForPanes("latency");
                        layer.Annotations.Add(new ValueZoneAnnotation(0, 150, "Latency target")
                        {
                            Fill = new ChartColor(0x2B, 0x8A, 0x3A, 0x32),
                            Border = new ChartColor(0x2B, 0x8A, 0x3A, 0x96),
                            TargetPaneId = "latency",
                        });
                    });
                    break;

                case ChartScenario.PriceVolume:
                    builder.Pane("price")
                        .WithSeries(PriceSeriesId)
                        .WithHeightRatio(3.0)
                        .ShareXAxisWithPrimary()
                        .Done();

                    builder.Pane("volume")
                        .WithSeries(VolumeSeriesId)
                        .WithHeightRatio(1.4)
                        .ShareXAxisWithPrimary()
                        .Done();
                    break;

                case ChartScenario.RollingHeatmap:
                    builder.Pane("price")
                        .WithSeries(PriceSeriesId)
                        .WithHeightRatio(2.0)
                        .ShareXAxisWithPrimary()
                        .Done();

                    var heatmapSeries = Enumerable.Range(0, HeatmapBucketCount)
                        .Select(i => (uint)(HeatmapSeriesBaseId + i))
                        .ToArray();
                    builder.Pane("heatmap")
                        .WithSeries(heatmapSeries)
                        .WithHeightRatio(2.0)
                        .ShareXAxisWithPrimary()
                        .Done();
                    break;
            }

            builder.AnnotationLayer("recent-activity", AnnotationZOrder.BelowSeries, layer =>
            {
                layer.ForPanes("price");
                priceWindowLayer = layer;
            });
        });

        _priceWindowLayer = priceWindowLayer;
        return composition;
    }

    private string FormatVolumeStatus() =>
        _lastVolumeBucket > 0
            ? $"Volume ({VolumeBucketSeconds:0}s): {_lastVolumeBucket:0.###} BTC"
            : $"Volume ({VolumeBucketSeconds:0}s): --";

    private string FormatHeatmapStatus()
    {
        if (_lastHeatmapBucketIndex < 0)
        {
            return "Δ bucket: --";
        }

        return $"Δ bucket: {DescribeHeatmapBucket(_lastHeatmapBucketIndex)} | weight {_lastHeatmapIntensity:0.###} BTC";
    }

    private static string DescribeHeatmapBucket(int bucketIndex) => bucketIndex switch
    {
        0 => "< -0.35%",
        1 => "-0.35% .. -0.15%",
        2 => "±0.15%",
        3 => "0.15% .. 0.35%",
        4 => "> 0.35%",
        _ => "--",
    };

    private static ChartColor GetHeatmapColor(int bucketIndex) => bucketIndex switch
    {
        0 => new ChartColor(0xF4, 0x5E, 0x8C, 0xFF),
        1 => new ChartColor(0xFB, 0x9B, 0x5F, 0xFF),
        2 => new ChartColor(0xF4, 0xE2, 0x64, 0xFF),
        3 => new ChartColor(0x62, 0xD8, 0xA7, 0xFF),
        4 => new ChartColor(0x3A, 0xB8, 0xFF, 0xFF),
        _ => new ChartColor(0x99, 0xA2, 0xC6, 0xFF),
    };

    private void UpdateOverlayArtifacts(double timestampSeconds, double price)
    {
        UpdateRecentWindow(timestampSeconds);
        UpdatePriceAnnotations(timestampSeconds, price);
        ChartHost.RequestRender();
    }

    private void UpdateRecentWindow(double timestampSeconds)
    {
        if (_priceWindowLayer is null)
        {
            return;
        }

        var windowStart = Math.Max(timestampSeconds - RecentWindowSeconds, 0d);

        _priceWindowLayer.Annotations.Clear();
        _priceWindowLayer.Annotations.Add(new TimeRangeAnnotation(windowStart, timestampSeconds, "Last 60s")
        {
            Fill = new ChartColor(0x3A, 0xB8, 0xFF, 0x28),
            Border = new ChartColor(0x3A, 0xB8, 0xFF, 0x80),
            TargetPaneId = "price",
        });
    }

    private void UpdatePriceAnnotations(double timestampSeconds, double price)
    {
        ChartHost.Annotations = BuildAnnotations(timestampSeconds, price);
    }

    private IReadOnlyList<ChartAnnotation> BuildAnnotations(double timestampSeconds, double price)
    {
        var accent = new ChartColor(0x3A, 0xB8, 0xFF, 0xFF);
        var annotations = new List<ChartAnnotation>
        {
            new HorizontalLineAnnotation(price, "Last trade")
            {
                Color = accent,
                Thickness = 1.5,
                TargetPaneId = "price",
            },
            new CalloutAnnotation(timestampSeconds, price, $"{price:0.00}")
            {
                Color = accent,
                Background = new ChartColor(0x10, 0x15, 0x1F, 0xE6),
                Border = accent,
                TextColor = new ChartColor(0xEC, 0xEF, 0xF4, 0xFF),
                TargetPaneId = "price",
                PointerLength = 12.0,
            },
        };

        switch (_activeScenario)
        {
            case ChartScenario.PriceVolume when _lastVolumeBucket > 0:
            {
                var normalized = Math.Min(_lastVolumeBucket, VolumeGradientMax);
                var volumeColor = _volumeGradient.Evaluate(normalized);
                annotations.Add(new GradientZoneAnnotation(0.0, _lastVolumeBucket, ChartColor.FromRgb(0x1F, 0x40, 0x60), volumeColor)
                {
                    FillOpacity = 0.28,
                    BorderThickness = 1.0,
                    TargetPaneId = "volume",
                });
                break;
            }

            case ChartScenario.RollingHeatmap when _lastHeatmapBucketIndex >= 0:
            {
                var (min, max) = GetHeatmapBucketRange(_lastHeatmapBucketIndex);
                annotations.Add(new GradientZoneAnnotation(min, max, ChartColor.FromRgb(0x2F, 0x95, 0xFF), ChartColor.FromRgb(0xF9, 0x65, 0x7D))
                {
                    FillOpacity = 0.2,
                    BorderThickness = 0.0,
                    TargetPaneId = "heatmap",
                });
                break;
            }
        }

        return annotations;
    }

    private (double Min, double Max) GetHeatmapBucketRange(int bucketIndex) => bucketIndex switch
    {
        0 => (-1.0, -0.35),
        1 => (-0.35, -0.15),
        2 => (-0.15, 0.15),
        3 => (0.15, 0.35),
        4 => (0.35, 1.0),
        _ => (-0.15, 0.15),
    };

    private void ApplyVolumeStyling()
    {
        if (_activeScenario != ChartScenario.PriceVolume || _lastVolumeBucket <= 0)
        {
            return;
        }

        var normalized = Math.Min(_lastVolumeBucket, VolumeGradientMax);
        var color = _volumeGradient.Evaluate(normalized);
        var overrides = new[]
        {
            new ChartSeriesOverride(VolumeSeriesId).WithColor(color),
        };

        _chartEngine.ApplySeriesOverrides(overrides);
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


