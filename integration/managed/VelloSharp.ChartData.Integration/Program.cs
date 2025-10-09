using System;
using VelloSharp.ChartData;

Console.WriteLine("Verifying VelloSharp.ChartData package usage…");

var bus = new ChartDataBus(capacity: 4);
bus.Write(ReadOnlySpan<float>.Empty);
bus.Write(new[] { 1.0f, 2.5f, 3.75f });

if (bus.TryRead(out var slice))
{
    Console.WriteLine($"Slice contains {slice.ItemCount} samples of {slice.ElementType.Name}.");
    slice.Dispose();
}

Console.WriteLine("VelloSharp.ChartData integration test completed.");
