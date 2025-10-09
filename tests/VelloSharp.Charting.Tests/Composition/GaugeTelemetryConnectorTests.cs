using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VelloSharp.Composition.Input;
using VelloSharp.Composition.Telemetry;
using Xunit;

namespace VelloSharp.Charting.Tests.Composition;

public sealed class GaugeTelemetryConnectorTests
{
    [Fact]
    public void ForwardsTelemetryToConsumer()
    {
        var hub = new TelemetryHub();
        var broker = new CommandBroker();
        var connector = new GaugeTelemetryConnector(hub, broker);
        var consumer = new TestGaugeConsumer();

        using var handle = connector.Register("gauge-signal", "gauge-target", consumer);

        var sample = new TelemetrySample(DateTime.UtcNow, 42.5, TelemetryQuality.Good, "°C", null);
        hub.Publish("gauge-signal", sample);

        Assert.NotNull(consumer.LastSample);
        Assert.Equal(sample.Value, consumer.LastSample!.Value.Value);
        Assert.Equal(sample.Quality, consumer.LastSample!.Value.Quality);
    }

    [Fact]
    public async Task RoutesCommandsThroughCommandBroker()
    {
        var hub = new TelemetryHub();
        var broker = new CommandBroker();
        var connector = new GaugeTelemetryConnector(hub, broker);
        var consumer = new TestGaugeConsumer();

        using var handle = connector.Register("pressure", "pressure-target", consumer);

        var request = new CommandRequest("pressure-target", "ack", new Dictionary<string, object?>(), DateTime.UtcNow, InputModifiers.None);
        var result = await broker.SendAsync(request);

        Assert.True(consumer.CommandHandled);
        Assert.Equal(CommandStatus.Accepted, result.Status);
    }

    private sealed class TestGaugeConsumer : IGaugeTelemetryConsumer
    {
        public TelemetrySample? LastSample { get; private set; }

        public bool CommandHandled { get; private set; }

        public void OnTelemetry(in TelemetrySample sample)
        {
            LastSample = sample;
        }

        public ValueTask<CommandResult> HandleCommandAsync(CommandRequest request, CancellationToken cancellationToken)
        {
            CommandHandled = true;
            return ValueTask.FromResult(CommandResult.Accepted("ok"));
        }
    }
}
