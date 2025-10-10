using VelloSharp.Composition;

Console.WriteLine("Verifying VelloSharp.Composition package usage…");

var metrics = new LabelMetrics(120, 24, 18);
Console.WriteLine($"Label metrics: {metrics.Width}x{metrics.Height} ascent {metrics.Ascent}");

Console.WriteLine("VelloSharp.Composition integration test completed.");
