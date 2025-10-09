using System;
using VelloSharp.Composition.Controls;
using VelloSharp.Composition.Input;
using Xunit;

namespace VelloSharp.Charting.Tests.Composition;

public sealed class InputControlTests
{
    private static CompositionPointerEventArgs CreatePointerArgs(PointerEventType type, ulong pointerId = 1) =>
        new(
            type,
            PointerDeviceKind.Mouse,
            InputModifiers.None,
            PointerButton.Primary,
            pointerId,
            12.5,
            24.2,
            0,
            0,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public void PointerEntered_SetsPointerOver()
    {
        var control = new InputControl();
        var sink = (ICompositionInputSink)control;
        sink.ProcessPointerEvent(CreatePointerArgs(PointerEventType.Enter));

        Assert.True(control.IsPointerOver);

        sink.ProcessPointerEvent(CreatePointerArgs(PointerEventType.Leave));
        Assert.False(control.IsPointerOver);
    }

    [Fact]
    public void PointerCaptureLoss_RemovesPointer()
    {
        var control = new InputControl();
        var sink = (ICompositionInputSink)control;
        var pressed = CreatePointerArgs(PointerEventType.Down);
        sink.ProcessPointerEvent(pressed);
        Assert.Single(control.CapturedPointers);

        sink.ProcessPointerEvent(CreatePointerArgs(PointerEventType.CaptureLost));
        Assert.Empty(control.CapturedPointers);
    }

    [Fact]
    public void Focus_UpdatesState()
    {
        var control = new InputControl();
        var sink = (ICompositionInputSink)control;
        sink.ProcessFocusChanged(true);
        Assert.True(control.IsFocused);
        sink.ProcessFocusChanged(false);
        Assert.False(control.IsFocused);
    }

    [Fact]
    public void KeyEvents_FireWhenFocused()
    {
        var control = new InputControl();
        var sink = (ICompositionInputSink)control;
        sink.ProcessFocusChanged(true);

        var handled = false;
        control.KeyDown += (_, args) =>
        {
            handled = true;
            args.Handled = true;
        };

        var keyArgs = new CompositionKeyEventArgs(KeyEventType.Down, 42, InputModifiers.Control, false, null);
        sink.ProcessKeyEvent(keyArgs);

        Assert.True(handled);
        Assert.True(keyArgs.Handled);
    }

    [Fact]
    public void TextInput_IgnoredWhenNotFocused()
    {
        var control = new InputControl();
        var sink = (ICompositionInputSink)control;
        var textHandled = false;
        control.TextInput += (_, args) => textHandled = true;

        sink.ProcessTextInput(new CompositionTextInputEventArgs("test"));
        Assert.False(textHandled);

        sink.ProcessFocusChanged(true);
        sink.ProcessTextInput(new CompositionTextInputEventArgs("test"));
        Assert.True(textHandled);
    }
}