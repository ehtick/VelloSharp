using System;
using System.Numerics;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using VelloSharp.Scenes;
using VelloSharp;
using VelloSharp.Integration.Avalonia;

namespace AvaloniaVelloExamples.Views;

public class ExamplesView : VelloSurfaceView
{
    public static readonly StyledProperty<ManagedSceneHost?> HostProperty =
        AvaloniaProperty.Register<ExamplesView, ManagedSceneHost?>(nameof(Host));

    public static readonly StyledProperty<int> SceneIndexProperty =
        AvaloniaProperty.Register<ExamplesView, int>(nameof(SceneIndex), -1);

    public static readonly StyledProperty<int> ComplexityProperty =
        AvaloniaProperty.Register<ExamplesView, int>(nameof(Complexity), 1);

    public static readonly StyledProperty<bool> IsInteractiveProperty =
        AvaloniaProperty.Register<ExamplesView, bool>(nameof(IsInteractive), true);

    private double _time;
    private Matrix3x2 _userTransform = Matrix3x2.Identity;
    private Point? _lastPointerPosition;
    private bool _isPointerCaptured;

    static ExamplesView()
    {
        AffectsRender<ExamplesView>(HostProperty, SceneIndexProperty, ComplexityProperty, IsInteractiveProperty);
    }

    public ExamplesView()
    {
        Cursor = new Cursor(StandardCursorType.Cross);
    }

    public ManagedSceneHost? Host
    {
        get => GetValue(HostProperty);
        set => SetValue(HostProperty, value);
    }

    public int SceneIndex
    {
        get => GetValue(SceneIndexProperty);
        set => SetValue(SceneIndexProperty, value);
    }

    public int Complexity
    {
        get => GetValue(ComplexityProperty);
        set => SetValue(ComplexityProperty, value);
    }

    public bool IsInteractive
    {
        get => GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    public void ResetView()
    {
        _userTransform = Matrix3x2.Identity;
        RequestRender();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == HostProperty ||
            property == SceneIndexProperty ||
            property == ComplexityProperty ||
            property == IsInteractiveProperty)
        {
            RequestRender();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _lastPointerPosition = e.GetPosition(this);
            _isPointerCaptured = true;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_isPointerCaptured)
        {
            e.Pointer.Capture(null);
            _isPointerCaptured = false;
        }
        _lastPointerPosition = null;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_isPointerCaptured || _lastPointerPosition is null)
        {
            return;
        }

        var current = e.GetPosition(this);
        var delta = current - _lastPointerPosition.Value;
        if (Math.Abs(delta.X) > double.Epsilon || Math.Abs(delta.Y) > double.Epsilon)
        {
            var translation = Matrix3x2.CreateTranslation((float)delta.X, (float)delta.Y);
            _userTransform = translation * _userTransform;
            _lastPointerPosition = current;
            RequestRender();
            e.Handled = true;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (!IsInteractive)
        {
            return;
        }

        var delta = e.Delta.Y;
        if (Math.Abs(delta) < double.Epsilon)
        {
            return;
        }

        const double BaseScale = 1.05;
        var scale = (float)Math.Pow(BaseScale, delta);
        var position = e.GetPosition(this);
        var translateToOrigin = Matrix3x2.CreateTranslation(-(float)position.X, -(float)position.Y);
        var translateBack = Matrix3x2.CreateTranslation((float)position.X, (float)position.Y);
        _userTransform = translateToOrigin * Matrix3x2.CreateScale(scale) * translateBack * _userTransform;
        RequestRender();
        e.Handled = true;
    }

    protected override void OnRenderFrame(VelloRenderFrameContext context)
    {
        base.OnRenderFrame(context);

        var host = Host;
        if (host is null || SceneIndex < 0 || SceneIndex >= host.Scenes.Count)
        {
            return;
        }

        var clampedComplexity = Math.Max(1, Complexity);
        _time += context.DeltaTime.TotalSeconds;

        var transform = _userTransform;
        var scene = context.Scene;
        var result = host.Render(SceneIndex, scene, _time, IsInteractive, clampedComplexity, transform);

        if (result.BaseColor is { } color)
        {
            RenderParameters = RenderParameters with { BaseColor = color };
        }
    }
}
