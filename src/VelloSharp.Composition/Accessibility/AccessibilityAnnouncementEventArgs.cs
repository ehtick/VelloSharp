using System;

namespace VelloSharp.Composition.Accessibility;

public sealed class AccessibilityAnnouncementEventArgs : EventArgs
{
    public AccessibilityAnnouncementEventArgs(string message, AccessibilityLiveSetting liveSetting)
    {
        Message = message;
        LiveSetting = liveSetting;
    }

    public string Message { get; }

    public AccessibilityLiveSetting LiveSetting { get; }
}
