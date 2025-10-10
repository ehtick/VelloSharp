using System;
using VelloSharp.Charting.WinForms;

namespace VelloSharp.Charting.WinForms.Integration;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Console.WriteLine("Verifying VelloSharp.Charting.WinForms package usage.");
        Console.WriteLine($"WinForms chart control type: {typeof(ChartView).FullName}");
        Console.WriteLine("VelloSharp.Charting.WinForms integration test completed.");
    }
}
