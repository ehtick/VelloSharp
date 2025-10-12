#if WINDOWS
using System;
using System.Collections.Generic;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WindowsPoint = Windows.Foundation.Point;
using Windows.System;
using VelloSharp.Composition.Input;
using Windows.UI.Core;

namespace VelloSharp.Maui.Input;

public sealed partial class MauiCompositionInputSource
{
    private sealed class PointerState
    {
        public PointerState(PointerDeviceKind kind)
        {
            DeviceKind = kind;
            LastPoint = new WindowsPoint(double.NaN, double.NaN);
        }

        public PointerDeviceKind DeviceKind { get; set; }
        public WindowsPoint LastPoint { get; set; }
        public Pointer? Pointer { get; set; }
    }

    private UIElement? _uiElement;
    private readonly Dictionary<ulong, PointerState> _pointerStates = new();

    partial void InitializePlatformState(object platformView)
    {
        _uiElement = platformView as UIElement ?? throw new InvalidOperationException("The MAUI VelloView requires a WinUI UIElement platform implementation.");
        _uiElement.IsTabStop = true;
    }

    partial void ConnectPlatform(object platformView)
    {
        if (_uiElement is null)
        {
            return;
        }

        _uiElement.PointerEntered += OnPointerEntered;
        _uiElement.PointerMoved += OnPointerMoved;
        _uiElement.PointerExited += OnPointerExited;
        _uiElement.PointerPressed += OnPointerPressed;
        _uiElement.PointerReleased += OnPointerReleased;
        _uiElement.PointerWheelChanged += OnPointerWheelChanged;
        _uiElement.PointerCanceled += OnPointerCanceled;
        _uiElement.PointerCaptureLost += OnPointerCaptureLost;

        _uiElement.GotFocus += OnGotFocus;
        _uiElement.LostFocus += OnLostFocus;
    }

    partial void DisconnectPlatform(object platformView)
    {
        if (_uiElement is null)
        {
            return;
        }

        _uiElement.PointerEntered -= OnPointerEntered;
        _uiElement.PointerMoved -= OnPointerMoved;
        _uiElement.PointerExited -= OnPointerExited;
        _uiElement.PointerPressed -= OnPointerPressed;
        _uiElement.PointerReleased -= OnPointerReleased;
        _uiElement.PointerWheelChanged -= OnPointerWheelChanged;
        _uiElement.PointerCanceled -= OnPointerCanceled;
        _uiElement.PointerCaptureLost -= OnPointerCaptureLost;

        _uiElement.GotFocus -= OnGotFocus;
        _uiElement.LostFocus -= OnLostFocus;

        _pointerStates.Clear();
    }

    partial void DisposePlatform(object platformView)
    {
        _pointerStates.Clear();
        _uiElement = null;
    }

    partial void RequestPointerCapturePlatform(object platformView, ulong pointerId)
    {
        if (_uiElement is null)
        {
            return;
        }

        if (_pointerStates.TryGetValue(pointerId, out var state) && state.Pointer is not null)
        {
            _uiElement.CapturePointer(state.Pointer);
        }
    }

    partial void ReleasePointerCapturePlatform(object platformView, ulong pointerId)
    {
        if (_uiElement is null)
        {
            return;
        }

        _uiElement.ReleasePointerCaptures();
    }

    partial void RequestFocusPlatform(object platformView)
    {
        if (_uiElement is Control control)
        {
            control.Focus(FocusState.Programmatic);
        }
        else
        {
            _uiElement?.Focus(FocusState.Programmatic);
        }
    }

    private void OnGotFocus(object sender, RoutedEventArgs e)
        => ForwardFocusChanged(true);

    private void OnLostFocus(object sender, RoutedEventArgs e)
        => ForwardFocusChanged(false);

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        var state = GetOrAddPointerState(e);
        var position = e.GetCurrentPoint(null).Position;
        state.LastPoint = position;

        var args = new CompositionPointerEventArgs(
            PointerEventType.Enter,
            state.DeviceKind,
            GetModifiers(e),
            PointerButton.None,
            e.Pointer.PointerId,
            position.X,
            position.Y,
            0,
            0,
            0,
            0,
            ToTimestamp(e));

        ForwardPointerEvent(args);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var state = GetOrAddPointerState(e);
        var point = e.GetCurrentPoint(null);
        var position = point.Position;
        var last = state.LastPoint;
        var deltaX = double.IsNaN(last.X) ? 0 : position.X - last.X;
        var deltaY = double.IsNaN(last.Y) ? 0 : position.Y - last.Y;
        state.LastPoint = position;

        var args = new CompositionPointerEventArgs(
            PointerEventType.Move,
            state.DeviceKind,
            GetModifiers(e),
            PointerButton.None,
            e.Pointer.PointerId,
            position.X,
            position.Y,
            deltaX,
            deltaY,
            0,
            0,
            ToTimestamp(e));

        e.Handled = ForwardPointerEvent(args);
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerStates.TryGetValue(e.Pointer.PointerId, out var state))
        {
            var point = e.GetCurrentPoint(null).Position;
            var args = new CompositionPointerEventArgs(
                PointerEventType.Leave,
                state.DeviceKind,
                GetModifiers(e),
                PointerButton.None,
                e.Pointer.PointerId,
                point.X,
                point.Y,
                0,
                0,
                0,
                0,
                ToTimestamp(e));

            ForwardPointerEvent(args);
            _pointerStates.Remove(e.Pointer.PointerId);
        }
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var state = GetOrAddPointerState(e);
        var point = e.GetCurrentPoint(null);
        state.Pointer = e.Pointer;
        var position = point.Position;
        state.LastPoint = position;

        var args = new CompositionPointerEventArgs(
            PointerEventType.Down,
            state.DeviceKind,
            GetModifiers(e),
            MapPointerUpdateKind(point.Properties.PointerUpdateKind),
            e.Pointer.PointerId,
            position.X,
            position.Y,
            0,
            0,
            0,
            0,
            ToTimestamp(e));

        e.Handled = ForwardPointerEvent(args);
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_pointerStates.TryGetValue(e.Pointer.PointerId, out var state))
        {
            return;
        }

        var point = e.GetCurrentPoint(null);
        var position = point.Position;
        state.LastPoint = position;

        var args = new CompositionPointerEventArgs(
            PointerEventType.Up,
            state.DeviceKind,
            GetModifiers(e),
            MapPointerUpdateKind(point.Properties.PointerUpdateKind),
            e.Pointer.PointerId,
            position.X,
            position.Y,
            0,
            0,
            0,
            0,
            ToTimestamp(e));

        e.Handled = ForwardPointerEvent(args);
    }

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var state = GetOrAddPointerState(e);
        var point = e.GetCurrentPoint(null);
        var position = point.Position;
        state.LastPoint = position;

        var wheelArgs = new CompositionPointerEventArgs(
            PointerEventType.Wheel,
            state.DeviceKind,
            GetModifiers(e),
            PointerButton.None,
            e.Pointer.PointerId,
            position.X,
            position.Y,
            0,
            0,
            0,
            point.Properties.MouseWheelDelta,
            ToTimestamp(e));

        e.Handled = ForwardPointerEvent(wheelArgs);
    }

    private void OnPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (!_pointerStates.TryGetValue(e.Pointer.PointerId, out var state))
        {
            return;
        }

        var position = e.GetCurrentPoint(null).Position;
        state.LastPoint = position;

        var args = new CompositionPointerEventArgs(
            PointerEventType.Cancel,
            state.DeviceKind,
            GetModifiers(e),
            PointerButton.None,
            e.Pointer.PointerId,
            position.X,
            position.Y,
            0,
            0,
            0,
            0,
            ToTimestamp(e));

        ForwardPointerEvent(args);
        _pointerStates.Remove(e.Pointer.PointerId);
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        var args = new CompositionPointerEventArgs(
            PointerEventType.CaptureLost,
            PointerDeviceKind.Unknown,
            InputModifiers.None,
            PointerButton.None,
            e.Pointer.PointerId,
            0,
            0,
            0,
            0,
            0,
            0,
            ToTimestamp(e));

        ForwardPointerEvent(args);
    }

    private PointerState GetOrAddPointerState(PointerRoutedEventArgs e)
    {
        var pointerId = e.Pointer.PointerId;
        if (_pointerStates.TryGetValue(pointerId, out var state))
        {
            return state;
        }

        state = new PointerState(MapDeviceKind(e.Pointer.PointerDeviceType))
        {
            Pointer = e.Pointer,
        };
        _pointerStates[pointerId] = state;
        return state;
    }

    private static PointerDeviceKind MapDeviceKind(PointerDeviceType type)
        => type switch
        {
            PointerDeviceType.Mouse => PointerDeviceKind.Mouse,
            PointerDeviceType.Touch => PointerDeviceKind.Touch,
            PointerDeviceType.Pen => PointerDeviceKind.Pen,
            _ => PointerDeviceKind.Unknown,
        };

    private static PointerButton MapPointerUpdateKind(PointerUpdateKind kind)
        => kind switch
        {
            PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.LeftButtonReleased => PointerButton.Primary,
            PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased => PointerButton.Secondary,
            PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.MiddleButtonReleased => PointerButton.Middle,
            PointerUpdateKind.XButton1Pressed or PointerUpdateKind.XButton1Released => PointerButton.XButton1,
            PointerUpdateKind.XButton2Pressed or PointerUpdateKind.XButton2Released => PointerButton.XButton2,
            _ => PointerButton.None,
        };

    private static InputModifiers GetModifiers(PointerRoutedEventArgs e)
    {
        var modifiers = InputModifiers.None;
        var properties = e.GetCurrentPoint(null).Properties;

        if (properties.IsLeftButtonPressed)
        {
            modifiers |= InputModifiers.PrimaryButton;
        }
        if (properties.IsRightButtonPressed)
        {
            modifiers |= InputModifiers.SecondaryButton;
        }
        if (properties.IsMiddleButtonPressed)
        {
            modifiers |= InputModifiers.MiddleButton;
        }
        if (properties.IsXButton1Pressed)
        {
            modifiers |= InputModifiers.XButton1;
        }
        if (properties.IsXButton2Pressed)
        {
            modifiers |= InputModifiers.XButton2;
        }

        if (IsKeyDown(VirtualKey.Shift))
        {
            modifiers |= InputModifiers.Shift;
        }
        if (IsKeyDown(VirtualKey.Control))
        {
            modifiers |= InputModifiers.Control;
        }
        if (IsKeyDown(VirtualKey.Menu))
        {
            modifiers |= InputModifiers.Alt;
        }

        return modifiers;
    }

    private static bool IsKeyDown(VirtualKey key)
    {
        var state = InputKeyboardSource.GetKeyStateForCurrentThread(key);
        return (state & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
    }

    private static TimeSpan ToTimestamp(PointerRoutedEventArgs e)
        => TimeSpan.FromMilliseconds(Environment.TickCount64 & int.MaxValue);
}
#endif
