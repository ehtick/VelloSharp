using System;
using VelloSharp.Wpf.Integration;

[STAThread]

Console.WriteLine("Verifying VelloSharp.Integration.Wpf package usage…");

Console.WriteLine($"Resolved type: {typeof(VelloNativeSwapChainView).FullName}");
Console.WriteLine($"Assembly location: {typeof(VelloNativeSwapChainView).Assembly.Location}");

Console.WriteLine("VelloSharp.Integration.Wpf integration test completed.");
