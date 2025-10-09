using System;

namespace VelloSharp.Composition.Input;

public sealed class CompositionKeyEventArgs : EventArgs
{
    public CompositionKeyEventArgs(KeyEventType eventType, int keyCode, InputModifiers modifiers, bool isRepeat, string? text)
    {
        EventType = eventType;
        KeyCode = keyCode;
        Modifiers = modifiers;
        IsRepeat = isRepeat;
        Text = text;
    }

    public KeyEventType EventType { get; }

    public int KeyCode { get; }

    public InputModifiers Modifiers { get; }

    public bool IsRepeat { get; }

    public string? Text { get; }

    public bool Handled { get; set; }
}
