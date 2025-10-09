using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VelloSharp.Composition.Input;
using VelloSharp.Composition.Telemetry;
using Xunit;

namespace VelloSharp.Charting.Tests.Composition;

public sealed class ScadaTelemetryRouterTests
{
    [Fact]
    public void RouterReplaysLastSampleOnSubscribe()
    {
        var hub = new TelemetryHub();
        var broker = new CommandBroker();
        var router = new ScadaTelemetryRouter(hub, broker);

        var sample = new TelemetrySample(DateTime.UtcNow, 12.3, TelemetryQuality.Uncertain, "bar", null);
        router.Publish("scada.flow", sample);

        var observer = new RecordingObserver();
        using var _ = router.Subscribe("scada.flow", observer, replayLastSample: true);

        Assert.Single(observer.Samples);
        Assert.Equal(sample.Value, observer.Samples[0].Value);
        Assert.Equal(sample.Quality, observer.Samples[0].Quality);
    }

    [Fact]
    public async Task RouterRoutesCommands()
    {
        var hub = new TelemetryHub();
        var broker = new CommandBroker();
        var router = new ScadaTelemetryRouter(hub, broker);

        var handler = new InlineCommandHandler(CommandResult.Accepted("acknowledged"));
        using var registration = router.RegisterCommandHandler("scada.command", handler);

        var request = new CommandRequest("scada.command", "override", new Dictionary<string, object?>(), DateTime.UtcNow, InputModifiers.None);
        var result = await router.SendCommandAsync(request);

        Assert.Equal(CommandStatus.Accepted, result.Status);
        Assert.True(handler.Called);
    }

    private sealed class RecordingObserver : ITelemetryObserver
    {
        public List<TelemetrySample> Samples { get; } = new();

        public void OnTelemetry(in TelemetrySample sample) => Samples.Add(sample);

        public void OnError(string signalId, Exception error)
        {
        }

        public void OnCompleted(string signalId)
        {
        }
    }

    private sealed class InlineCommandHandler : ICommandHandler, IDisposable
    {
        private readonly CommandResult _result;
        public bool Called { get; private set; }

        public InlineCommandHandler(CommandResult result)
        {
            _result = result;
        }

        public ValueTask<CommandResult> HandleAsync(CommandRequest request, CancellationToken cancellationToken)
        {
            Called = true;
            return ValueTask.FromResult(_result);
        }

        public void Dispose()
        {
        }
    }
}
