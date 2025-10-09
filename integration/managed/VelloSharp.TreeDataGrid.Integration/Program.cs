using VelloSharp.TreeDataGrid;

Console.WriteLine("Verifying VelloSharp.TreeDataGrid package usage…");

using var model = new TreeDataModel();
var nodes = new[] { new TreeNodeDescriptor(1, TreeRowKind.Data, 24f, hasChildren: false) };
model.AttachRoots(nodes);
model.Clear();

Console.WriteLine("VelloSharp.TreeDataGrid integration test completed.");

