namespace VelloSharp.Windows;

public interface IWindowsSurfaceSource
{
    WindowsSurfaceDescriptor GetSurfaceDescriptor();

    WindowsSurfaceSize GetSurfaceSize();

    string? DiagnosticsLabel => null;

    void OnSwapChainCreated(WindowsSwapChainSurface surface) { }

    void OnSwapChainResized(WindowsSwapChainSurface surface, WindowsSurfaceSize size) { }

    void OnSwapChainDestroyed() { }

    void OnDeviceLost(string? reason) { }
}
