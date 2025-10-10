using System;
using VelloSharp.Charting.Wpf;

namespace VelloSharp.Charting.Wpf.Integration;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Console.WriteLine("Verifying VelloSharp.Charting.Wpf package usage.");
        Console.WriteLine($"WPF chart control type: {typeof(ChartView).FullName}");
        Console.WriteLine("VelloSharp.Charting.Wpf integration test completed.");
    }
}
