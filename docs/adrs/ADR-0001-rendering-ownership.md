# ADR-0001: Rendering Ownership and Engine Boundaries

## Status
Proposed – pending Architecture Council review.

## Context
- VelloSharp integrates Rust-based Vello rendering with .NET hosts.
- Deterministic resource ownership is required to avoid double-free and lifetime bugs across FFI.
- Engine features must be shareable across desktop, mobile, and headless environments.

## Decision
- The Rust chart engine (`ffi/chart-engine`) owns GPU resources, command buffers, and scene memory.
- The .NET facade (`src/VelloSharp.ChartEngine`) orchestrates lifecycle events (initialise, resize, render, dispose) but never manipulates raw GPU handles directly.
- Surface handles (WGPU textures, D3D11 textures, etc.) are passed to Rust through opaque descriptors validated before use.
- Resource reuse (buffers, glyph atlases) is centralised in Rust with reference-counted pools; .NET maintains lightweight managed mirrors for diagnostics only.

## Consequences
- Clear separation enables aggressive optimisation in Rust without leaking unsafe handles to managed code.
- Platform adapters can remain thin wrappers translating framework-specific surfaces into descriptors.
- Requires consistent FFI marshalling helpers to convert managed structs into Rust-friendly representations.
