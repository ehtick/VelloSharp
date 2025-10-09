using VelloSharp.Uno.Controls;

Console.WriteLine("Verifying VelloSharp.Uno package usage…");

var args = new VelloDiagnosticsChangedEventArgs("Test", DateTimeOffset.UtcNow, "Sample payload");
Console.WriteLine($"Diagnostics channel: {args.Channel}, Timestamp: {args.Timestamp:O}");
Console.WriteLine($"Assembly location: {typeof(VelloDiagnosticsChangedEventArgs).Assembly.Location}");

Console.WriteLine("VelloSharp.Uno integration test completed.");
