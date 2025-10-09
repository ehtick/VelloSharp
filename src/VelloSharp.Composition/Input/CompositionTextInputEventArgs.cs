using System;

namespace VelloSharp.Composition.Input;

public sealed class CompositionTextInputEventArgs : EventArgs
{
    public CompositionTextInputEventArgs(string text)
    {
        Text = text;
    }

    public string Text { get; }

    public bool Handled { get; set; }
}
