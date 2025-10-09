using System.Reflection;

Console.WriteLine("Verifying VelloSharp.Skia.Gpu package usage…");

var assembly = Assembly.Load("VelloSharp.Skia.Gpu");
Console.WriteLine($"Loaded assembly '{assembly.FullName}' from '{assembly.Location}'.");
Console.WriteLine($"Defined types: {assembly.GetTypes().Length}");

Console.WriteLine("VelloSharp.Skia.Gpu integration test completed.");
