using System;
using System.Collections.Generic;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using VelloSharp.Composition.Accessibility;
using VelloSharp.Composition.Controls;
using VelloSharp.Composition.Input;
using VelloSharp.Windows.Controls;

namespace VelloSharp.Windows.Input;

internal sealed class VelloSwapChainInputSource : ICompositionInputSource
{
    private sealed class PointerState
    {
        public PointerState(Pointer devicePointer)
        {
            Pointer = devicePointer;
            DeviceKind = MapPointerDeviceType(devicePointer.PointerDeviceType);
        }

        public Pointer Pointer { get; }
        public PointerDeviceKind DeviceKind { get; }
        public Point LastPoint { get; set; } = new(double.NaN, double.NaN);
    }

    private readonly VelloSwapChainControl _control;
    private readonly Dictionary<ulong, PointerState> _pointers = new();
    private ICompositionInputSink? _sink;
    private InputControl? _inputControl;

    public VelloSwapChainInputSource(VelloSwapChainControl control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _control.IsTabStop = true;
    }

    public void Connect(ICompositionInputSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        if (_sink is not null && !ReferenceEquals(_sink, sink))
        {
            throw new InvalidOperationException("An input sink is already connected to this VelloSwapChainInputSource.");
        }

        _sink = sink;

        if (sink is InputControl inputControl)
        {
            _inputControl = inputControl;
            _inputControl.AccessibilityChanged += OnAccessibilityChanged;
            _inputControl.AccessibilityActionInvoked += OnAccessibilityActionInvoked;
            ApplyAccessibilityProperties();
        }

        AttachHandlers();
    }

    public void Disconnect(ICompositionInputSink sink)
    {
        if (!ReferenceEquals(_sink, sink))
        {
            return;
        }

        DetachHandlers();
        _pointers.Clear();

        if (_inputControl is not null)
        {
            _inputControl.AccessibilityChanged -= OnAccessibilityChanged;
            _inputControl.AccessibilityActionInvoked -= OnAccessibilityActionInvoked;
            _inputControl = null;
        }

        _sink = null;
    }

    public void RequestPointerCapture(ICompositionInputSink sink, ulong pointerId)
    {
        if (!ReferenceEquals(_sink, sink))
        {
            return;
        }

        if (_pointers.TryGetValue(pointerId, out var state))
        {
            _control.CapturePointer(state.Pointer);
        }
    }

    public void ReleasePointerCapture(ICompositionInputSink sink, ulong pointerId)
    {
        if (!ReferenceEquals(_sink, sink))
        {
            return;
        }

        _control.ReleasePointerCaptures();
    }

    public void RequestFocus(ICompositionInputSink sink)
    {
        if (!ReferenceEquals(_sink, sink))
        {
            return;
        }

        _control.Focus(FocusState.Programmatic);
    }

    private void AttachHandlers()
    {
        _control.PointerEntered += OnPointerEntered;
        _control.PointerMoved += OnPointerMoved;
        _control.PointerExited += OnPointerExited;
        _control.PointerPressed += OnPointerPressed;
        _control.PointerReleased += OnPointerReleased;
        _control.PointerCanceled += OnPointerCanceled;
        _control.PointerCaptureLost += OnPointerCaptureLost;
        _control.PointerWheelChanged += OnPointerWheelChanged;

        _control.KeyDown += OnKeyDown;
        _control.KeyUp += OnKeyUp;
        _control.CharacterReceived += OnCharacterReceived;
        _control.GotFocus += OnGotFocus;
        _control.LostFocus += OnLostFocus;
    }

    private void DetachHandlers()
    {
        _control.PointerEntered -= OnPointerEntered;
        _control.PointerMoved -= OnPointerMoved;
        _control.PointerExited -= OnPointerExited;
        _control.PointerPressed -= OnPointerPressed;
        _control.PointerReleased -= OnPointerReleased;
        _control.PointerCanceled -= OnPointerCanceled;
        _control.PointerCaptureLost -= OnPointerCaptureLost;
        _control.PointerWheelChanged -= OnPointerWheelChanged;

        _control.KeyDown -= OnKeyDown;
        _control.KeyUp -= OnKeyUp;
        _control.CharacterReceived -= OnCharacterReceived;
        _control.GotFocus -= OnGotFocus;
        _control.LostFocus -= OnLostFocus;
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
        => ForwardPointerEvent(e, PointerEventType.Enter, PointerButton.None);

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
        => ForwardPointerEvent(e, PointerEventType.Move, PointerButton.None);

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
        => ForwardPointerEvent(e, PointerEventType.Leave, PointerButton.None, remove: true);

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _control.Focus(FocusState.Programmatic);
        var point = e.GetCurrentPoint(_control);
        ForwardPointerEvent(e, PointerEventType.Down, MapPointerUpdate(point.Properties.PointerUpdateKind));
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_control);
        ForwardPointerEvent(e, PointerEventType.Up, MapPointerUpdate(point.Properties.PointerUpdateKind), remove: true);
    }

    private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
        => ForwardPointerEvent(e, PointerEventType.Cancel, PointerButton.None, remove: true);

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        var pointerId = e.Pointer.PointerId;
        if (!_pointers.TryGetValue(pointerId, out var state))
        {
            return;
        }

        var position = e.GetCurrentPoint(_control).Position;
        ForwardPointer(pointerId, state.DeviceKind, PointerEventType.CaptureLost, position, InputModifiers.None, PointerButton.None, deltaX: 0, deltaY: 0);
        _pointers.Remove(pointerId);
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_control);
        var modifiers = GetModifiers(e);
        var position = point.Position;
        var wheelDelta = point.Properties.MouseWheelDelta;
        ForwardPointer(
            point.PointerId,
            MapPointerDeviceType(point.PointerDeviceType),
            PointerEventType.Wheel,
            position,
            modifiers,
            PointerButton.None,
            deltaX: 0,
            deltaY: wheelDelta);
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_sink is null)
        {
            return;
        }

        var args = new CompositionKeyEventArgs(
            KeyEventType.Down,
            (int)e.Key,
            GetModifiers(e),
            e.KeyStatus.WasKeyDown,
            e.Key.ToString());

        _sink.ProcessKeyEvent(args);
        e.Handled = e.Handled || args.Handled;
    }

    private void OnKeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (_sink is null)
        {
            return;
        }

        var args = new CompositionKeyEventArgs(
            KeyEventType.Up,
            (int)e.Key,
            GetModifiers(e),
            e.KeyStatus.WasKeyDown,
            e.Key.ToString());

        _sink.ProcessKeyEvent(args);
        e.Handled = e.Handled || args.Handled;
    }

    private void OnCharacterReceived(object sender, CharacterReceivedRoutedEventArgs e)
    {
        if (_sink is null)
        {
            return;
        }

        var text = e.Character.ToString();
        var args = new CompositionTextInputEventArgs(text);
        _sink.ProcessTextInput(args);
        e.Handled = e.Handled || args.Handled;
    }

    private void OnGotFocus(object sender, RoutedEventArgs e)
        => _sink?.ProcessFocusChanged(true);

    private void OnLostFocus(object sender, RoutedEventArgs e)
        => _sink?.ProcessFocusChanged(false);

    private void OnAccessibilityChanged(object? sender, AccessibilityChangedEventArgs e)
        => ApplyAccessibilityProperties();

    private void OnAccessibilityActionInvoked(object? sender, AccessibilityActionEventArgs e)
    {
        if (_inputControl is null)
        {
            return;
        }

        switch (e.Action)
        {
            case AccessibilityAction.Invoke:
                _control.RequestAccessKitAction("click", _control.AccessibilityFocusNodeId);
                break;
            case AccessibilityAction.Focus:
                _control.RequestAccessKitAction("focus", _control.AccessibilityFocusNodeId);
                break;
        }
    }

    private void ApplyAccessibilityProperties()
    {
        if (_inputControl is null)
        {
            return;
        }

        var props = _inputControl.Accessibility;
        if (!string.IsNullOrWhiteSpace(props.Name))
        {
            AutomationProperties.SetName(_control, props.Name);
        }

        if (!string.IsNullOrWhiteSpace(props.HelpText))
        {
            AutomationProperties.SetHelpText(_control, props.HelpText);
        }
    }

    private void ForwardPointerEvent(PointerRoutedEventArgs e, PointerEventType type, PointerButton button, bool remove = false)
    {
        if (_sink is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(_control);
        var pointerId = point.PointerId;
        if (!_pointers.TryGetValue(pointerId, out var state))
        {
            state = new PointerState(e.Pointer);
            _pointers[pointerId] = state;
        }

        var position = point.Position;
        var last = state.LastPoint;
        var deltaX = double.IsNaN(last.X) ? 0 : position.X - last.X;
        var deltaY = double.IsNaN(last.Y) ? 0 : position.Y - last.Y;
        state.LastPoint = position;

        var modifiers = GetModifiers(e);
        ForwardPointer(pointerId, state.DeviceKind, type, position, modifiers, button, deltaX, deltaY);

        if (remove)
        {
            _pointers.Remove(pointerId);
        }
    }

    private void ForwardPointer(
        ulong pointerId,
        PointerDeviceKind deviceKind,
        PointerEventType type,
        Point position,
        InputModifiers modifiers,
        PointerButton button,
        double deltaX = 0,
        double deltaY = 0)
    {
        if (_sink is null)
        {
            return;
        }

        var timestamp = TimeSpan.FromMilliseconds(Environment.TickCount64);

        var args = new CompositionPointerEventArgs(
            type,
            deviceKind,
            modifiers,
            button,
            pointerId,
            position.X,
            position.Y,
            deltaX,
            deltaY,
            wheelDeltaX: 0,
            wheelDeltaY: 0,
            timestamp);

        _sink.ProcessPointerEvent(args);
    }

    public void Dispose()
    {
        DetachHandlers();
        _pointers.Clear();

        if (_inputControl is not null)
        {
            _inputControl.AccessibilityChanged -= OnAccessibilityChanged;
            _inputControl.AccessibilityActionInvoked -= OnAccessibilityActionInvoked;
            _inputControl = null;
        }

        _sink = null;
    }

    private static InputModifiers GetModifiers(KeyRoutedEventArgs e)
    {
        var modifiers = InputModifiers.None;

        if (IsVirtualKeyDown(VirtualKey.Shift))
        {
            modifiers |= InputModifiers.Shift;
        }

        if (IsVirtualKeyDown(VirtualKey.Control))
        {
            modifiers |= InputModifiers.Control;
        }

        if (IsVirtualKeyDown(VirtualKey.Menu))
        {
            modifiers |= InputModifiers.Alt;
        }

        return modifiers;
    }

    private static InputModifiers GetModifiers(PointerRoutedEventArgs e)
    {
        var modifiers = InputModifiers.None;
        var props = e.GetCurrentPoint(null).Properties;

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

        if (IsVirtualKeyDown(VirtualKey.Shift))
        {
            modifiers |= InputModifiers.Shift;
        }

        if (IsVirtualKeyDown(VirtualKey.Control))
        {
            modifiers |= InputModifiers.Control;
        }

        if (IsVirtualKeyDown(VirtualKey.Menu))
        {
            modifiers |= InputModifiers.Alt;
        }

        return modifiers;
    }

    private static bool IsVirtualKeyDown(VirtualKey key)
    {
        var state = InputKeyboardSource.GetKeyStateForCurrentThread(key);
        return (state & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    }

    private static PointerButton MapPointerUpdate(PointerUpdateKind kind)
        => kind switch
        {
            PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.LeftButtonReleased => PointerButton.Primary,
            PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased => PointerButton.Secondary,
            PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.MiddleButtonReleased => PointerButton.Middle,
            PointerUpdateKind.XButton1Pressed or PointerUpdateKind.XButton1Released => PointerButton.XButton1,
            PointerUpdateKind.XButton2Pressed or PointerUpdateKind.XButton2Released => PointerButton.XButton2,
            _ => PointerButton.None,
        };

    private static PointerDeviceKind MapPointerDeviceType(PointerDeviceType deviceType)
        => deviceType switch
        {
            PointerDeviceType.Mouse => PointerDeviceKind.Mouse,
            PointerDeviceType.Touch => PointerDeviceKind.Touch,
            PointerDeviceType.Pen => PointerDeviceKind.Pen,
            _ => PointerDeviceKind.Unknown,
        };
}
