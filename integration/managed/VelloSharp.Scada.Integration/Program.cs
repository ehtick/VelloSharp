using VelloSharp.Scada;

Console.WriteLine("Verifying VelloSharp.Scada package usage…");

var runtimeType = typeof(ScadaRuntime);
Console.WriteLine($"SCADA runtime type: {runtimeType.FullName}");

Console.WriteLine("VelloSharp.Scada integration test completed.");
