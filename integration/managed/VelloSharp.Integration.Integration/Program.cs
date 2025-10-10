using System.Reflection;
using Avalonia;
using VelloSharp.Integration.Avalonia;

Console.WriteLine("Verifying VelloSharp.Integration package usage…");

var builder = AppBuilder.Configure(() => new DummyApp());
builder.UseVelloSkiaTextServices();

Console.WriteLine("Configured Avalonia builder with Vello text services.");

var assembly = Assembly.Load("VelloSharp.Integration");
Console.WriteLine($"Assembly location: {assembly.Location}");

Console.WriteLine("VelloSharp.Integration package integration test completed.");

sealed class DummyApp : Application
{
}
