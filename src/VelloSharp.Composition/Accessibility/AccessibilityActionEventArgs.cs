using System;

namespace VelloSharp.Composition.Accessibility;

public sealed class AccessibilityActionEventArgs : EventArgs
{
    public AccessibilityActionEventArgs(AccessibilityAction action)
    {
        Action = action;
    }

    public AccessibilityAction Action { get; }
}
