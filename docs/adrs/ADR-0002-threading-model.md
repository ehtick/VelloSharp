# ADR-0002: Threading Model and Scheduler

## Status
Proposed – pending Architecture Council review.

## Context
- Host frameworks exhibit differing threading rules (e.g., WPF single UI thread, WinUI CoreDispatcher, MAUI multi-threaded).
- The chart engine must maintain deterministic frame pacing while ingesting high-frequency data.

## Decision
- Introduce a dedicated render scheduler within `src/VelloSharp.ChartRuntime` driving frame ticks on a background thread.
- UI frameworks forward surface resize and input events through thread-safe channels; the scheduler marshals updates to the render thread.
- A cooperative scheduling mode allows adapters to request synchronous renders when host frameworks require UI-thread drawing (e.g., WinForms fallback).
- Data ingestion occurs on worker threads feeding lock-free ring buffers; render thread consumes snapshots without blocking producers.

## Consequences
- Consistent frame pacing achievable across frameworks with explicit ownership of the render loop.
- Introduces additional coordination overhead; adapters must initialise the scheduler during control construction.
- Facilitates deterministic testing by enabling manual tick advancement in headless or offline scenarios.
