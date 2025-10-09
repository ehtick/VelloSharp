using VelloSharp.ChartEngine;

Console.WriteLine("Verifying VelloSharp.ChartEngine package usage…");

var profile = ChartAnimationProfile.Default with { ReducedMotionEnabled = true };
var color = ChartColor.FromRgb(34, 139, 230);

Console.WriteLine($"Reduced motion: {profile.ReducedMotionEnabled}, Color: {color}");
Console.WriteLine("VelloSharp.ChartEngine integration test completed.");

