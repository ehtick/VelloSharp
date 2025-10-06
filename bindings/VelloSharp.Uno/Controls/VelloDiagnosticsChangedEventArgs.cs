#if HAS_UNO

using System;
using VelloSharp.Windows;

namespace VelloSharp.Uno.Controls;

public sealed class VelloDiagnosticsChangedEventArgs : EventArgs
{
    public VelloDiagnosticsChangedEventArgs(WindowsGpuDiagnostics diagnostics)
    {
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public WindowsGpuDiagnostics Diagnostics { get; }
}

#endif
