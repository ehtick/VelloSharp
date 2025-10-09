using VelloSharp.Scada;

Console.WriteLine("Verifying VelloSharp.Scada package usage…");

ScadaRuntime.EnsureInitialized();
Console.WriteLine($"SCADA runtime initialized: {ScadaRuntime.IsInitialized}");

Console.WriteLine("VelloSharp.Scada integration test completed.");
