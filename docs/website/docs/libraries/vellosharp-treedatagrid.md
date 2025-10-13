# VelloSharp.TreeDataGrid

`VelloSharp.TreeDataGrid` provides a high-performance tree data grid control backed by the Vello renderer.

## Getting Started

1. Install the package: `dotnet add package VelloSharp.TreeDataGrid`.
2. Add `using VelloSharp.TreeDataGrid;` to the project where you display hierarchical data.
3. Bind the grid to your view models using the data source abstractions provided by the package, then host it in your UI framework of choice.
4. Combine the grid with `VelloSharp.Charting` or `VelloSharp.Composition` when you want synchronized detail views or mixed visuals.

## Usage Example

```csharp
using VelloSharp.TreeDataGrid;

using var model = new TreeDataModel();
model.AttachRoots(new[] { new TreeNodeDescriptor(1, TreeRowKind.Data, 24f, hasChildren: false) });
```

## Next Steps

- Review the API reference for virtualization options, column configuration, and interaction hooks.
- Examine the `samples/VelloSharp.TreeDataGrid.CompositionSample` project to see the grid operating inside a composed scene.

