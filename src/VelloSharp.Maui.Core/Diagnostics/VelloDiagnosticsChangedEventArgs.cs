using System;

namespace VelloSharp.Maui.Diagnostics;

/// <summary>
/// Event arguments raised when diagnostics information changes.
/// </summary>
public sealed class VelloDiagnosticsChangedEventArgs : EventArgs
{
    public VelloDiagnosticsChangedEventArgs(VelloDiagnosticsSnapshot snapshot)
    {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public VelloDiagnosticsSnapshot Snapshot { get; }
}
