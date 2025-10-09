using VelloSharp;
using VelloSharp.Avalonia.Vello;

Console.WriteLine("Verifying VelloSharp.Avalonia.Vello package usage…");

var options = new VelloPlatformOptions
{
    FramesPerSecond = 120,
    ClearColor = RgbaColor.FromBytes(16, 32, 64, 255),
};

var resolved = options.ResolveAntialiasing(AntialiasingMode.Msaa16);
Console.WriteLine($"Resolved antialiasing: {resolved}");
Console.WriteLine($"Assembly location: {typeof(VelloPlatformOptions).Assembly.Location}");

Console.WriteLine("VelloSharp.Avalonia.Vello integration test completed.");
