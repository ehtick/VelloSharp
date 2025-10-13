using System;
using VelloSharp.Windows;

namespace VelloSharp.Windows.Shared.Diagnostics;

public sealed class VelloDiagnosticsChangedEventArgs : EventArgs
{
    public VelloDiagnosticsChangedEventArgs(WindowsGpuDiagnostics diagnostics)
    {
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public WindowsGpuDiagnostics Diagnostics { get; }
}
