using VelloSharp.Composition;

Console.WriteLine("Verifying VelloSharp.Composition package usage…");

var metrics = new LabelMetrics(width: 120, height: 24, baseline: 18);
Console.WriteLine($"Label metrics: {metrics.Width}x{metrics.Height} baseline {metrics.Baseline}");

Console.WriteLine("VelloSharp.Composition integration test completed.");
