using VelloSharp.Gauges;

Console.WriteLine("Verifying VelloSharp.Gauges package usage…");

GaugeModule.EnsureInitialized();
Console.WriteLine($"Gauges initialized: {GaugeModule.IsInitialized}");

Console.WriteLine("VelloSharp.Gauges integration test completed.");
