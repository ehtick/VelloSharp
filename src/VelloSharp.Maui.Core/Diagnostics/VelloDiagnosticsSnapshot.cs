namespace VelloSharp.Maui.Diagnostics;

/// <summary>
/// Immutable snapshot of diagnostics information surfaced by a presenter.
/// </summary>
using System.Collections.Generic;

public sealed record class VelloDiagnosticsSnapshot(
    double FramesPerSecond,
    long SwapChainResets,
    long KeyedMutexContention,
    string? LastError,
    IReadOnlyDictionary<string, string>? ExtendedProperties = null);
