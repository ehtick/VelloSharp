using System;
using System.Collections.Generic;
using VelloSharp.Composition.Input;

namespace VelloSharp.Composition.Telemetry;

public readonly record struct CommandRequest(
    string TargetId,
    string Command,
    IReadOnlyDictionary<string, object?> Parameters,
    DateTime TimestampUtc,
    InputModifiers Modifiers);
