using System;
using System.Reflection;

namespace VelloSharp.ChartRuntime.Windows.Integration;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Console.WriteLine("Verifying VelloSharp.ChartRuntime.Windows package usage.");

        var assembly = Assembly.Load("VelloSharp.ChartRuntime.Windows");
        Console.WriteLine($"Exported types: {assembly.GetExportedTypes().Length}");

        Console.WriteLine("VelloSharp.ChartRuntime.Windows integration test completed.");
    }
}
