# ADR-0003: Serialization Strategy for Chart State

## Status
Proposed – pending Architecture Council review.

## Context
- Real-time chart layouts and series definitions must persist across sessions and support export/import scenarios.
- Large datasets (millions of points) cannot be blindly serialized without compression or decimation.

## Decision
- Use a binary snapshot format (`.vellochart`) for high-frequency persistence containing metadata, layout descriptors, and compressed data windows.
- Provide parallel JSON descriptors for tooling interoperability (editors, diagnostics); JSON references binary payloads via URIs.
- Implement snapshot read/write services in `ffi/chart-data` with FFI exposed methods for .NET consumers.
- Version snapshots using semantic version fields; load routines support backward-compatible migrations.

## Consequences
- Binary format sustains performance targets and keeps file sizes manageable.
- Dual representation increases code surface area; shared schema definitions mitigate divergence.
- Requires validation tooling to inspect and diff snapshots within CI.
