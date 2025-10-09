using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace VelloSharp.Composition.Telemetry;

public sealed class ScadaTelemetryRouter : IDisposable
{
    private readonly TelemetryHub _hub;
    private readonly CommandBroker _broker;
    private readonly ConcurrentDictionary<string, RouterSubscription> _subscriptions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TelemetrySample> _latestSamples = new(StringComparer.OrdinalIgnoreCase);

    public ScadaTelemetryRouter(TelemetryHub hub, CommandBroker broker)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
    }

    public IDisposable Subscribe(string signalId, ITelemetryObserver observer, bool replayLastSample = true)
    {
        ArgumentNullException.ThrowIfNull(signalId);
        ArgumentNullException.ThrowIfNull(observer);

        if (replayLastSample && _latestSamples.TryGetValue(signalId, out var sample))
        {
            observer.OnTelemetry(sample);
        }

        var subscription = _hub.Subscribe(signalId, new MirroredObserver(signalId, observer, this));
        var routerSubscription = new RouterSubscription(signalId, subscription, this);
        if (!_subscriptions.TryAdd(signalId, routerSubscription))
        {
            subscription.Dispose();
            throw new InvalidOperationException($"A subscription for '{signalId}' is already active on the router.");
        }

        return routerSubscription;
    }

    public void Publish(string signalId, TelemetrySample sample)
    {
        ArgumentNullException.ThrowIfNull(signalId);
        RecordSample(signalId, sample);
        _hub.Publish(signalId, sample);
    }

    public ValueTask PublishAsync(string signalId, TelemetrySample sample, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signalId);
        RecordSample(signalId, sample);
        return _hub.PublishAsync(signalId, sample, cancellationToken);
    }

    public ValueTask<CommandResult> SendCommandAsync(CommandRequest request, CancellationToken cancellationToken = default) =>
        _broker.SendAsync(request, cancellationToken);

    public IDisposable RegisterCommandHandler(string targetId, ICommandHandler handler) =>
        _broker.Register(targetId, handler);

    internal void RecordSample(string signalId, TelemetrySample sample)
    {
        _latestSamples[signalId] = sample;
    }

    internal void RemoveSubscription(string signalId)
    {
        _subscriptions.TryRemove(signalId, out _);
    }

    public void Dispose()
    {
        foreach (var subscription in _subscriptions.Values)
        {
            subscription.Dispose();
        }

        _subscriptions.Clear();
    }

    private sealed class MirroredObserver : ITelemetryObserver
    {
        private readonly string _signalId;
        private readonly ITelemetryObserver _inner;
        private readonly ScadaTelemetryRouter _router;

        public MirroredObserver(string signalId, ITelemetryObserver inner, ScadaTelemetryRouter router)
        {
            _signalId = signalId;
            _inner = inner;
            _router = router;
        }

        public void OnTelemetry(in TelemetrySample sample)
        {
            _router.RecordSample(_signalId, sample);
            _inner.OnTelemetry(sample);
        }

        public void OnError(string signalId, Exception error)
        {
            _inner.OnError(signalId, error);
        }

        public void OnCompleted(string signalId)
        {
            _inner.OnCompleted(signalId);
        }
    }

    private sealed class RouterSubscription : IDisposable
    {
        private readonly string _signalId;
        private readonly IDisposable _subscription;
        private readonly ScadaTelemetryRouter _router;
        private int _disposed;

        public RouterSubscription(string signalId, IDisposable subscription, ScadaTelemetryRouter router)
        {
            _signalId = signalId;
            _subscription = subscription;
            _router = router;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            _subscription.Dispose();
            _router.RemoveSubscription(_signalId);
        }
    }
}
