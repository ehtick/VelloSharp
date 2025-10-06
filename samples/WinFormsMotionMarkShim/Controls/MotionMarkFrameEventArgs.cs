using System;

namespace WinFormsMotionMarkShim.Controls;

internal sealed class MotionMarkFrameEventArgs : EventArgs
{
    public MotionMarkFrameEventArgs(TimeSpan delta, bool isAnimationFrame, int elementTarget, bool isFastPathActive)
    {
        Delta = delta;
        IsAnimationFrame = isAnimationFrame;
        ElementTarget = elementTarget;
        IsFastPathActive = isFastPathActive;
    }

    public TimeSpan Delta { get; }

    public bool IsAnimationFrame { get; }

    public int ElementTarget { get; }

    public bool IsFastPathActive { get; }
}
