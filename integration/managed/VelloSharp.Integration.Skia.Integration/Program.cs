using System.Reflection;

Console.WriteLine("Verifying VelloSharp.Integration.Skia package usage…");

var assembly = Assembly.Load("VelloSharp.Integration.Skia");
Console.WriteLine($"Loaded assembly '{assembly.FullName}' from '{assembly.Location}'.");

Console.WriteLine("VelloSharp.Integration.Skia integration test completed.");
