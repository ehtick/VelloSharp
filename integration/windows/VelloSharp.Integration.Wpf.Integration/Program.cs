using System;
using VelloSharp.Wpf.Integration;

namespace VelloSharp.Integration.Wpf.Integration;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Console.WriteLine("Verifying VelloSharp.Integration.Wpf package usage.");
        Console.WriteLine($"Resolved type: {typeof(VelloNativeSwapChainView).FullName}");
        Console.WriteLine($"Assembly location: {typeof(VelloNativeSwapChainView).Assembly.Location}");
        Console.WriteLine("VelloSharp.Integration.Wpf integration test completed.");
    }
}
