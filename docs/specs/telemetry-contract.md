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

## Gauge Addendum
- Gauges subscribe through `GaugeTelemetryConnector`, binding telemetry `signalId` to gauge instance identifiers; connector enforces single writer per signal and exposes quality-driven styling callbacks.
- Telemetry samples should include `Unit` (`psi`, `gpm`, `rpm`) and optional `Dimensions` entries such as `{"setpoint": 75.0, "high_alarm": 95.0}` so gauges can surface target bands without additional lookups.
- Command requests for `setpoint`, `mode`, and `acknowledge` must include operator metadata in `Parameters` (`{ "operator": "badge1234" }`) to satisfy industrial audit trails.
- Gauges publish `CommandResult.Pending` for long running operations (e.g., remote valve actuation) and update to `Accepted` or `Failed` once feedback arrives; UI layers use this to drive annunciator states.
- `TelemetryHub` should coalesce duplicate samples arriving within the same frame for the same `signalId` to preserve the <8 ms budget on high rate analog inputs.
- Last-known sample caching is required for annunciators rendering stale indicators; `GaugeTelemetryConnector` exposes helpers to request cached samples during initialization.

## SCADA Addendum
- `ScadaTelemetryRouter` extends `TelemetryHub` to provide historian-aware caching, stale detection, and replay notifications; SCADA dashboards must subscribe through the router to ensure synchronized chart/gauge/TDG updates.
- Telemetry samples published for SCADA contexts must embed `Dimensions` entries for alarm thresholds, engineering units, setpoints, and mode descriptors to avoid cross-service lookups.
- Historian playback is modelled as a virtual telemetry channel; replay controllers publish `Quality = TelemetryQuality::Uncertain` with a `Dimensions["replay"] = 1` flag to let widgets decorate historical data distinct from live feeds.
- Commands issued via `CommandBroker` require operator attribution (`Parameters["operator"]`), reason codes, and optional second approver metadata for two-person integrity policies.
- Alarm acknowledgements, shelving, and inhibit commands must be idempotent; responders return `CommandResult.Accepted` with a monotonically increasing sequence number so annunciators reconcile in redundant deployments.
- SCADA connectors surface heartbeat metrics through `TelemetryHub` under the `system/*` namespace; consumers should monitor these channels to drive redundant connector failover.
- Historian scrubbing and live mode transitions emit `CommandResult.Pending` updates followed by completion events; dashboards must reflect intermediate states to avoid stale controls.

## Editor Addendum
- Editor preview services rely on sandboxed `TelemetryHub`/`ScadaTelemetryRouter` instances; bindings authored in the editor must validate quality metadata and stale timers identical to runtime expectations.
- Drag/drop canvas prototypes (`ffi/experimental/editor_canvas_prototype`) simulate value-driven updates without real connectors; Phase 1 will swap to shared preview services exposed via `VelloSharp.Editor.Telemetry`.
- Serialization format (`docs/specs/editor-serialization.md`) persists telemetry and command bindings using the same schema as runtime dashboards to guarantee lossless deployment.

## Testing & Diagnostics
- `TelemetryHubTests` in `tests/VelloSharp.Charting.Tests` validate fan-out, cancellation, and command routing behaviour.
- Consumers must add integration tests covering signal quality propagation and command acknowledgement flows before marking plan milestones complete.

