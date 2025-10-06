using System;

namespace VelloSharp.Windows;

public enum WindowsSurfaceKind
{
    Win32Hwnd,
    SwapChainPanel,
    CoreWindow,
}

public readonly struct WindowsSurfaceDescriptor
{
    public WindowsSurfaceDescriptor(WindowsSurfaceKind kind, nint primaryHandle, nint secondaryHandle = 0)
    {
        Kind = kind;
        PrimaryHandle = primaryHandle;
        SecondaryHandle = secondaryHandle;
    }

    public WindowsSurfaceKind Kind { get; }

    public nint PrimaryHandle { get; }

    public nint SecondaryHandle { get; }

    public bool IsEmpty => PrimaryHandle == 0;

    public static WindowsSurfaceDescriptor FromHwnd(nint hwnd, nint hinstance = 0)
    {
        if (hwnd == 0)
        {
            throw new ArgumentNullException(nameof(hwnd));
        }

        return new WindowsSurfaceDescriptor(WindowsSurfaceKind.Win32Hwnd, hwnd, hinstance);
    }

    public static WindowsSurfaceDescriptor FromSwapChainPanel(nint panel)
    {
        if (panel == 0)
        {
            throw new ArgumentNullException(nameof(panel));
        }

        return new WindowsSurfaceDescriptor(WindowsSurfaceKind.SwapChainPanel, panel);
    }

    public static WindowsSurfaceDescriptor FromCoreWindow(nint coreWindow)
    {
        if (coreWindow == 0)
        {
            throw new ArgumentNullException(nameof(coreWindow));
        }

        return new WindowsSurfaceDescriptor(WindowsSurfaceKind.CoreWindow, coreWindow);
    }
}
