using System;
using VelloSharp.ChartRuntime;

Console.WriteLine("Verifying VelloSharp.ChartRuntime package usage…");

using var scheduler = new RenderScheduler(TimeSpan.FromMilliseconds(16), TimeProvider.System);
Console.WriteLine("Render scheduler created successfully.");

Console.WriteLine("VelloSharp.ChartRuntime integration test completed.");

