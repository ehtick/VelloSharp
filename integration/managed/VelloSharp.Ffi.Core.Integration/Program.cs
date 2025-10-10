using System.Reflection;
using VelloSharp;

Console.WriteLine("Verifying VelloSharp.Ffi.Core package usage…");

var gradient = new PenikoLinearGradient
{
    Start = new PenikoPoint { X = 0, Y = 0 },
    End = new PenikoPoint { X = 128, Y = 256 },
};

var stops = new[]
{
    new PenikoColorStop
    {
        Offset = 0f,
        Color = new VelloColor { R = 1f, G = 0f, B = 0f, A = 1f },
    },
    new PenikoColorStop
    {
        Offset = 1f,
        Color = new VelloColor { R = 0f, G = 0f, B = 1f, A = 1f },
    },
};

Console.WriteLine($"Gradient from ({gradient.Start.X}, {gradient.Start.Y}) to ({gradient.End.X}, {gradient.End.Y}).");
Console.WriteLine($"First stop RGBA: {stops[0].Color.R}, {stops[0].Color.G}, {stops[0].Color.B}, {stops[0].Color.A}.");
Console.WriteLine($"Assembly location: {typeof(PenikoPoint).Assembly.Location}");

var assembly = Assembly.Load("VelloSharp.Ffi.Core");
Console.WriteLine($"Loaded assembly '{assembly.FullName}'.");

Console.WriteLine("VelloSharp.Ffi.Core integration test completed.");
