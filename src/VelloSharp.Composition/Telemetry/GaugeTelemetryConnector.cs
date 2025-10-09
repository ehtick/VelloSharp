using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace VelloSharp.Composition.Telemetry;

public sealed class GaugeTelemetryConnector : IDisposable
{
    private readonly TelemetryHub _hub;
    private readonly CommandBroker _broker;
    private readonly ConcurrentDictionary<string, Connection> _connections = new(StringComparer.OrdinalIgnoreCase);

    public GaugeTelemetryConnector(TelemetryHub hub, CommandBroker broker)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
    }

    public IDisposable Register(string signalId, string commandTargetId, IGaugeTelemetryConsumer consumer)
    {
        ArgumentNullException.ThrowIfNull(signalId);
        ArgumentNullException.ThrowIfNull(commandTargetId);
        ArgumentNullException.ThrowIfNull(consumer);

        var observer = new GaugeObserver(consumer);
        var subscription = _hub.Subscribe(signalId, observer);
        var registration = _broker.Register(commandTargetId, new GaugeCommandHandler(consumer));

        var connection = new Connection(signalId, subscription, registration, this);
        if (!_connections.TryAdd(signalId, connection))
        {
            subscription.Dispose();
            registration.Dispose();
            throw new InvalidOperationException($"A gauge connector is already registered for signal '{signalId}'.");
        }

        return connection;
    }

    internal void Remove(string signalId)
    {
        _connections.TryRemove(signalId, out _);
    }

    public void Dispose()
    {
        foreach (var connection in _connections.Values)
        {
            connection.Dispose();
        }

        _connections.Clear();
    }

    private sealed class GaugeObserver : ITelemetryObserver
    {
        private readonly IGaugeTelemetryConsumer _consumer;

        public GaugeObserver(IGaugeTelemetryConsumer consumer)
        {
            _consumer = consumer;
        }

        public void OnTelemetry(in TelemetrySample sample) => _consumer.OnTelemetry(sample);

        public void OnError(string signalId, Exception error)
        {
        }

        public void OnCompleted(string signalId)
        {
        }
    }

    private sealed class GaugeCommandHandler : ICommandHandler
    {
        private readonly IGaugeTelemetryConsumer _consumer;

        public GaugeCommandHandler(IGaugeTelemetryConsumer consumer)
        {
            _consumer = consumer;
        }

        public ValueTask<CommandResult> HandleAsync(CommandRequest request, CancellationToken cancellationToken) =>
            _consumer.HandleCommandAsync(request, cancellationToken);
    }

    private sealed class Connection : IDisposable
    {
        private readonly string _signalId;
        private readonly IDisposable _subscription;
        private readonly IDisposable _registration;
        private readonly GaugeTelemetryConnector _owner;
        private int _disposed;

        public Connection(string signalId, IDisposable subscription, IDisposable registration, GaugeTelemetryConnector owner)
        {
            _signalId = signalId;
            _subscription = subscription;
            _registration = registration;
            _owner = owner;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            _subscription.Dispose();
            _registration.Dispose();
            _owner.Remove(_signalId);
        }
    }
}
