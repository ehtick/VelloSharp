using VelloSharp;
using VelloSharp.Avalonia.Vello;

Console.WriteLine("Verifying VelloSharp.Avalonia.Vello package usage…");

var options = new VelloPlatformOptions
{
    FramesPerSecond = 120,
    ClearColor = RgbaColor.FromBytes(16, 32, 64, 255),
};

var supportsMsaa16 = options.RendererOptions.SupportMsaa16;
var preferred = supportsMsaa16 ? AntialiasingMode.Msaa16 : options.Antialiasing;
Console.WriteLine($"Preferred antialiasing: {preferred}");
Console.WriteLine($"Assembly location: {typeof(VelloPlatformOptions).Assembly.Location}");

Console.WriteLine("VelloSharp.Avalonia.Vello integration test completed.");
