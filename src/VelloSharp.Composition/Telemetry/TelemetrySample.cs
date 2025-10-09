using System;
using System.Collections.Generic;

namespace VelloSharp.Composition.Telemetry;

public readonly record struct TelemetrySample(
    DateTime TimestampUtc,
    double Value,
    TelemetryQuality Quality,
    string? Unit,
    IReadOnlyDictionary<string, double>? Dimensions);

