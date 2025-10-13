using VelloSharp.Uno.Controls;
using VelloSharp.Windows;
using VelloSharp.Windows.Shared.Diagnostics;

Console.WriteLine("Verifying VelloSharp.Uno package usage…");

var diagnostics = new WindowsGpuDiagnostics();
var eventArgs = new VelloDiagnosticsChangedEventArgs(diagnostics);
Console.WriteLine($"Diagnostics object reports {diagnostics.SwapChainPresentations} presentations.");
Console.WriteLine($"Assembly location: {typeof(VelloDiagnosticsChangedEventArgs).Assembly.Location}");

Console.WriteLine("VelloSharp.Uno integration test completed.");
