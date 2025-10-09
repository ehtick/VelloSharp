using System.Reflection;

Console.WriteLine("Verifying VelloSharp.Avalonia.Winit package usage…");

var assembly = Assembly.Load("VelloSharp.Avalonia.Winit");
Console.WriteLine($"Loaded assembly '{assembly.FullName}' from '{assembly.Location}'.");

Console.WriteLine("VelloSharp.Avalonia.Winit integration test completed.");
