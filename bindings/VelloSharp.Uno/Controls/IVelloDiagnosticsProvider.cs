#if HAS_UNO

using System;
using VelloSharp.Windows;

namespace VelloSharp.Uno.Controls;

public interface IVelloDiagnosticsProvider
{
    WindowsGpuDiagnostics Diagnostics { get; }

    event EventHandler<VelloDiagnosticsChangedEventArgs> DiagnosticsUpdated;
}

#endif
