using System.Threading;
using System.Threading.Tasks;

namespace VelloSharp.Composition.Telemetry;

public interface IGaugeTelemetryConsumer
{
    void OnTelemetry(in TelemetrySample sample);

    ValueTask<CommandResult> HandleCommandAsync(CommandRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(CommandResult.NotFound("Gauge command handling not implemented."));
}
