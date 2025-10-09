#if HAS_WINUI
using System;
using System.Collections.Generic;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.System;
using VelloSharp.Composition.Accessibility;
using VelloSharp.Composition.Controls;
using VelloSharp.Composition.Input;

namespace VelloSharp.ChartRuntime.Windows.WinUI;

public sealed class WinUICompositionInputSource : ICompositionInputSource
{
    private readonly UIElement _target;
    private readonly Dictionary<ulong, PointerState> _pointers = new();
    private ICompositionInputSink? _sink;
    private InputControl? _inputControl;
    private bool _disposed;

    public WinUICompositionInputSource(UIElement target)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _target.IsTabStop = true;
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

        _target.PointerEntered += OnPointerEntered;
        _target.PointerMoved += OnPointerMoved;
        _target.PointerExited += OnPointerExited;
        _target.PointerPressed += OnPointerPressed;
        _target.PointerReleased += OnPointerReleased;
        _target.PointerWheelChanged += OnPointerWheelChanged;
        _target.PointerCanceled += OnPointerCanceled;
        _target.PointerCaptureLost += OnPointerCaptureLost;

        _target.KeyDown += OnKeyDown;
        _target.KeyUp += OnKeyUp;
        _target.CharacterReceived += OnCharacterReceived;

        _target.GotFocus += OnGotFocus;
        _target.LostFocus += OnLostFocus;
    }

    public void Disconnect(ICompositionInputSink sink)
    {
        if (!ReferenceEquals(_sink, sink))
        {
            return;
        }

        _target.PointerEntered -= OnPointerEntered;
        _target.PointerMoved -= OnPointerMoved;
        _target.PointerExited -= OnPointerExited;
        _target.PointerPressed -= OnPointerPressed;
        _target.PointerReleased -= OnPointerReleased;
        _target.PointerWheelChanged -= OnPointerWheelChanged;
        _target.PointerCanceled -= OnPointerCanceled;
        _target.PointerCaptureLost -= OnPointerCaptureLost;

        _target.KeyDown -= OnKeyDown;
        _target.KeyUp -= OnKeyUp;
        _target.CharacterReceived -= OnCharacterReceived;

        _target.GotFocus -= OnGotFocus;
        _target.LostFocus -= OnLostFocus;

        if (_inputControl is not null)
        {
            _inputControl.AccessibilityChanged -= OnAccessibilityChanged;
            _inputControl = null;
        }

        _pointers.Clear();
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
            _target.CapturePointer(state.Pointer);
        }
    }

    public void ReleasePointerCapture(ICompositionInputSink sink, ulong pointerId)
    {
        if (!ReferenceEquals(_sink, sink))
        {
            return;
        }

        if (_pointers.TryGetValue(pointerId, out var state))
        {
            _target.ReleasePointerCaptures();
            state.Pointer = null;
        }
    }

    public void RequestFocus(ICompositionInputSink sink)
    {
        if (!ReferenceEquals(_sink, sink))
        {
            return;
        }

        _target.Focus(FocusState.Programmatic);
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e) =>
        ForwardPointerEvent(e, PointerEventType.Enter, PointerButton.None);

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e) =>
        ForwardPointerEvent(e, PointerEventType.Move, PointerButton.None);

    private void OnPointerExited(object sender, PointerRoutedEventArgs e) =>
        ForwardPointerEvent(e, PointerEventType.Leave, PointerButton.None, remove: true);

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _target.Focus(FocusState.Programmatic);
        var updateKind = e.GetCurrentPoint(_target).Properties.PointerUpdateKind;
        ForwardPointerEvent(e, PointerEventType.Down, MapPointerUpdateKind(updateKind));
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var updateKind = e.GetCurrentPoint(_target).Properties.PointerUpdateKind;
        ForwardPointerEvent(e, PointerEventType.Up, MapPointerUpdateKind(updateKind), remove: true);
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(_target);
        ForwardPointerEvent(e, PointerEventType.Wheel, PointerButton.None, wheelDeltaY: point.Properties.MouseWheelDelta);
    }

    private void OnPointerCanceled(object sender, PointerRoutedEventArgs e) =>
        ForwardPointerEvent(e, PointerEventType.Cancel, PointerButton.None, remove: true);

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        var pointerId = e.Pointer.PointerId;
        if (_pointers.TryGetValue(pointerId, out var state))
        {
            ForwardPointer(pointerId, state.DeviceKind, PointerEventType.CaptureLost, state.LastPoint, InputModifiers.None, PointerButton.None);
            _pointers.Remove(pointerId);
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var args = new CompositionKeyEventArgs(KeyEventType.Down, (int)e.Key, GetModifiers(e), e.KeyStatus.WasKeyDown, e.Key.ToString());
        _sink?.ProcessKeyEvent(args);
        if (args.Handled)
        {
            e.Handled = true;
        }
    }

    private void OnKeyUp(object sender, KeyRoutedEventArgs e)
    {
        var args = new CompositionKeyEventArgs(KeyEventType.Up, (int)e.Key, GetModifiers(e), e.KeyStatus.WasKeyDown, e.Key.ToString());
        _sink?.ProcessKeyEvent(args);
        if (args.Handled)
        {
            e.Handled = true;
        }
    }

    private void OnCharacterReceived(object sender, CharacterReceivedEventArgs e)
    {
        if (_sink is null)
        {
            return;
        }

        var args = new CompositionTextInputEventArgs(char.ConvertFromUtf32((int)e.KeyCode));
        _sink.ProcessTextInput(args);
        if (args.Handled)
        {
            e.Handled = true;
        }
    }

    private void OnGotFocus(object sender, RoutedEventArgs e) =>
        _sink?.ProcessFocusChanged(true);

    private void OnLostFocus(object sender, RoutedEventArgs e) =>
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
            return;
        }

        AutomationProperties.SetName(_target, props.Name);
        AutomationProperties.SetHelpText(_target, props.HelpText);
        AutomationProperties.SetAutomationId(_target, props.AutomationId);
    }

    private void ForwardPointerEvent(PointerRoutedEventArgs e, PointerEventType eventType, PointerButton button, bool remove = false, double wheelDeltaX = 0, double wheelDeltaY = 0)
    {
        var point = e.GetCurrentPoint(_target);
        var pointerId = point.PointerId;
        var deviceKind = MapDeviceKind(point.PointerDeviceType);
        var state = GetOrCreatePointerState(pointerId, deviceKind);
        state.Pointer = e.Pointer;

        var modifiers = GetModifiers(e);
        var position = point.Position;
        ForwardPointer(pointerId, deviceKind, eventType, position, modifiers, button, wheelDeltaX, wheelDeltaY);

        if (remove || eventType is PointerEventType.Cancel or PointerEventType.Up or PointerEventType.Leave)
        {
            _pointers.Remove(pointerId);
        }
    }

    private void ForwardPointer(ulong pointerId, PointerDeviceKind deviceKind, PointerEventType eventType, Point position, InputModifiers modifiers, PointerButton button, double wheelDeltaX = 0, double wheelDeltaY = 0)
    {
        if (_sink is null)
        {
            return;
        }

        var state = GetOrCreatePointerState(pointerId, deviceKind);
        var deltaX = position.X - state.LastPoint.X;
        var deltaY = position.Y - state.LastPoint.Y;
        if (double.IsNaN(state.LastPoint.X))
        {
            deltaX = 0;
            deltaY = 0;
        }

        state.LastPoint = position;

        var args = new CompositionPointerEventArgs(
            eventType,
            deviceKind,
            modifiers,
            button,
            pointerId,
            position.X,
            position.Y,
            deltaX,
            deltaY,
            wheelDeltaX,
            wheelDeltaY,
            TimeSpan.FromMilliseconds(Environment.TickCount64 & int.MaxValue));

        _sink.ProcessPointerEvent(args);
    }

    private PointerState GetOrCreatePointerState(ulong pointerId, PointerDeviceKind kind)
    {
        if (!_pointers.TryGetValue(pointerId, out var state))
        {
            state = new PointerState(kind);
            _pointers[pointerId] = state;
        }

        state.DeviceKind = kind;
        return state;
    }

    private static PointerDeviceKind MapDeviceKind(PointerDeviceType type) =>
        type switch
        {
            PointerDeviceType.Mouse => PointerDeviceKind.Mouse,
            PointerDeviceType.Touch => PointerDeviceKind.Touch,
            PointerDeviceType.Pen => PointerDeviceKind.Pen,
            _ => PointerDeviceKind.Unknown,
        };

    private static PointerButton MapPointerUpdateKind(PointerUpdateKind kind) =>
        kind switch
        {
            PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.LeftButtonReleased => PointerButton.Primary,
            PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased => PointerButton.Secondary,
            PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.MiddleButtonReleased => PointerButton.Middle,
            PointerUpdateKind.XButton1Pressed or PointerUpdateKind.XButton1Released => PointerButton.XButton1,
            PointerUpdateKind.XButton2Pressed or PointerUpdateKind.XButton2Released => PointerButton.XButton2,
            _ => PointerButton.None,
        };

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
        => (InputKeyboardSource.GetKeyStateForCurrentThread(key) & InputVirtualKeyStates.Down) != 0;

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
            LastPoint = new Windows.Foundation.Point(double.NaN, double.NaN);
        }

        public PointerDeviceKind DeviceKind { get; set; }
        public Point LastPoint { get; set; }
        public Pointer? Pointer { get; set; }
    }
}
#endif

