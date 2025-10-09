using VelloSharp.Editor;

Console.WriteLine("Verifying VelloSharp.Editor package usage…");

EditorRuntime.EnsureInitialized();
Console.WriteLine($"Editor initialized: {EditorRuntime.IsInitialized}");

Console.WriteLine("VelloSharp.Editor integration test completed.");
