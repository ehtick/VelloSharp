using System.Reflection;

Console.WriteLine("Verifying VelloSharp.Skia.Cpu package usage…");

var assembly = Assembly.Load("VelloSharp.Skia.Cpu");
Console.WriteLine($"Loaded assembly '{assembly.FullName}' from '{assembly.Location}'.");
Console.WriteLine($"Defined types: {assembly.GetTypes().Length}");

Console.WriteLine("VelloSharp.Skia.Cpu integration test completed.");
