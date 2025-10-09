using System;
using System.Reflection;

[STAThread]
Console.WriteLine("Verifying VelloSharp.ChartRuntime.Windows package usage…");

var assembly = Assembly.Load("VelloSharp.ChartRuntime.Windows");
Console.WriteLine($"Exported types: {assembly.GetExportedTypes().Length}");

Console.WriteLine("VelloSharp.ChartRuntime.Windows integration test completed.");

