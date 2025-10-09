using System;

namespace VelloSharp.Composition.Accessibility;

public sealed class AccessibilityChangedEventArgs : EventArgs
{
    public AccessibilityChangedEventArgs(string propertyName)
    {
        PropertyName = propertyName;
    }

    public string PropertyName { get; }
}
