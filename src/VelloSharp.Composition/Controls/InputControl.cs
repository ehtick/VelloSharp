using System;
using System.Collections.Generic;
using System.ComponentModel;
using VelloSharp.Composition.Accessibility;
using VelloSharp.Composition.Input;

namespace VelloSharp.Composition.Controls;

public class InputControl : TemplatedControl, ICompositionInputSink
{
    private readonly HashSet<ulong> _capturedPointers = new();
    private ICompositionInputSource? _inputSource;

    public InputControl()
    {
        Accessibility = new AccessibilityProperties();
        Accessibility.PropertyChanged += OnAccessibilityPropertyChanged;
    }

    public bool IsPointerOver { get; private set; }

    public bool IsFocused { get; private set; }

    public AccessibilityProperties Accessibility { get; }

    public event EventHandler<CompositionPointerEventArgs>? PointerEntered;
    public event EventHandler<CompositionPointerEventArgs>? PointerMoved;
    public event EventHandler<CompositionPointerEventArgs>? PointerExited;
    public event EventHandler<CompositionPointerEventArgs>? PointerPressed;
    public event EventHandler<CompositionPointerEventArgs>? PointerReleased;
    public event EventHandler<CompositionPointerEventArgs>? PointerWheelChanged;
    public event EventHandler<CompositionPointerEventArgs>? PointerCanceled;
    public event EventHandler<CompositionPointerEventArgs>? PointerCaptureLost;

    public event EventHandler<CompositionKeyEventArgs>? KeyDown;
    public event EventHandler<CompositionKeyEventArgs>? KeyUp;

    public event EventHandler<CompositionTextInputEventArgs>? TextInput;

    public event EventHandler? GotFocus;
    public event EventHandler? LostFocus;

    public event EventHandler<AccessibilityChangedEventArgs>? AccessibilityChanged;
    public event EventHandler<AccessibilityAnnouncementEventArgs>? AccessibilityAnnouncementRequested;
    public event EventHandler<AccessibilityActionEventArgs>? AccessibilityActionInvoked;

    public IReadOnlyCollection<ulong> CapturedPointers => _capturedPointers;

    public void AttachInputSource(ICompositionInputSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (ReferenceEquals(_inputSource, source))
        {
            return;
        }

        _inputSource?.Disconnect(this);
        _inputSource = source;
        _inputSource.Connect(this);
    }

    public void DetachInputSource()
    {
        if (_inputSource is null)
        {
            return;
        }

        _inputSource.Disconnect(this);
        _inputSource = null;
        _capturedPointers.Clear();
        IsPointerOver = false;
        IsFocused = false;
    }

    public void CapturePointer(ulong pointerId)
    {
        _inputSource?.RequestPointerCapture(this, pointerId);
    }

    public void ReleasePointer(ulong pointerId)
    {
        _inputSource?.ReleasePointerCapture(this, pointerId);
    }

    public void RequestFocus()
    {
        _inputSource?.RequestFocus(this);
    }

    public void AnnounceAccessibility(string message, AccessibilityLiveSetting liveSetting = AccessibilityLiveSetting.Polite)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        AccessibilityAnnouncementRequested?.Invoke(this, new AccessibilityAnnouncementEventArgs(message, liveSetting));
    }

    public void HandleAccessibilityAction(AccessibilityAction action)
    {
        OnAccessibilityActionInvoked(action);
    }

    protected virtual void OnPointerEntered(CompositionPointerEventArgs args) =>
        PointerEntered?.Invoke(this, args);

    protected virtual void OnPointerMoved(CompositionPointerEventArgs args) =>
        PointerMoved?.Invoke(this, args);

    protected virtual void OnPointerExited(CompositionPointerEventArgs args) =>
        PointerExited?.Invoke(this, args);

    protected virtual void OnPointerPressed(CompositionPointerEventArgs args) =>
        PointerPressed?.Invoke(this, args);

    protected virtual void OnPointerReleased(CompositionPointerEventArgs args) =>
        PointerReleased?.Invoke(this, args);

    protected virtual void OnPointerWheelChanged(CompositionPointerEventArgs args) =>
        PointerWheelChanged?.Invoke(this, args);

    protected virtual void OnPointerCanceled(CompositionPointerEventArgs args) =>
        PointerCanceled?.Invoke(this, args);

    protected virtual void OnPointerCaptureLost(CompositionPointerEventArgs args) =>
        PointerCaptureLost?.Invoke(this, args);

    protected virtual void OnKeyDown(CompositionKeyEventArgs args) =>
        KeyDown?.Invoke(this, args);

    protected virtual void OnKeyUp(CompositionKeyEventArgs args) =>
        KeyUp?.Invoke(this, args);

    protected virtual void OnTextInput(CompositionTextInputEventArgs args) =>
        TextInput?.Invoke(this, args);

    protected virtual void OnGotFocus(EventArgs args) =>
        GotFocus?.Invoke(this, args);

    protected virtual void OnLostFocus(EventArgs args) =>
        LostFocus?.Invoke(this, args);

    protected virtual void OnAccessibilityActionInvoked(AccessibilityAction action) =>
        AccessibilityActionInvoked?.Invoke(this, new AccessibilityActionEventArgs(action));

    private void OnAccessibilityPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        AccessibilityChanged?.Invoke(this, new AccessibilityChangedEventArgs(e.PropertyName ?? string.Empty));
    }

    void ICompositionInputSink.ProcessPointerEvent(CompositionPointerEventArgs args)
    {
        switch (args.EventType)
        {
            case PointerEventType.Enter:
                IsPointerOver = true;
                OnPointerEntered(args);
                break;
            case PointerEventType.Move:
                OnPointerMoved(args);
                break;
            case PointerEventType.Leave:
                IsPointerOver = false;
                OnPointerExited(args);
                break;
            case PointerEventType.Down:
                _capturedPointers.Add(args.PointerId);
                OnPointerPressed(args);
                break;
            case PointerEventType.Up:
                _capturedPointers.Remove(args.PointerId);
                OnPointerReleased(args);
                break;
            case PointerEventType.Cancel:
                _capturedPointers.Remove(args.PointerId);
                OnPointerCanceled(args);
                break;
            case PointerEventType.Wheel:
                OnPointerWheelChanged(args);
                break;
            case PointerEventType.CaptureLost:
                _capturedPointers.Remove(args.PointerId);
                OnPointerCaptureLost(args);
                break;
        }
    }

    void ICompositionInputSink.ProcessKeyEvent(CompositionKeyEventArgs args)
    {
        if (args is null || !IsFocused)
        {
            return;
        }

        switch (args.EventType)
        {
            case KeyEventType.Down:
                OnKeyDown(args);
                break;
            case KeyEventType.Up:
                OnKeyUp(args);
                break;
        }
    }

    void ICompositionInputSink.ProcessTextInput(CompositionTextInputEventArgs args)
    {
        if (!IsFocused || args is null)
        {
            return;
        }

        OnTextInput(args);
    }

    void ICompositionInputSink.ProcessFocusChanged(bool isFocused)
    {
        if (IsFocused == isFocused)
        {
            return;
        }

        IsFocused = isFocused;
        if (isFocused)
        {
            OnGotFocus(EventArgs.Empty);
        }
        else
        {
            OnLostFocus(EventArgs.Empty);
        }
    }

    public override void Unmount()
    {
        DetachInputSource();
        base.Unmount();
    }
}
