---
title: VelloSharp Documentation
---

> [!div class="hero"]
> # VelloSharp Documentation
> Build real-time, GPU-accelerated experiences with the VelloSharp family of .NET libraries.
> 
> [Documentation Overview](articles/index.md) · [API Reference](api/index.md) · [GitHub Repository](https://github.com/wieslawsoltes/VelloSharp)

> [!div class="cards cards-3"]
> [!div class="card"]
> ### Conceptual Guides
> [Start with the documentation overview](articles/index.md) to explore renderer, charting, and visualization libraries, plus platform integrations.
> 
> [!div class="card"]
> ### API Reference
> [Browse the generated API reference](api/index.md) for namespaces, types, and members sourced directly from the latest `main` branch builds.
> 
> [!div class="card"]
> ### Samples & Tooling
> Clone the repository and jump into the [integration samples](https://github.com/wieslawsoltes/VelloSharp/tree/main/integration) and [Avalonia demos](https://github.com/wieslawsoltes/VelloSharp/tree/main/samples) to see the runtime in action.

## Quick Start

```powershell
git clone https://github.com/wieslawsoltes/VelloSharp.git
cd VelloSharp
dotnet tool restore
docfx docs/docfx/docfx.json --serve
```

- Review the [DocFX maintenance guide](articles/docs-maintenance.md) for local build, preview, and cleanup workflows.
- Use `dotnet build` on targeted integration projects (e.g., `integration/managed/VelloSharp.Rendering.Integration`) to validate the end-to-end pipelines.
- Check the [STATUS.md](../STATUS.md) dashboard for roadmap and milestone tracking.

## Ecosystem Highlights

- **Rendering pipeline:** Core engine built on Vello with GPU paths via Skia, WGPU, and specialized charting compositors.
- **Cross-platform UI:** Avalonia, Uno, WPF, WinForms, and custom windowing hosts for native or hybrid experiences.
- **Data visualization:** High-frequency charting, SCADA dashboards, gauges, and tree data grids tuned for real-time telemetry.
- **Interop surface:** Rich FFI layers for hosting VelloSharp from native stacks or extending the rendering backend.

Stay up to date by watching the repository or joining the discussion threads in `docs/` planning outlines for upcoming releases.
