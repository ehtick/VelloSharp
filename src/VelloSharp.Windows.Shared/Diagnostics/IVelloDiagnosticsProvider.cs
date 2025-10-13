using System;
using VelloSharp.Windows;

namespace VelloSharp.Windows.Shared.Diagnostics;

public interface IVelloDiagnosticsProvider
{
    WindowsGpuDiagnostics Diagnostics { get; }

    event EventHandler<VelloDiagnosticsChangedEventArgs> DiagnosticsUpdated;
}
