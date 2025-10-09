# Shared Telemetry & Command Contract

## Goals
- Provide a unified telemetry fan-out and command routing surface for charts, TreeDataGrid, gauges, editor, and SCADA runtimes.
- Guarantee consistent quality metadata, cancellation semantics, and acknowledgement flows across managed consumers and native bridges.
- Enable partner connectors (OPC UA, MQTT, REST, historian replay) to integrate once while serving all dashboards.

## Managed Surface (`VelloSharp.Composition.Telemetry`)
- `TelemetryHub`
  - Thread-safe publish/subscribe hub keyed by `signalId` (case-insensitive).
  - `IDisposable Subscribe(string signalId, ITelemetryObserver observer)` – registers an observer.
  - `void Publish(string signalId, TelemetrySample sample)` – synchronous fan-out.
  - `ValueTask PublishAsync(string signalId, TelemetrySample sample, CancellationToken token = default)` – cancellation-aware fan-out for bursty streams.
  - `void Complete(string signalId)` / `void Fault(string signalId, Exception error)` – lifecycle notifications broadcast to observers.
- `TelemetrySample`
  - Fields: `DateTime TimestampUtc`, `double Value`, `TelemetryQuality Quality`, `string? Unit`, `IReadOnlyDictionary<string, double>? Dimensions`.
  - Consumers must treat `TimestampUtc` as monotonic; histogram/aggregation layers are responsible for drift correction.
- `TelemetryQuality`
  - Values: `Unknown`, `Good`, `Uncertain`, `Bad`.
  - Gauges/SCADA must surface quality in UI (colour, icons); charts may downgrade visuals when quality != `Good`.
- `ITelemetryObserver`
  - Callbacks: `OnTelemetry(in TelemetrySample sample)`, `OnError(string signalId, Exception error)`, `OnCompleted(string signalId)`.
  - Observers are expected to be lightweight; expensive processing should schedule work off-thread.

### Command Routing
- `CommandBroker`
  - `IDisposable Register(string targetId, ICommandHandler handler)` – enforces single handler per `targetId` (equipment, dashboard surface, etc.).
  - `ValueTask<CommandResult> SendAsync(CommandRequest request, CancellationToken token = default)` – routes commands to handlers, returning `CommandResult.NotFound` when unmatched.
- `CommandRequest`
  - Fields: `string TargetId`, `string Command`, `IReadOnlyDictionary<string, object?> Parameters`, `DateTime TimestampUtc`, `InputModifiers Modifiers`.
  - Parameters are read-only; handlers should copy mutable values.
- `CommandResult`
  - Fields: `CommandStatus Status`, `string Message`, `DateTime TimestampUtc` with convenience factories (`Accepted`, `Rejected`, `Failed`, `Pending`, `NotFound`).
  - Status values map to SCADA annunciation semantics (Accepted = acked, Pending = queued, Failed/Rejected = operator follow-up).
- `CommandStatus` enum – `Accepted`, `Rejected`, `Failed`, `Pending`, `NotFound`.
- `ICommandHandler`
  - `ValueTask<CommandResult> HandleAsync(CommandRequest request, CancellationToken cancellationToken)`.
  - Handlers must honour cancellation and propagate domain-specific error messages.

## Threading & Performance
- `TelemetryHub.Publish` executes observers inline; heavy consumers should subscribe with lightweight shims and push work to background pipelines.
- `TelemetryHub.PublishAsync` schedules observer invocations via `Task.Run` to avoid blocking producers when performing longer-running work.
- `CommandBroker` is primarily request/response; handlers may return `CommandResult.Pending` to indicate asynchronous completion managed elsewhere.

## Error Handling & Lifecycle
- Observers receiving `OnError` should surface the failure (UI banner, logs) and decide whether to re-subscribe.
- `Dispose()` returned by `Subscribe`/`Register` must be called when dashboards tear down to avoid dangling handlers.
- Implementations are resilient to concurrent subscribe/unsubscribe and publish operations.

## Connector Patterns
- **`Telemetry.GaugeTelemetryConnector`**: wraps `TelemetryHub` + `CommandBroker` to subscribe gauges to signal ids while enforcing single handler per command target. Consumers call `Register` with a signal id/command id pair and receive telemetry via `IGaugeTelemetryConsumer` while returning `CommandResult` from `HandleCommandAsync`.
- **`Telemetry.ScadaTelemetryRouter`**: aggregates last-known samples per signal, replays to new observers, and offers convenience helpers (`Publish`, `PublishAsync`, `SendCommandAsync`, `RegisterCommandHandler`) for SCADA orchestration layers. Subscriptions mirror telemetry back into the router to keep cached values fresh.

## Integration Expectations
- Charts: telemetry feeds for price, volume, overlays should publish via `TelemetryHub` and register command handlers for chart-level interactions (e.g., alert override).
- TreeDataGrid: data providers expose row metrics via hub; command broker handles inline edits/acknowledgements.
- Gauges: connectors push signal updates through hub and use command broker for setpoint changes, alarm acknowledgements.
- Editor: design-time preview consumes hub samples and routes apply/reset commands via broker.
- SCADA runtime: acts as orchestrator, bridging plant/historian feeds into hub and delegating operator commands.

## Testing & Diagnostics
- `TelemetryHubTests` in `tests/VelloSharp.Charting.Tests` validate fan-out, cancellation, and command routing behaviour.
- Consumers must add integration tests covering signal quality propagation and command acknowledgement flows before marking plan milestones complete.

