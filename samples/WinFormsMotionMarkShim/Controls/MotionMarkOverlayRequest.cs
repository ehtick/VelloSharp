namespace WinFormsMotionMarkShim.Controls;

internal readonly struct MotionMarkOverlayRequest
{
    public MotionMarkOverlayRequest(int elementTarget, bool isFastPathActive)
    {
        ElementTarget = elementTarget;
        IsFastPathActive = isFastPathActive;
    }

    public int ElementTarget { get; }

    public bool IsFastPathActive { get; }
}
