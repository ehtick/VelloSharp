using System;
using VelloSharp.WinForms;

[STAThread]

Console.WriteLine("Verifying VelloSharp.WinForms.Core package usage…");

Console.WriteLine($"Resolved type: {typeof(VelloBitmap).FullName}");
Console.WriteLine($"Assembly location: {typeof(VelloBitmap).Assembly.Location}");

Console.WriteLine("VelloSharp.WinForms.Core integration test completed.");
