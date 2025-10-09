using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using VelloSharp.Composition.Accessibility;
using VelloSharp.Composition.Controls;
using VelloSharp.Composition.Input;

namespace VelloSharp.Wpf.Integration;

public sealed class WpfCompositionInputSource : ICompositionInputSource
{
    private const ulong MousePointerId = 1;

    private readonly UIElement _target;
    private readonly Dictionary<ulong, PointerState> _pointerStates = new();
    private ICompositionInputSink? _sink;
    private InputControl? _inputControl;
    private bool _disposed;

    public WpfCompositionInputSource(UIElement target)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _target.Focusable = true;
        _target.IsHitTestVisible = true;
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
            ApplyAccessibilityProperties();
        }

        _target.MouseEnter += OnMouseEnter;
        _target.MouseLeave += OnMouseLeave;
        _target.MouseMove += OnMouseMove;
        _target.PreviewMouseDown += OnMouseDown;
        _target.PreviewMouseUp += OnMouseUp;
        _target.PreviewMouseWheel += OnMouseWheel;

        _target.TouchEnter += OnTouchEnter;
        _target.TouchLeave += OnTouchLeave;
        _target.TouchDown += OnTouchDown;
        _target.TouchMove += OnTouchMove;
        _target.TouchUp += OnTouchUp;

        _target.StylusInAirMove += OnStylusMove;
        _target.StylusDown += OnStylusDown;
        _target.StylusMove += OnStylusMove;
        _target.StylusUp += OnStylusUp;
        _target.StylusOutOfRange += OnStylusOutOfRange;

        _target.LostMouseCapture += OnLostMouseCapture;
        _target.LostTouchCapture += OnLostTouchCapture;
        _target.LostStylusCapture += OnLostStylusCapture;

        _target.PreviewKeyDown += OnKeyDown;
        _target.PreviewKeyUp += OnKeyUp;
        _target.TextInput += OnTextInput;

        _target.GotKeyboardFocus += OnGotKeyboardFocus;
        _target.LostKeyboardFocus += OnLostKeyboardFocus;
    }

    public void Disconnect(ICompositionInputSink sink)
    {
        if (!ReferenceEquals(_sink, sink))
        {
            return;
        }

        _target.MouseEnter -= OnMouseEnter;
        _target.MouseLeave -= OnMouseLeave;
        _target.MouseMove -= OnMouseMove;
        _target.PreviewMouseDown -= OnMouseDown;
        _target.PreviewMouseUp -= OnMouseUp;
        _target.PreviewMouseWheel -= OnMouseWheel;

        _target.TouchEnter -= OnTouchEnter;
        _target.TouchLeave -= OnTouchLeave;
        _target.TouchDown -= OnTouchDown;
        _target.TouchMove -= OnTouchMove;
        _target.TouchUp -= OnTouchUp;

        _target.StylusInAirMove -= OnStylusMove;
        _target.StylusDown -= OnStylusDown;
        _target.StylusMove -= OnStylusMove;
        _target.StylusUp -= OnStylusUp;
        _target.StylusOutOfRange -= OnStylusOutOfRange;

        _target.LostMouseCapture -= OnLostMouseCapture;
        _target.LostTouchCapture -= OnLostTouchCapture;
        _target.LostStylusCapture -= OnLostStylusCapture;

        _target.PreviewKeyDown -= OnKeyDown;
        _target.PreviewKeyUp -= OnKeyUp;
        _target.TextInput -= OnTextInput;

        _target.GotKeyboardFocus -= OnGotKeyboardFocus;
        _target.LostKeyboardFocus -= OnLostKeyboardFocus;

        if (_inputControl is not null)
        {
            _inputControl.AccessibilityChanged -= OnAccessibilityChanged;
            _inputControl = null;
        }

        _pointerStates.Clear();
        _sink = null;
    }

    public void RequestPointerCapture(ICompositionInputSink sink, ulong pointerId)
    {
        if (!ReferenceEquals(_sink, sink))
        {
            return;
        }

        if (!_pointerStates.TryGetValue(pointerId, out var state))
        {
            if (pointerId == MousePointerId)
            {
                _target.CaptureMouse();
            }
            return;
        }

        switch (state.DeviceKind)
        {
            case PointerDeviceKind.Mouse:
                _target.CaptureMouse();
                break;
            case PointerDeviceKind.Touch when state.TouchDevice is not null:
                _target.CaptureTouch(state.TouchDevice);
                break;
            case PointerDeviceKind.Pen when state.StylusDevice is not null:
                state.StylusDevice.Capture(_target, CaptureMode.Element);
                break;
        }
    }

    public void ReleasePointerCapture(ICompositionInputSink sink, ulong pointerId)
    {
        if (!ReferenceEquals(_sink, sink))
        {
            return;
        }

        if (!_pointerStates.TryGetValue(pointerId, out var state))
        {
            if (pointerId == MousePointerId)
            {
                if (Mouse.Captured == _target)
                {
                    Mouse.Capture(null);
                }
            }
            return;
        }

        switch (state.DeviceKind)
        {
            case PointerDeviceKind.Mouse:
                if (Mouse.Captured == _target)
                {
                    Mouse.Capture(null);
                }
                break;
            case PointerDeviceKind.Touch when state.TouchDevice is not null:
                _target.ReleaseTouchCapture(state.TouchDevice);
                break;
            case PointerDeviceKind.Pen when state.StylusDevice is not null:
                if (state.StylusDevice.Captured == _target)
                {
                    state.StylusDevice.Capture(null);
                }
                break;
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

    private void OnMouseEnter(object sender, MouseEventArgs e) =>
        ForwardMouseEvent(e, PointerEventType.Enter, PointerButton.None);

    private void OnMouseMove(object sender, MouseEventArgs e) =>
        ForwardMouseEvent(e, PointerEventType.Move, PointerButton.None);

    private void OnMouseLeave(object sender, MouseEventArgs e) =>
        ForwardMouseEvent(e, PointerEventType.Leave, PointerButton.None, remove: true);

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _target.Focus();
        var button = MapMouseButton(e.ChangedButton);
        ForwardMouseEvent(e, PointerEventType.Down, button);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        var button = MapMouseButton(e.ChangedButton);
        ForwardMouseEvent(e, PointerEventType.Up, button, remove: true);
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var point = e.GetPosition(_target);
        ForwardPointerEvent(MousePointerId, PointerDeviceKind.Mouse, PointerEventType.Wheel, point, GetKeyboardModifiers(), PointerButton.None, 0, e.Delta);
    }

    private void OnTouchEnter(object sender, TouchEventArgs e) =>
        ForwardTouchEvent(e, PointerEventType.Enter);

    private void OnTouchLeave(object sender, TouchEventArgs e) =>
        ForwardTouchEvent(e, PointerEventType.Leave, remove: true);

    private void OnTouchDown(object sender, TouchEventArgs e)
    {
        _target.Focus();
        ForwardTouchEvent(e, PointerEventType.Down);
    }

    private void OnTouchMove(object sender, TouchEventArgs e) =>
        ForwardTouchEvent(e, PointerEventType.Move);

    private void OnTouchUp(object sender, TouchEventArgs e) =>
        ForwardTouchEvent(e, PointerEventType.Up, remove: true);

    private void OnStylusDown(object sender, StylusDownEventArgs e)
    {
        _target.Focus();
        ForwardStylusEvent(e, PointerEventType.Down);
    }

    private void OnStylusMove(object sender, StylusEventArgs e) =>
        ForwardStylusEvent(e, PointerEventType.Move);

    private void OnStylusUp(object sender, StylusEventArgs e) =>
        ForwardStylusEvent(e, PointerEventType.Up, remove: true);

    private void OnStylusOutOfRange(object sender, StylusEventArgs e) =>
        ForwardStylusEvent(e, PointerEventType.Leave, remove: true);

    private void OnLostMouseCapture(object sender, MouseEventArgs e) =>
        EmitCaptureLost(MousePointerId, PointerDeviceKind.Mouse);

    private void OnLostTouchCapture(object sender, TouchEventArgs e) =>
        EmitCaptureLost(GetTouchPointerId(e.TouchDevice), PointerDeviceKind.Touch);

    private void OnLostStylusCapture(object sender, StylusEventArgs e) =>
        EmitCaptureLost(GetStylusPointerId(e.StylusDevice), PointerDeviceKind.Pen);

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        var args = new CompositionKeyEventArgs(KeyEventType.Down, (int)e.Key, GetKeyboardModifiers(), e.IsRepeat, e.SystemKey.ToString());
        _sink?.ProcessKeyEvent(args);
        if (args.Handled)
        {
            e.Handled = true;
        }
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        var args = new CompositionKeyEventArgs(KeyEventType.Up, (int)e.Key, GetKeyboardModifiers(), e.IsRepeat, e.SystemKey.ToString());
        _sink?.ProcessKeyEvent(args);
        if (args.Handled)
        {
            e.Handled = true;
        }
    }

    private void OnTextInput(object sender, TextCompositionEventArgs e)
    {
        if (_sink is null)
        {
            return;
        }

        var args = new CompositionTextInputEventArgs(e.Text);
        _sink.ProcessTextInput(args);
        if (args.Handled)
        {
            e.Handled = true;
        }
    }

    private void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        _sink?.ProcessFocusChanged(true);

    private void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        _sink?.ProcessFocusChanged(false);

    private void OnAccessibilityChanged(object? sender, AccessibilityChangedEventArgs e) =>
        ApplyAccessibilityProperties();

    private void ApplyAccessibilityProperties()
    {
        if (_inputControl is null)
        {
            AutomationProperties.SetName(_target, null);
            AutomationProperties.SetHelpText(_target, null);
            AutomationProperties.SetAutomationId(_target, null);
            return;
        }

        var props = _inputControl.Accessibility;
        if (!props.IsAccessible)
        {
            AutomationProperties.SetName(_target, null);
            AutomationProperties.SetHelpText(_target, null);
            AutomationProperties.SetAutomationId(_target, props.AutomationId);
            AutomationProperties.SetLiveSetting(_target, AutomationLiveSetting.Off);
            return;
        }

        AutomationProperties.SetName(_target, props.Name);
        AutomationProperties.SetHelpText(_target, props.HelpText);
        AutomationProperties.SetAutomationId(_target, props.AutomationId);
        AutomationProperties.SetLiveSetting(_target, props.LiveSetting switch
        {
            AccessibilityLiveSetting.Assertive => AutomationLiveSetting.Assertive,
            AccessibilityLiveSetting.Polite => AutomationLiveSetting.Polite,
            _ => AutomationLiveSetting.Off,
        });
    }

    private void ForwardMouseEvent(MouseEventArgs e, PointerEventType eventType, PointerButton button, bool remove = false)
    {
        var position = e.GetPosition(_target);
        var modifiers = GetMouseModifiers(e);
        ForwardPointerEvent(MousePointerId, PointerDeviceKind.Mouse, eventType, position, modifiers, button);
        if (remove && eventType is PointerEventType.Up or PointerEventType.Leave)
        {
            _pointerStates.Remove(MousePointerId);
        }
    }

    private void ForwardTouchEvent(TouchEventArgs e, PointerEventType eventType, bool remove = false)
    {
        var point = e.GetTouchPoint(_target);
        var pointerId = GetTouchPointerId(e.TouchDevice);
        var modifiers = GetKeyboardModifiers();
        var state = GetOrCreatePointerState(pointerId, PointerDeviceKind.Touch);
        state.TouchDevice = e.TouchDevice;
        ForwardPointerEvent(pointerId, PointerDeviceKind.Touch, eventType, point.Position, modifiers, PointerButton.Primary);
        if (remove)
        {
            _pointerStates.Remove(pointerId);
        }
    }

    private void ForwardStylusEvent(StylusEventArgs e, PointerEventType eventType, bool remove = false)
    {
        var pointerId = GetStylusPointerId(e.StylusDevice);
        var position = e.GetPosition(_target);
        var modifiers = GetKeyboardModifiers();
        var state = GetOrCreatePointerState(pointerId, PointerDeviceKind.Pen);
        state.StylusDevice = e.StylusDevice;
        ForwardPointerEvent(pointerId, PointerDeviceKind.Pen, eventType, position, modifiers, PointerButton.Primary);
        if (remove)
        {
            _pointerStates.Remove(pointerId);
        }
    }

    private PointerState GetOrCreatePointerState(ulong pointerId, PointerDeviceKind deviceKind)
    {
        if (!_pointerStates.TryGetValue(pointerId, out var state))
        {
            state = new PointerState(deviceKind);
            _pointerStates[pointerId] = state;
        }

        state.DeviceKind = deviceKind;
        return state;
    }

    private void ForwardPointerEvent(
        ulong pointerId,
        PointerDeviceKind deviceKind,
        PointerEventType eventType,
        Point position,
        InputModifiers modifiers,
        PointerButton button,
        double wheelDeltaX = 0,
        double wheelDeltaY = 0)
    {
        if (_sink is null)
        {
            return;
        }

        var state = GetOrCreatePointerState(pointerId, deviceKind);
        var delta = new Vector(position.X - state.LastPosition.X, position.Y - state.LastPosition.Y);
        if (eventType == PointerEventType.Enter || double.IsNaN(state.LastPosition.X))
        {
            delta = new Vector(0, 0);
        }

        state.LastPosition = position;

        var args = new CompositionPointerEventArgs(
            eventType,
            deviceKind,
            modifiers,
            button,
            pointerId,
            position.X,
            position.Y,
            delta.X,
            delta.Y,
            wheelDeltaX,
            wheelDeltaY,
            TimeSpan.FromMilliseconds(Environment.TickCount64 & int.MaxValue));

        _sink.ProcessPointerEvent(args);
    }

    private void EmitCaptureLost(ulong pointerId, PointerDeviceKind kind)
    {
        if (!_pointerStates.ContainsKey(pointerId))
        {
            return;
        }

        ForwardPointerEvent(pointerId, kind, PointerEventType.CaptureLost, _pointerStates[pointerId].LastPosition, GetKeyboardModifiers(), PointerButton.None);
        _pointerStates.Remove(pointerId);
    }

    private static InputModifiers GetMouseModifiers(MouseEventArgs e)
    {
        var modifiers = InputModifiers.None;
        var keyboard = Keyboard.Modifiers;
        if ((keyboard & ModifierKeys.Shift) != 0)
        {
            modifiers |= InputModifiers.Shift;
        }
        if ((keyboard & ModifierKeys.Control) != 0)
        {
            modifiers |= InputModifiers.Control;
        }
        if ((keyboard & ModifierKeys.Alt) != 0)
        {
            modifiers |= InputModifiers.Alt;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            modifiers |= InputModifiers.PrimaryButton;
        }
        if (e.RightButton == MouseButtonState.Pressed)
        {
            modifiers |= InputModifiers.SecondaryButton;
        }
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            modifiers |= InputModifiers.MiddleButton;
        }
        if (e.XButton1 == MouseButtonState.Pressed)
        {
            modifiers |= InputModifiers.XButton1;
        }
        if (e.XButton2 == MouseButtonState.Pressed)
        {
            modifiers |= InputModifiers.XButton2;
        }

        return modifiers;
    }

    private static InputModifiers GetKeyboardModifiers()
    {
        var modifiers = InputModifiers.None;
        var keyboard = Keyboard.Modifiers;
        if ((keyboard & ModifierKeys.Shift) != 0)
        {
            modifiers |= InputModifiers.Shift;
        }
        if ((keyboard & ModifierKeys.Control) != 0)
        {
            modifiers |= InputModifiers.Control;
        }
        if ((keyboard & ModifierKeys.Alt) != 0)
        {
            modifiers |= InputModifiers.Alt;
        }

        return modifiers;
    }

    private static PointerButton MapMouseButton(MouseButton button) =>
        button switch
        {
            MouseButton.Left => PointerButton.Primary,
            MouseButton.Right => PointerButton.Secondary,
            MouseButton.Middle => PointerButton.Middle,
            MouseButton.XButton1 => PointerButton.XButton1,
            MouseButton.XButton2 => PointerButton.XButton2,
            _ => PointerButton.None,
        };

    private static ulong GetTouchPointerId(TouchDevice device) => (ulong)(0x10000 + device.Id);

    private static ulong GetStylusPointerId(StylusDevice device) => (ulong)(0x20000 + device.Id);

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

    private sealed class PointerState
    {
        public PointerState(PointerDeviceKind kind)
        {
            DeviceKind = kind;
            LastPosition = new Point(double.NaN, double.NaN);
        }

        public PointerDeviceKind DeviceKind { get; set; }
        public Point LastPosition { get; set; }
        public TouchDevice? TouchDevice { get; set; }
        public StylusDevice? StylusDevice { get; set; }
    }
}
