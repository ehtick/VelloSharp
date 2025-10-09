using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VelloSharp.Composition.Input;
using VelloSharp.Composition.Telemetry;
using Xunit;

namespace VelloSharp.Charting.Tests.Composition;

public sealed class TelemetryHubTests
{
    private sealed class TestObserver : ITelemetryObserver
    {
        public List<TelemetrySample> Samples { get; } = new();
        public bool Completed { get; private set; }
        public Exception? Error { get; private set; }

        public void OnTelemetry(in TelemetrySample sample) => Samples.Add(sample);

        public void OnError(string signalId, Exception error) => Error = error;

        public void OnCompleted(string signalId) => Completed = true;
    }

    private sealed class EchoCommandHandler : ICommandHandler
    {
        private readonly CommandResult _result;

        public EchoCommandHandler(CommandResult result)
        {
            _result = result;
        }

        public ValueTask<CommandResult> HandleAsync(CommandRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(_result);
    }

    [Fact]
    public void Hub_Dispatches_ToSubscribers()
    {
        var hub = new TelemetryHub();
        var observer = new TestObserver();
        using var subscription = hub.Subscribe("signal", observer);

        hub.Publish("signal", new TelemetrySample(DateTime.UtcNow, 42.0, TelemetryQuality.Good, null, null));

        Assert.Single(observer.Samples);
        Assert.Equal(42.0, observer.Samples[0].Value);
    }

    [Fact]
    public async Task Hub_PublishAsync_HonoursCancellation()
    {
        var hub = new TelemetryHub();
        var observer = new TestObserver();
        using var subscription = hub.Subscribe("signal", observer);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await hub.PublishAsync("signal", new TelemetrySample(DateTime.UtcNow, 7, TelemetryQuality.Unknown, null, null), cts.Token);
        });
    }

    [Fact]
    public void CommandBroker_Routes_ToRegisteredHandler()
    {
        var broker = new CommandBroker();
        var handler = new EchoCommandHandler(CommandResult.Accepted("ok"));
        using var registration = broker.Register("pump", handler);

        var request = new CommandRequest("pump", "start", new Dictionary<string, object?>(), DateTime.UtcNow, InputModifiers.None);
        var result = broker.SendAsync(request).GetAwaiter().GetResult();

        Assert.Equal(CommandStatus.Accepted, result.Status);
    }

    [Fact]
    public async Task CommandBroker_Returns_NotFound_WhenMissing()
    {
        var broker = new CommandBroker();
        var request = new CommandRequest("missing", "reset", new Dictionary<string, object?>(), DateTime.UtcNow, InputModifiers.None);
        var result = await broker.SendAsync(request);

        Assert.Equal(CommandStatus.NotFound, result.Status);
    }
}
