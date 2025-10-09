using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace VelloSharp.Composition.Telemetry;

public sealed class CommandBroker
{
    private readonly ConcurrentDictionary<string, ICommandHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);

    public IDisposable Register(string targetId, ICommandHandler handler)
    {
        ArgumentNullException.ThrowIfNull(targetId);
        ArgumentNullException.ThrowIfNull(handler);

        if (!_handlers.TryAdd(targetId, handler))
        {
            throw new InvalidOperationException($"A handler for '{targetId}' is already registered.");
        }

        return new Registration(targetId, this);
    }

    public ValueTask<CommandResult> SendAsync(CommandRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request.TargetId);
        if (!_handlers.TryGetValue(request.TargetId, out var handler))
        {
            return ValueTask.FromResult(CommandResult.NotFound($"No handler registered for '{request.TargetId}'."));
        }

        return handler.HandleAsync(request, cancellationToken);
    }

    private void Unregister(string targetId)
    {
        _handlers.TryRemove(targetId, out _);
    }

    private sealed class Registration : IDisposable
    {
        private readonly string _targetId;
        private readonly CommandBroker _broker;
        private int _disposed;

        public Registration(string targetId, CommandBroker broker)
        {
            _targetId = targetId;
            _broker = broker;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            _broker.Unregister(_targetId);
        }
    }
}
