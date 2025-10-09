using System;
using VelloSharp.WinForms.Integration;

[STAThread]

Console.WriteLine("Verifying VelloSharp.Integration.WinForms package usage…");

Console.WriteLine($"Resolved type: {typeof(VelloRenderControl).FullName}");
Console.WriteLine($"Assembly location: {typeof(VelloRenderControl).Assembly.Location}");

Console.WriteLine("VelloSharp.Integration.WinForms integration test completed.");
