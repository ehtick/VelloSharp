using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VelloSharp.Composition.Telemetry;

public sealed class TelemetryHub
{
    private readonly ConcurrentDictionary<string, TelemetryTopic> _topics = new(StringComparer.OrdinalIgnoreCase);

    public IDisposable Subscribe(string signalId, ITelemetryObserver observer)
    {
        ArgumentNullException.ThrowIfNull(signalId);
        ArgumentNullException.ThrowIfNull(observer);

        var topic = _topics.GetOrAdd(signalId, static _ => new TelemetryTopic());
        topic.Add(observer);
        return new Subscription(signalId, observer, this);
    }

    public void Publish(string signalId, TelemetrySample sample)
    {
        ArgumentNullException.ThrowIfNull(signalId);
        if (_topics.TryGetValue(signalId, out var topic))
        {
            topic.Notify(sample);
        }
    }

    public ValueTask PublishAsync(string signalId, TelemetrySample sample, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signalId);
        if (!_topics.TryGetValue(signalId, out var topic))
        {
            return ValueTask.CompletedTask;
        }

        return topic.NotifyAsync(sample, cancellationToken);
    }

    public void Complete(string signalId)
    {
        ArgumentNullException.ThrowIfNull(signalId);
        if (_topics.TryGetValue(signalId, out var topic))
        {
            topic.Complete(signalId);
        }
    }

    public void Fault(string signalId, Exception error)
    {
        ArgumentNullException.ThrowIfNull(signalId);
        ArgumentNullException.ThrowIfNull(error);

        if (_topics.TryGetValue(signalId, out var topic))
        {
            topic.Fault(signalId, error);
        }
    }

    private void Remove(string signalId, ITelemetryObserver observer)
    {
        if (_topics.TryGetValue(signalId, out var topic))
        {
            topic.Remove(observer);
            if (topic.IsEmpty)
            {
                _topics.TryRemove(signalId, out _);
            }
        }
    }

    private sealed class TelemetryTopic
    {
        private readonly ConcurrentDictionary<ITelemetryObserver, byte> _observers = new();

        public bool IsEmpty => _observers.IsEmpty;

        public void Add(ITelemetryObserver observer) => _observers.TryAdd(observer, 0);

        public void Remove(ITelemetryObserver observer) => _observers.TryRemove(observer, out _);

        public void Notify(in TelemetrySample sample)
        {
            foreach (var observer in _observers.Keys)
            {
                observer.OnTelemetry(sample);
            }
        }

        public async ValueTask NotifyAsync(TelemetrySample sample, CancellationToken cancellationToken)
        {
            foreach (var observer in _observers.Keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Run(() => observer.OnTelemetry(sample), cancellationToken).ConfigureAwait(false);
            }
        }

        public void Complete(string signalId)
        {
            foreach (var observer in _observers.Keys)
            {
                observer.OnCompleted(signalId);
            }
        }

        public void Fault(string signalId, Exception error)
        {
            foreach (var observer in _observers.Keys)
            {
                observer.OnError(signalId, error);
            }
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly string _signalId;
        private readonly TelemetryHub _hub;
        private ITelemetryObserver? _observer;

        public Subscription(string signalId, ITelemetryObserver observer, TelemetryHub hub)
        {
            _signalId = signalId;
            _observer = observer;
            _hub = hub;
        }

        public void Dispose()
        {
            var observer = Interlocked.Exchange(ref _observer, null);
            if (observer is not null)
            {
                _hub.Remove(_signalId, observer);
            }
        }
    }
}
