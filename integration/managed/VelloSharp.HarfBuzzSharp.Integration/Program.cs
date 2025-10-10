using System.Reflection;
using System.Text;
using HarfBuzzSharp;

Console.WriteLine("Verifying VelloSharp.HarfBuzzSharp package usage…");

using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Hello, Vello!"));
using var blob = Blob.FromStream(stream);

Console.WriteLine($"Blob length: {blob.Length}");
Console.WriteLine($"First byte: {blob.AsSpan()[0]}");

var language = Language.FromBcp47("en");
Console.WriteLine($"Resolved language tag: {language?.ToString() ?? "<null>"}");

var assembly = Assembly.Load("VelloSharp.HarfBuzzSharp");
Console.WriteLine($"Assembly location: {assembly.Location}");

Console.WriteLine("VelloSharp.HarfBuzzSharp integration test completed.");
