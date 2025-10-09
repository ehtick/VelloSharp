using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VelloSharp.Composition.Accessibility;
using VelloSharp.Composition.Controls;
using VelloSharp.Composition.Input;

namespace VelloSharp.Integration.Avalonia;

public sealed class AvaloniaCompositionInputSource : ICompositionInputSource
{
    private readonly Control _target;
    private readonly Dictionary<ulong, PointerState> _activePointers = new();
    private ICompositionInputSink? _sink;
    private InputControl? _inputControl;
    private bool _disposed;

    public AvaloniaCompositionInputSource(Control target)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _target.Focusable = true;
    }

    public void Connect(ICompositionInputSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        if (!ReferenceEquals(_sink, null))
        {
            throw new InvalidOperationException("An input sink is already connected.");
        }

        _sink = sink;
        _inputControl = sink as InputControl;
        if (_inputControl is not null)
        {
            _inputControl.AccessibilityChanged += OnAccessibilityChanged;
            _inputControl.AccessibilityAnnouncementRequested += OnAccessibilityAnnouncementRequested;
            ApplyAccessibilityProperties();
        }

        _target.PointerEntered += OnPointerEntered;
        _target.PointerMoved += OnPointerMoved;
        _target.PointerExited += OnPointerExited;
        _target.PointerPressed += OnPointerPressed;
        _target.PointerReleased += OnPointerReleased;
        _target.PointerWheelChanged += OnPointerWheelChanged;
        _target.PointerCaptureLost += OnPointerCaptureLost;
        _target.KeyDown += OnKeyDown;
        _target.KeyUp += OnKeyUp;
        _target.TextInput += OnTextInput;
        _target.GotFocus += OnGotFocus;
        _target.LostFocus += OnLostFocus;
    }

    public void Disconnect(ICompositionInputSink sink)
    {
        if (!ReferenceEquals(_sink, sink))
        {
            return;
        }

        if (_inputControl is not null)
        {
            _inputControl.AccessibilityChanged -= OnAccessibilityChanged;
            _inputControl.AccessibilityAnnouncementRequested -= OnAccessibilityAnnouncementRequested;
            _inputControl = null;
        }

        _target.PointerEntered -= OnPointerEntered;
        _target.PointerMoved -= OnPointerMoved;
        _target.PointerExited -= OnPointerExited;
        _target.PointerPressed -= OnPointerPressed;
        _target.PointerReleased -= OnPointerReleased;
        _target.PointerWheelChanged -= OnPointerWheelChanged;
        _target.PointerCaptureLost -= OnPointerCaptureLost;
        _target.KeyDown -= OnKeyDown;
        _target.KeyUp -= OnKeyUp;
        _target.TextInput -= OnTextInput;
        _target.GotFocus -= OnGotFocus;
        _target.LostFocus -= OnLostFocus;

        _activePointers.Clear();
        _sink = null;
    }

    public void RequestPointerCapture(ICompositionInputSink sink, ulong pointerId)
    {
        if (!ReferenceEquals(_sink, sink))
        {
            return;
        }

        if (_activePointers.TryGetValue(pointerId, out var state))
        {
            state.Pointer.Capture(_target);
        }
    }

    public void ReleasePointerCapture(ICompositionInputSink sink, ulong pointerId)
    {
        if (!ReferenceEquals(_sink, sink))
        {
            return;
        }

        if (_activePointers.TryGetValue(pointerId, out var state))
        {
            state.Pointer.Capture(null);
        }
    }

    public void RequestFocus(ICompositionInputSink sink)
    {
        if (!ReferenceEquals(_sink, sink))
        {
            return;
        }

        _target.Focus();
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e) =>
        ForwardPointerEvent(e, PointerEventType.Enter, PointerButton.None, e.GetCurrentPoint(_target).Properties);

    private void OnPointerMoved(object? sender, PointerEventArgs e) =>
        ForwardPointerEvent(e, PointerEventType.Move, PointerButton.None, e.GetCurrentPoint(_target).Properties);

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        ForwardPointerEvent(e, PointerEventType.Leave, PointerButton.None, e.GetCurrentPoint(_target).Properties);
        _activePointers.Remove(GetPointerId(e.Pointer));
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _target.Focus();
        var point = e.GetCurrentPoint(_target);
        var properties = point.Properties;
        var button = MapButton(properties.PointerUpdateKind);
        ForwardPointerEvent(e, PointerEventType.Down, button, properties);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var properties = e.GetCurrentPoint(_target).Properties;
        var button = MapButton(properties.PointerUpdateKind);
        ForwardPointerEvent(e, PointerEventType.Up, button, properties);
        _activePointers.Remove(GetPointerId(e.Pointer));
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e) =>
        ForwardPointerEvent(e, PointerEventType.Wheel, PointerButton.None, e.GetCurrentPoint(_target).Properties, e.Delta);

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        var pointerId = GetPointerId(e.Pointer);
        if (!_activePointers.TryGetValue(pointerId, out var state))
        {
            return;
        }

        _activePointers.Remove(pointerId);

        if (_sink is null)
        {
            return;
        }

        var args = new CompositionPointerEventArgs(
            PointerEventType.CaptureLost,
            MapDeviceKind(e.Pointer.Type),
            ToModifiers(state.LastKeyModifiers, state.LastProperties),
            PointerButton.None,
            pointerId,
            state.LastPosition.X,
            state.LastPosition.Y,
            0,
            0,
            0,
            0,
            TimeSpan.FromMilliseconds(Environment.TickCount64 & int.MaxValue));

        _sink.ProcessPointerEvent(args);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var args = new CompositionKeyEventArgs(KeyEventType.Down, (int)e.Key, ToModifiers(e.KeyModifiers, null), isRepeat: false, e.KeySymbol);
        _sink?.ProcessKeyEvent(args);
        if (args.Handled)
        {
            e.Handled = true;
        }
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        var args = new CompositionKeyEventArgs(KeyEventType.Up, (int)e.Key, ToModifiers(e.KeyModifiers, null), isRepeat: false, e.KeySymbol);
        _sink?.ProcessKeyEvent(args);
        if (args.Handled)
        {
            e.Handled = true;
        }
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (_sink is null)
        {
            return;
        }

        var args = new CompositionTextInputEventArgs(e.Text ?? string.Empty);
        _sink.ProcessTextInput(args);
        if (args.Handled)
        {
            e.Handled = true;
        }
    }

    private void OnGotFocus(object? sender, GotFocusEventArgs e) =>
        _sink?.ProcessFocusChanged(true);

    private void OnLostFocus(object? sender, RoutedEventArgs e) =>
        _sink?.ProcessFocusChanged(false);

    private void OnAccessibilityChanged(object? sender, AccessibilityChangedEventArgs e) =>
        ApplyAccessibilityProperties();

    private void OnAccessibilityAnnouncementRequested(object? sender, AccessibilityAnnouncementEventArgs e)
    {
        // Avalonia notification routing will be implemented once automation peers are in place.
    }

    private void ApplyAccessibilityProperties()
    {
        if (_inputControl is null)
        {
            AutomationProperties.SetName(_target, null);
            AutomationProperties.SetHelpText(_target, null);
            AutomationProperties.SetControlTypeOverride(_target, null);
            AutomationProperties.SetAutomationId(_target, null);
            AutomationProperties.SetAccessibilityView(_target, AccessibilityView.Raw);
            AutomationProperties.SetLiveSetting(_target, AutomationLiveSetting.Off);
            return;
        }

        var props = _inputControl.Accessibility;
        if (!props.IsAccessible)
        {
            AutomationProperties.SetAccessibilityView(_target, AccessibilityView.Raw);
            AutomationProperties.SetName(_target, null);
            AutomationProperties.SetHelpText(_target, null);
            AutomationProperties.SetControlTypeOverride(_target, null);
            AutomationProperties.SetAutomationId(_target, props.AutomationId);
            AutomationProperties.SetLiveSetting(_target, AutomationLiveSetting.Off);
            return;
        }

        AutomationProperties.SetAccessibilityView(_target, AccessibilityView.Content);
        AutomationProperties.SetName(_target, props.Name);
        AutomationProperties.SetHelpText(_target, props.HelpText);
        AutomationProperties.SetAutomationId(_target, props.AutomationId);
        AutomationProperties.SetControlTypeOverride(_target, MapAutomationControlType(props.Role));
        AutomationProperties.SetLiveSetting(_target, MapLiveSetting(props.LiveSetting));
    }

    private static AutomationControlType? MapAutomationControlType(AccessibilityRole role) =>
        role switch
        {
            AccessibilityRole.Button => AutomationControlType.Button,
            AccessibilityRole.ToggleButton => AutomationControlType.Button,
            AccessibilityRole.CheckBox => AutomationControlType.CheckBox,
            AccessibilityRole.Slider => AutomationControlType.Slider,
            AccessibilityRole.TabItem => AutomationControlType.TabItem,
            AccessibilityRole.Text => AutomationControlType.Text,
            AccessibilityRole.ListItem => AutomationControlType.ListItem,
            _ => null,
        };

    private static AutomationLiveSetting MapLiveSetting(AccessibilityLiveSetting setting) =>
        setting switch
        {
            AccessibilityLiveSetting.Off => AutomationLiveSetting.Off,
            AccessibilityLiveSetting.Assertive => AutomationLiveSetting.Assertive,
            _ => AutomationLiveSetting.Polite,
        };

    private void ForwardPointerEvent(PointerEventArgs e, PointerEventType eventType, PointerButton button, PointerPointProperties? properties, Vector? wheelDelta = null)
    {
        if (_sink is null)
        {
            return;
        }

        var pointerId = GetPointerId(e.Pointer);
        var position = e.GetPosition(_target);
        var previousPosition = position;
        var effectiveProperties = properties;

        if (_activePointers.TryGetValue(pointerId, out var state))
        {
            previousPosition = state.LastPosition;
            if (effectiveProperties is null)
            {
                effectiveProperties = state.LastProperties;
            }

            state.LastPosition = position;
            state.LastProperties = effectiveProperties;
            state.LastKeyModifiers = e.KeyModifiers;
        }
        else
        {
            _activePointers[pointerId] = new PointerState(e.Pointer, position, effectiveProperties, e.KeyModifiers);
        }

        var delta = position - previousPosition;
        var args = new CompositionPointerEventArgs(
            eventType,
            MapDeviceKind(e.Pointer.Type),
            ToModifiers(e.KeyModifiers, effectiveProperties),
            button,
            pointerId,
            position.X,
            position.Y,
            delta.X,
            delta.Y,
            wheelDelta?.X ?? 0,
            wheelDelta?.Y ?? 0,
            TimeSpan.FromMilliseconds(e.Timestamp));

        _sink.ProcessPointerEvent(args);
        if (args.Handled)
        {
            e.Handled = true;
        }
    }

    private static ulong GetPointerId(IPointer pointer) =>
        unchecked((ulong)(uint)pointer.Id);

    private static PointerButton MapButton(PointerUpdateKind updateKind) =>
        updateKind switch
        {
            PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.LeftButtonReleased => PointerButton.Primary,
            PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased => PointerButton.Secondary,
            PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.MiddleButtonReleased => PointerButton.Middle,
            PointerUpdateKind.XButton1Pressed or PointerUpdateKind.XButton1Released => PointerButton.XButton1,
            PointerUpdateKind.XButton2Pressed or PointerUpdateKind.XButton2Released => PointerButton.XButton2,
            _ => PointerButton.None,
        };

    private static PointerDeviceKind MapDeviceKind(PointerType type) =>
        type switch
        {
            PointerType.Mouse => PointerDeviceKind.Mouse,
            PointerType.Touch => PointerDeviceKind.Touch,
            PointerType.Pen => PointerDeviceKind.Pen,
            _ => PointerDeviceKind.Unknown,
        };

    private static InputModifiers ToModifiers(KeyModifiers keyModifiers, PointerPointProperties? pointerProperties)
    {
        var modifiers = InputModifiers.None;

        if ((keyModifiers & KeyModifiers.Shift) != 0)
        {
            modifiers |= InputModifiers.Shift;
        }
        if ((keyModifiers & KeyModifiers.Control) != 0)
        {
            modifiers |= InputModifiers.Control;
        }
        if ((keyModifiers & KeyModifiers.Alt) != 0)
        {
            modifiers |= InputModifiers.Alt;
        }
        if ((keyModifiers & KeyModifiers.Meta) != 0)
        {
            modifiers |= InputModifiers.Meta;
        }

        if (pointerProperties is not { } props)
        {
            return modifiers;
        }

        if (props.IsLeftButtonPressed)
        {
            modifiers |= InputModifiers.PrimaryButton;
        }
        if (props.IsRightButtonPressed)
        {
            modifiers |= InputModifiers.SecondaryButton;
        }
        if (props.IsMiddleButtonPressed)
        {
            modifiers |= InputModifiers.MiddleButton;
        }
        if (props.IsXButton1Pressed)
        {
            modifiers |= InputModifiers.XButton1;
        }
        if (props.IsXButton2Pressed)
        {
            modifiers |= InputModifiers.XButton2;
        }

        return modifiers;
    }

    private sealed class PointerState
    {
        public PointerState(IPointer pointer, Point lastPosition, PointerPointProperties? lastProperties, KeyModifiers lastKeyModifiers)
        {
            Pointer = pointer;
            LastPosition = lastPosition;
            LastProperties = lastProperties;
            LastKeyModifiers = lastKeyModifiers;
        }

        public IPointer Pointer { get; }
        public Point LastPosition { get; set; }
        public PointerPointProperties? LastProperties { get; set; }
        public KeyModifiers LastKeyModifiers { get; set; }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_sink is not null)
        {
            Disconnect(_sink);
        }

        GC.SuppressFinalize(this);
    }
}
