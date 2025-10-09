using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using VelloSharp;
using VelloSharp.ChartData;
using VelloSharp.ChartEngine;
using VelloSharp.ChartRuntime;
using VelloSharp.ChartRuntime.Windows.Wpf;
using VelloSharp.Wpf.Integration;
using VelloSharp.Windows;
using VelloSharp.ChartEngine.Annotations;
using VelloSharp.Charting.Legend;
using VelloSharp.Charting.Rendering;
using VelloSharp.Charting.Styling;
using VelloSharp.Composition.Accessibility;
using VelloSharp.Composition.Controls;

namespace VelloSharp.Charting.Wpf;

/// <summary>
/// WPF control hosting the Vello chart engine.
/// </summary>
public sealed class ChartView : System.Windows.Controls.ContentControl, IDisposable
{
    private ChartEngine.ChartEngine _engine;
    private bool _ownsEngine;
    private readonly VelloView _surfaceView;
    private IFrameTickSource? _tickSource;
    private bool _isLoaded;
    private bool _renderLoopActive;
    private bool _isDisposed;
    private readonly ChartOverlayRenderer _overlayRenderer = new();
    private ChartTheme _theme = ChartTheme.Default;
    private LegendDefinition? _legend;
    private IReadOnlyList<ChartAnnotation>? _annotations;
    private ChartComposition? _composition;
    private readonly InputControl _inputControl = new();
    private readonly WpfCompositionInputSource _inputSource;
    private string? _lastAccessibilityName;
    private string? _lastAccessibilityHelpText;

    public ChartView()
    {
        _surfaceView = new VelloView
        {
            RenderMode = VelloRenderMode.OnDemand,
            RenderLoopDriver = RenderLoopDriver.None,
        };

        Content = _surfaceView;

        _engine = new ChartEngine.ChartEngine(new ChartEngineOptions());
        _ownsEngine = true;
        _inputSource = new WpfCompositionInputSource(_surfaceView);
        _inputControl.AccessibilityChanged += OnInputAccessibilityChanged;
        _inputControl.AccessibilityAnnouncementRequested += OnAccessibilityAnnouncementRequested;
        _lastAccessibilityName = _inputControl.Accessibility.Name;
        _lastAccessibilityHelpText = _inputControl.Accessibility.HelpText;

        _surfaceView.RenderSurface += OnRenderSurface;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public bool OwnsEngine
    {
        get => _ownsEngine;
        set => _ownsEngine = value;
    }

    public ChartEngine.ChartEngine ChartEngine
    {
        get => _engine;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(_engine, value))
            {
                return;
            }

            DetachTickSource();

            if (_ownsEngine)
            {
                _engine.Dispose();
            }

            _engine = value;
            _ownsEngine = false;
            try
            {
                _engine.ConfigureComposition(_composition);
            }
            catch (ObjectDisposedException)
            {
            }
            EnsureTickSource();
            BeginSchedulerLoop();
            _surfaceView.RequestRender();
        }
    }

    public VelloGraphicsDeviceOptions DeviceOptions
    {
        get => _surfaceView.DeviceOptions;
        set => _surfaceView.DeviceOptions = value;
    }

    public VelloRenderMode RenderMode
    {
        get => _surfaceView.RenderMode;
        set => _surfaceView.RenderMode = value;
    }

    public VelloRenderBackend PreferredBackend
    {
        get => _surfaceView.PreferredBackend;
        set => _surfaceView.PreferredBackend = value;
    }

    public ChartTheme Theme
    {
        get => _theme;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (ReferenceEquals(_theme, value))
            {
                return;
            }

            _theme = value;
            if (_isLoaded)
            {
                _surfaceView.RequestRender();
            }
        }
    }

    public InputControl Input => _inputControl;

    public LegendDefinition? Legend
    {
        get => _legend;
        set
        {
            if (ReferenceEquals(_legend, value))
            {
                return;
            }

            _legend = value;
            if (_isLoaded)
            {
                RequestRender();
            }
        }
    }

    public ChartComposition? Composition
    {
        get => _composition;
        set
        {
            if (ReferenceEquals(_composition, value))
            {
                return;
            }

            _composition = value;
            try
            {
                _engine.ConfigureComposition(_composition);
            }
            catch (ObjectDisposedException)
            {
            }

            if (_isLoaded)
            {
                RequestRender();
            }
        }
    }

    public IReadOnlyList<ChartAnnotation>? Annotations
    {
        get => _annotations;
        set
        {
            if (ReferenceEquals(_annotations, value))
            {
                return;
            }

            _annotations = value;
            if (_isLoaded)
            {
                RequestRender();
            }
        }
    }

    public void PublishSamples(ReadOnlySpan<ChartSamplePoint> samples)
    {
        if (!_isLoaded || samples.IsEmpty)
        {
            return;
        }

        try
        {
            _engine.PumpData(samples);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        RequestRender();
    }

    public void RequestRender()
        => _surfaceView.RequestRender();

    protected override AutomationPeer OnCreateAutomationPeer()
        => new ChartViewAutomationPeer(this);

    private FrameworkElementAutomationPeer? GetOrCreateAutomationPeer()
        => UIElementAutomationPeer.FromElement(this) as FrameworkElementAutomationPeer
           ?? UIElementAutomationPeer.CreatePeerForElement(this) as FrameworkElementAutomationPeer;

    private void OnInputAccessibilityChanged(object? sender, AccessibilityChangedEventArgs e)
    {
        if (!AutomationPeer.ListenerExists(AutomationEvents.PropertyChanged))
        {
            return;
        }

        if (GetOrCreateAutomationPeer() is not FrameworkElementAutomationPeer peer)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(AccessibilityProperties.Name):
                var previousName = _lastAccessibilityName;
                var currentName = _inputControl.Accessibility.Name;
                if (!Equals(previousName, currentName))
                {
                    peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, previousName, currentName);
                    _lastAccessibilityName = currentName;
                }
                break;
            case nameof(AccessibilityProperties.HelpText):
                var previousHelp = _lastAccessibilityHelpText;
                var currentHelp = _inputControl.Accessibility.HelpText;
                if (!Equals(previousHelp, currentHelp))
                {
                    peer.RaisePropertyChangedEvent(AutomationElementIdentifiers.HelpTextProperty, previousHelp, currentHelp);
                    _lastAccessibilityHelpText = currentHelp;
                }
                break;
            default:
                peer.InvalidatePeer();
                return;
        }

        peer.InvalidatePeer();
    }

    private void OnAccessibilityAnnouncementRequested(object? sender, AccessibilityAnnouncementEventArgs e)
    {
        if (!AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
        {
            return;
        }

        if (GetOrCreateAutomationPeer() is not FrameworkElementAutomationPeer peer)
        {
            return;
        }

        var notificationKind = e.LiveSetting == AccessibilityLiveSetting.Assertive
            ? AutomationNotificationKind.ActionCompleted
            : AutomationNotificationKind.Other;

        peer.RaiseNotificationEvent(notificationKind, AutomationNotificationProcessing.MostRecent, e.Message, "chart-view");
    }

    private sealed class ChartViewAutomationPeer : FrameworkElementAutomationPeer, IInvokeProvider
    {
        private readonly ChartView _owner;

        public ChartViewAutomationPeer(ChartView owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override bool IsControlElementCore()
            => _owner._inputControl.Accessibility.IsAccessible;

        protected override string GetNameCore()
        {
            var name = _owner._inputControl.Accessibility.Name;
            return string.IsNullOrEmpty(name) ? base.GetNameCore() : name!;
        }

        protected override string GetHelpTextCore()
        {
            var help = _owner._inputControl.Accessibility.HelpText;
            return string.IsNullOrEmpty(help) ? base.GetHelpTextCore() : help!;
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
            => MapRole(_owner._inputControl.Accessibility.Role);

        public override object? GetPattern(PatternInterface patternInterface)
            => patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);

        public void Invoke()
        {
            if (!_owner._inputControl.Accessibility.IsAccessible)
            {
                return;
            }

            _owner._inputControl.HandleAccessibilityAction(AccessibilityAction.Invoke);
        }

        private static AutomationControlType MapRole(AccessibilityRole role) => role switch
        {
            AccessibilityRole.Button or AccessibilityRole.ToggleButton => AutomationControlType.Button,
            AccessibilityRole.CheckBox => AutomationControlType.CheckBox,
            AccessibilityRole.Slider => AutomationControlType.Slider,
            AccessibilityRole.TabItem => AutomationControlType.TabItem,
            AccessibilityRole.Text => AutomationControlType.Text,
            AccessibilityRole.ListItem => AutomationControlType.ListItem,
            _ => AutomationControlType.Custom,
        };
    }
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        EnsureTickSource();
        BeginSchedulerLoop();
        _inputControl.DetachInputSource();
        _inputControl.AttachInputSource(_inputSource);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _renderLoopActive = false;
        _isLoaded = false;
        DetachTickSource();
        _inputControl.DetachInputSource();

        if (_ownsEngine)
        {
            _engine.Dispose();
        }
    }

    private void BeginSchedulerLoop()
    {
        if (_renderLoopActive || !_isLoaded)
        {
            return;
        }

        EnsureTickSource();
        _renderLoopActive = true;
        _engine.ScheduleRender(OnScheduledTick);
    }

    private void OnScheduledTick(FrameTick tick)
    {
        if (!_renderLoopActive || !_isLoaded)
        {
            return;
        }

        void Request()
        {
            if (_renderLoopActive && _isLoaded)
            {
                _surfaceView.RequestRender();
            }
        }

        if (Dispatcher.CheckAccess())
        {
            Request();
        }
        else
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(Request));
        }

        try
        {
            _engine.ScheduleRender(OnScheduledTick);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void OnRenderSurface(object? sender, VelloSurfaceRenderEventArgs e)
    {
        if (!_isLoaded || e.PixelSize.Width == 0 || e.PixelSize.Height == 0)
        {
            return;
        }

        try
        {
            _engine.Render(e.Scene, e.PixelSize.Width, e.PixelSize.Height);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        RenderOverlay(e.Scene, e.PixelSize.Width, e.PixelSize.Height, 1.0);

        if (!e.Handled)
        {
            e.RenderScene(e.Scene);
        }
    }

    private void EnsureTickSource()
    {
        if (_isDisposed)
        {
            return;
        }

        if (_tickSource is null)
        {
            _tickSource = new WpfCompositionTargetTickSource(Dispatcher);
        }

        _engine.ConfigureTickSource(_tickSource);
    }

    private void DetachTickSource()
    {
        if (_tickSource is null)
        {
            return;
        }

        try
        {
            _engine.ConfigureTickSource(null);
        }
        catch (ObjectDisposedException)
        {
        }

        _tickSource.Dispose();
        _tickSource = null;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _renderLoopActive = false;

        DetachTickSource();
        _surfaceView.RenderSurface -= OnRenderSurface;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        _inputControl.AccessibilityChanged -= OnInputAccessibilityChanged;
        _inputControl.AccessibilityAnnouncementRequested -= OnAccessibilityAnnouncementRequested;
        _inputControl.DetachInputSource();
        _inputSource.Dispose();

        if (_ownsEngine)
        {
            _engine.Dispose();
        }

        _isLoaded = false;
        _overlayRenderer.Dispose();
    }

    private void RenderOverlay(Scene scene, double width, double height, double devicePixelRatio)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var hasCompositionAnnotations = _composition?.AnnotationLayers.Count > 0;
        var hasInlineAnnotations = _annotations is { Count: > 0 };
        if (!_engine.Options.ShowAxes && _legend is null && !hasInlineAnnotations && !hasCompositionAnnotations)
        {
            return;
        }

        ChartFrameMetadata metadata;
        try
        {
            metadata = _engine.GetFrameMetadata();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        _overlayRenderer.Render(
            scene,
            metadata,
            width,
            height,
            devicePixelRatio,
            _theme,
            _legend,
            _composition,
            _annotations,
            _engine.Options.ShowAxes);
    }
}


