# VelloSharp Documentation

VelloSharp delivers a family of .NET libraries for GPU accelerated rendering, real-time data visualizations, and cross-platform UI integration. This site combines conceptual guides with full API reference generated from the source projects.

- Browse the [Libraries catalog](./libraries/vellosharp.md) for getting started guides tailored to each package family.
- Explore the [API Reference](../api/index.md) for namespace and member details generated directly from the builds.
- Follow the [DocFX Maintenance guide](./docs-maintenance.md) to rebuild, preview, or clean the documentation artifacts locally.

## Explore the Libraries

- **Core Renderer:** Dive into [VelloSharp](./libraries/vellosharp.md), [VelloSharp.Rendering](./libraries/vellosharp-rendering.md), [VelloSharp.Core](./libraries/vellosharp-core.md), and [VelloSharp.Text](./libraries/vellosharp-text.md) for the rendering pipeline and text stack.
- **GPU & Skia runtimes:** Compare [VelloSharp.Gpu](./libraries/vellosharp-gpu.md), [Skia GPU](./libraries/vellosharp-skia-gpu.md), and [Skia CPU](./libraries/vellosharp-skia-cpu.md) options for deployment.
- **Interop layers:** Extend the engine with [FFI Core](./libraries/vellosharp-ffi-core.md), [FFI GPU](./libraries/vellosharp-ffi-gpu.md), and [FFI Sparse](./libraries/vellosharp-ffi-sparse.md).
- **Visualization suites:** Build dashboards using [Charting](./libraries/vellosharp-charting.md), [Gauges](./libraries/vellosharp-gauges.md), and [SCADA](./libraries/vellosharp-scada.md).
- **Editor experiences:** Explore [Composition](./libraries/vellosharp-composition.md), the [Editor](./libraries/vellosharp-editor.md), and [TreeDataGrid](./libraries/vellosharp-treedatagrid.md) workflows.

## Integration Tracks

- **Cross-platform UI:** Follow guides for [Avalonia (Vello)](./libraries/vellosharp-avalonia-vello.md), [Avalonia (Winit)](./libraries/vellosharp-avalonia-winit.md), [Uno Platform](./libraries/vellosharp-uno.md), [WPF](./libraries/vellosharp-charting-wpf.md), and [WinForms](./libraries/vellosharp-charting-winforms.md).
- **Runtime plumbing:** Start with [VelloSharp.Integration](./libraries/vellosharp-integration.md) for shared patterns, then specialize with [WPF integration](./libraries/vellosharp-integration-wpf.md) or [WinForms integration](./libraries/vellosharp-integration-winforms.md).
- **Diagnostics & telemetry:** Harness [ChartDiagnostics](./libraries/vellosharp-chartdiagnostics.md) and [ChartRuntime.Windows](./libraries/vellosharp-chartruntime-windows.md) for instrumentation and performance tuning.

## Maintenance & Community

- Rebuild and preview the site using the [DocFX maintenance guide](./docs-maintenance.md).
- Track upcoming work in the `/docs` planning outlines and the repository [STATUS.md](../../STATUS.md).
- Report issues or propose enhancements via the [GitHub issue tracker](https://github.com/wieslawsoltes/VelloSharp/issues).

The documentation is produced with DocFX during continuous integration, ensuring the content always matches the latest code in `main`.
