using System.Reflection;

Console.WriteLine("Verifying VelloSharp.Skia.Core package usage…");

var assembly = Assembly.Load("VelloSharp.Skia.Core");
Console.WriteLine($"Loaded assembly '{assembly.FullName}' from '{assembly.Location}'.");
Console.WriteLine($"Exported types: {assembly.GetExportedTypes().Length}");

Console.WriteLine("VelloSharp.Skia.Core integration test completed.");
