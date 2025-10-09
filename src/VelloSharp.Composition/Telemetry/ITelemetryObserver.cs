using System;

namespace VelloSharp.Composition.Telemetry;

public interface ITelemetryObserver
{
    void OnTelemetry(in TelemetrySample sample);

    void OnError(string signalId, Exception error);

    void OnCompleted(string signalId);
}
