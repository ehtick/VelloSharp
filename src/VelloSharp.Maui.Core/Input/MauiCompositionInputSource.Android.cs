#if ANDROID
using System;
using System.Collections.Generic;
using Android.OS;
using Android.Views;
using VelloSharp.Composition.Input;
using AView = Android.Views.View;

namespace VelloSharp.Maui.Input;

public sealed partial class MauiCompositionInputSource
{
    private sealed class PointerState
    {
        public PointerState(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; set; }
        public float Y { get; set; }
    }

    private AView? _androidView;
    private readonly Dictionary<ulong, PointerState> _pointerStates = new();
    private readonly object _pointerLock = new();

    partial void InitializePlatformState(object platformView)
    {
        _androidView = platformView as AView ?? throw new InvalidOperationException("The MAUI VelloView requires an Android View platform implementation.");
    }

    partial void ConnectPlatform(object platformView)
    {
        if (_androidView is null)
        {
            return;
        }

        _androidView.Touch += OnTouch;
        _androidView.GenericMotion += OnGenericMotion;
        _androidView.Hover += OnHover;
        _androidView.FocusChange += OnFocusChange;
    }

    partial void DisconnectPlatform(object platformView)
    {
        if (_androidView is null)
        {
            return;
        }

        _androidView.Touch -= OnTouch;
        _androidView.GenericMotion -= OnGenericMotion;
        _androidView.Hover -= OnHover;
        _androidView.FocusChange -= OnFocusChange;

        lock (_pointerLock)
        {
            _pointerStates.Clear();
        }
    }

    partial void DisposePlatform(object platformView)
    {
        lock (_pointerLock)
        {
            _pointerStates.Clear();
        }

        _androidView = null;
    }

    partial void RequestPointerCapturePlatform(object platformView, ulong pointerId)
    {
        if (_androidView is null)
        {
            return;
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            _androidView.RequestPointerCapture();
        }
    }

    partial void ReleasePointerCapturePlatform(object platformView, ulong pointerId)
    {
        if (_androidView is null)
        {
            return;
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            _androidView.ReleasePointerCapture();
        }
    }

    partial void RequestFocusPlatform(object platformView)
    {
        _androidView?.RequestFocus();
    }

    private void OnFocusChange(object? sender, AView.FocusChangeEventArgs e)
    {
        ForwardFocusChanged(e.HasFocus);
    }

    private void OnTouch(object? sender, AView.TouchEventArgs e)
    {
        if (e.Event is null)
        {
            return;
        }

        var handled = HandleMotionEvent(e.Event);
        e.Handled = handled;
    }

    private void OnGenericMotion(object? sender, AView.GenericMotionEventArgs e)
    {
        if (e.Event is null)
        {
            return;
        }

        var handled = false;

        switch (e.Event.ActionMasked)
        {
            case MotionEventActions.Scroll:
                handled = HandleScroll(e.Event);
                break;
            case MotionEventActions.HoverMove:
            case MotionEventActions.HoverEnter:
            case MotionEventActions.HoverExit:
                handled = HandleHover(e.Event);
                break;
        }

        e.Handled = handled;
    }

    private void OnHover(object? sender, AView.HoverEventArgs e)
    {
        if (e.Event is null)
        {
            return;
        }

        e.Handled = HandleHover(e.Event);
    }

    private bool HandleMotionEvent(MotionEvent motion)
    {
        var handled = false;

        switch (motion.ActionMasked)
        {
            case MotionEventActions.Down:
            case MotionEventActions.PointerDown:
                handled = HandlePointerDown(motion, motion.ActionIndex);
                break;
            case MotionEventActions.Up:
            case MotionEventActions.PointerUp:
                handled = HandlePointerUp(motion, motion.ActionIndex);
                break;
            case MotionEventActions.Move:
                handled = HandlePointerMove(motion);
                break;
            case MotionEventActions.Cancel:
                handled = HandlePointerCancel(motion);
                break;
            case MotionEventActions.Outside:
                handled = HandlePointerLeave(motion, motion.ActionIndex);
                break;
        }

        return handled;
    }

    private bool HandlePointerDown(MotionEvent motion, int pointerIndex)
    {
        var pointerId = (ulong)motion.GetPointerId(pointerIndex);
        var x = motion.GetX(pointerIndex);
        var y = motion.GetY(pointerIndex);

        lock (_pointerLock)
        {
            _pointerStates[pointerId] = new PointerState(x, y);
        }

        var modifiers = ToModifiers(motion);
        var deviceKind = ToDeviceKind(motion, pointerIndex);
        if (deviceKind == PointerDeviceKind.Touch)
        {
            modifiers |= InputModifiers.PrimaryButton;
        }
        var timestamp = ToTimestamp(motion);
        var button = ToPointerButton(motion, pointerIndex, true);

        var enterArgs = new CompositionPointerEventArgs(
            PointerEventType.Enter,
            deviceKind,
            modifiers,
            PointerButton.None,
            pointerId,
            x,
            y,
            0,
            0,
            0,
            0,
            timestamp);

        ForwardPointerEvent(enterArgs);

        var downArgs = new CompositionPointerEventArgs(
            PointerEventType.Down,
            deviceKind,
            modifiers,
            button,
            pointerId,
            x,
            y,
            0,
            0,
            0,
            0,
            timestamp);

        return ForwardPointerEvent(downArgs);
    }

    private bool HandlePointerUp(MotionEvent motion, int pointerIndex)
    {
        var pointerId = (ulong)motion.GetPointerId(pointerIndex);
        var x = motion.GetX(pointerIndex);
        var y = motion.GetY(pointerIndex);
        float deltaX = 0;
        float deltaY = 0;

        lock (_pointerLock)
        {
            if (_pointerStates.TryGetValue(pointerId, out var state))
            {
                deltaX = x - state.X;
                deltaY = y - state.Y;
            }

            _pointerStates.Remove(pointerId);
        }

        var modifiers = ToModifiers(motion);
        var deviceKind = ToDeviceKind(motion, pointerIndex);
        if (deviceKind == PointerDeviceKind.Touch)
        {
            modifiers |= InputModifiers.PrimaryButton;
        }
        var timestamp = ToTimestamp(motion);
        var button = ToPointerButton(motion, pointerIndex, false);

        var upArgs = new CompositionPointerEventArgs(
            PointerEventType.Up,
            deviceKind,
            modifiers,
            button,
            pointerId,
            x,
            y,
            deltaX,
            deltaY,
            0,
            0,
            timestamp);

        var handled = ForwardPointerEvent(upArgs);

        var leaveArgs = new CompositionPointerEventArgs(
            PointerEventType.Leave,
            deviceKind,
            modifiers & ~InputModifiers.PrimaryButton,
            PointerButton.None,
            pointerId,
            x,
            y,
            0,
            0,
            0,
            0,
            timestamp);

        ForwardPointerEvent(leaveArgs);
        return handled;
    }

    private bool HandlePointerMove(MotionEvent motion)
    {
        var handled = false;
        var timestamp = ToTimestamp(motion);

        for (var index = 0; index < motion.PointerCount; index++)
        {
            var pointerId = (ulong)motion.GetPointerId(index);
            var x = motion.GetX(index);
            var y = motion.GetY(index);
            float deltaX;
            float deltaY;

            lock (_pointerLock)
            {
                if (_pointerStates.TryGetValue(pointerId, out var state))
                {
                    deltaX = x - state.X;
                    deltaY = y - state.Y;
                    state.X = x;
                    state.Y = y;
                }
                else
                {
                    _pointerStates[pointerId] = new PointerState(x, y);
                    deltaX = 0;
                    deltaY = 0;
                }
            }

            var deviceKind = ToDeviceKind(motion, index);
            var modifiers = ToModifiers(motion);
            if (deviceKind == PointerDeviceKind.Touch)
            {
                modifiers |= InputModifiers.PrimaryButton;
            }

            var args = new CompositionPointerEventArgs(
                PointerEventType.Move,
                deviceKind,
                modifiers,
                PointerButton.None,
                pointerId,
                x,
                y,
                deltaX,
                deltaY,
                0,
                0,
                timestamp);

            handled |= ForwardPointerEvent(args);
        }

        return handled;
    }

    private bool HandlePointerCancel(MotionEvent motion)
    {
        var handled = false;
        var timestamp = ToTimestamp(motion);

        for (var index = 0; index < motion.PointerCount; index++)
        {
            var pointerId = (ulong)motion.GetPointerId(index);
            float x;
            float y;

            lock (_pointerLock)
            {
                if (_pointerStates.TryGetValue(pointerId, out var state))
                {
                    x = state.X;
                    y = state.Y;
                }
                else
                {
                    x = motion.GetX(index);
                    y = motion.GetY(index);
                }

                _pointerStates.Remove(pointerId);
            }

            var deviceKind = ToDeviceKind(motion, index);
            var modifiers = ToModifiers(motion);
            if (deviceKind == PointerDeviceKind.Touch)
            {
                modifiers |= InputModifiers.PrimaryButton;
            }

            var args = new CompositionPointerEventArgs(
                PointerEventType.Cancel,
                deviceKind,
                modifiers,
                PointerButton.None,
                pointerId,
                x,
                y,
                0,
                0,
                0,
                0,
                timestamp);

            handled |= ForwardPointerEvent(args);

            var leaveArgs = new CompositionPointerEventArgs(
                PointerEventType.Leave,
                deviceKind,
                modifiers & ~InputModifiers.PrimaryButton,
                PointerButton.None,
                pointerId,
                x,
                y,
                0,
                0,
                0,
                0,
                timestamp);

            ForwardPointerEvent(leaveArgs);
        }

        return handled;
    }

    private bool HandlePointerLeave(MotionEvent motion, int pointerIndex)
    {
        var pointerId = (ulong)motion.GetPointerId(pointerIndex);
        float x;
        float y;

        lock (_pointerLock)
        {
            if (_pointerStates.TryGetValue(pointerId, out var state))
            {
                x = state.X;
                y = state.Y;
            }
            else
            {
                x = motion.GetX(pointerIndex);
                y = motion.GetY(pointerIndex);
            }

            _pointerStates.Remove(pointerId);
        }

        var deviceKind = ToDeviceKind(motion, pointerIndex);
        var modifiers = ToModifiers(motion);
        if (deviceKind == PointerDeviceKind.Touch)
        {
            modifiers |= InputModifiers.PrimaryButton;
        }

        var args = new CompositionPointerEventArgs(
            PointerEventType.Leave,
            deviceKind,
            modifiers & ~InputModifiers.PrimaryButton,
            PointerButton.None,
            pointerId,
            x,
            y,
            0,
            0,
            0,
            0,
            ToTimestamp(motion));

        return ForwardPointerEvent(args);
    }

    private bool HandleHover(MotionEvent motion)
    {
        var action = motion.ActionMasked;

        if (action == MotionEventActions.HoverEnter)
        {
            return HandleHoverEnter(motion);
        }

        if (action == MotionEventActions.HoverExit)
        {
            return HandleHoverExit(motion);
        }

        return HandleHoverMove(motion);
    }

    private bool HandleHoverEnter(MotionEvent motion)
    {
        var pointerId = (ulong)motion.GetPointerId(0);
        var x = motion.GetX(0);
        var y = motion.GetY(0);

        lock (_pointerLock)
        {
            _pointerStates[pointerId] = new PointerState(x, y);
        }

        var args = new CompositionPointerEventArgs(
            PointerEventType.Enter,
            ToDeviceKind(motion, 0),
            InputModifiers.None,
            PointerButton.None,
            pointerId,
            x,
            y,
            0,
            0,
            0,
            0,
            ToTimestamp(motion));

        return ForwardPointerEvent(args);
    }

    private bool HandleHoverMove(MotionEvent motion)
    {
        var pointerId = (ulong)motion.GetPointerId(0);
        var x = motion.GetX(0);
        var y = motion.GetY(0);
        float deltaX;
        float deltaY;

        lock (_pointerLock)
        {
            if (_pointerStates.TryGetValue(pointerId, out var state))
            {
                deltaX = x - state.X;
                deltaY = y - state.Y;
                state.X = x;
                state.Y = y;
            }
            else
            {
                _pointerStates[pointerId] = new PointerState(x, y);
                deltaX = 0;
                deltaY = 0;
            }
        }

        var args = new CompositionPointerEventArgs(
            PointerEventType.Move,
            ToDeviceKind(motion, 0),
            InputModifiers.None,
            PointerButton.None,
            pointerId,
            x,
            y,
            deltaX,
            deltaY,
            0,
            0,
            ToTimestamp(motion));

        return ForwardPointerEvent(args);
    }

    private bool HandleHoverExit(MotionEvent motion)
    {
        var pointerId = (ulong)motion.GetPointerId(0);
        float x;
        float y;

        lock (_pointerLock)
        {
            if (_pointerStates.TryGetValue(pointerId, out var state))
            {
                x = state.X;
                y = state.Y;
            }
            else
            {
                x = motion.GetX(0);
                y = motion.GetY(0);
            }

            _pointerStates.Remove(pointerId);
        }

        var args = new CompositionPointerEventArgs(
            PointerEventType.Leave,
            ToDeviceKind(motion, 0),
            InputModifiers.None,
            PointerButton.None,
            pointerId,
            x,
            y,
            0,
            0,
            0,
            0,
            ToTimestamp(motion));

        return ForwardPointerEvent(args);
    }

    private bool HandleScroll(MotionEvent motion)
    {
        var pointerId = motion.PointerCount > 0 ? (ulong)motion.GetPointerId(0) : 0UL;
        var x = motion.PointerCount > 0 ? motion.GetX(0) : 0f;
        var y = motion.PointerCount > 0 ? motion.GetY(0) : 0f;
        var wheelX = motion.GetAxisValue(Axis.Hscroll);
        var wheelY = -motion.GetAxisValue(Axis.Vscroll);

        var args = new CompositionPointerEventArgs(
            PointerEventType.Wheel,
            PointerDeviceKind.Mouse,
            ToModifiers(motion),
            PointerButton.None,
            pointerId,
            x,
            y,
            0,
            0,
            wheelX,
            wheelY,
            ToTimestamp(motion));

        return ForwardPointerEvent(args);
    }

    private static InputModifiers ToModifiers(MotionEvent motion)
    {
        var modifiers = InputModifiers.None;
        var meta = (MetaKeyStates)motion.MetaState;

        if (meta.HasFlag(MetaKeyStates.ShiftOn))
        {
            modifiers |= InputModifiers.Shift;
        }
        if (meta.HasFlag(MetaKeyStates.CtrlOn))
        {
            modifiers |= InputModifiers.Control;
        }
        if (meta.HasFlag(MetaKeyStates.AltOn))
        {
            modifiers |= InputModifiers.Alt;
        }
        if (meta.HasFlag(MetaKeyStates.MetaOn))
        {
            modifiers |= InputModifiers.Meta;
        }

        var buttons = OperatingSystem.IsAndroidVersionAtLeast(23)
            ? (MotionEventButtonState)motion.ActionButton
            : (MotionEventButtonState)motion.ButtonState;
        if (buttons.HasFlag(MotionEventButtonState.Primary))
        {
            modifiers |= InputModifiers.PrimaryButton;
        }
        if (buttons.HasFlag(MotionEventButtonState.Secondary))
        {
            modifiers |= InputModifiers.SecondaryButton;
        }
        if (buttons.HasFlag(MotionEventButtonState.Tertiary))
        {
            modifiers |= InputModifiers.MiddleButton;
        }
        if (buttons.HasFlag(MotionEventButtonState.Back))
        {
            modifiers |= InputModifiers.XButton1;
        }
        if (buttons.HasFlag(MotionEventButtonState.Forward))
        {
            modifiers |= InputModifiers.XButton2;
        }

        return modifiers;
    }

    private static PointerDeviceKind ToDeviceKind(MotionEvent motion, int pointerIndex)
    {
        var toolType = motion.GetToolType(pointerIndex);
        return toolType switch
        {
            MotionEventToolType.Finger => PointerDeviceKind.Touch,
            MotionEventToolType.Stylus => PointerDeviceKind.Pen,
            MotionEventToolType.Mouse => PointerDeviceKind.Mouse,
            MotionEventToolType.Unknown => PointerDeviceKind.Unknown,
            MotionEventToolType.Eraser => PointerDeviceKind.Pen,
            _ => PointerDeviceKind.Unknown,
        };
    }

    private static PointerButton ToPointerButton(MotionEvent motion, int pointerIndex, bool isDown)
    {
        var buttonState = OperatingSystem.IsAndroidVersionAtLeast(23)
            ? (MotionEventButtonState)motion.ActionButton
            : (MotionEventButtonState)motion.ButtonState;
        if (buttonState == 0 && isDown && motion.GetToolType(pointerIndex) == MotionEventToolType.Finger)
        {
            return PointerButton.Primary;
        }

        if (buttonState.HasFlag(MotionEventButtonState.Primary))
        {
            return PointerButton.Primary;
        }
        if (buttonState.HasFlag(MotionEventButtonState.Secondary))
        {
            return PointerButton.Secondary;
        }
        if (buttonState.HasFlag(MotionEventButtonState.Tertiary))
        {
            return PointerButton.Middle;
        }
        if (buttonState.HasFlag(MotionEventButtonState.Back))
        {
            return PointerButton.XButton1;
        }
        if (buttonState.HasFlag(MotionEventButtonState.Forward))
        {
            return PointerButton.XButton2;
        }

        return PointerButton.None;
    }

    private static TimeSpan ToTimestamp(MotionEvent motion)
        => TimeSpan.FromMilliseconds(motion.EventTime);
}
#endif
