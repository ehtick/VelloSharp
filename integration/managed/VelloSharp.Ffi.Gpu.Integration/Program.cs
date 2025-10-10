using System.Reflection;

Console.WriteLine("Verifying VelloSharp.Ffi.Gpu package usage…");

var assembly = Assembly.Load("VelloSharp.Ffi.Gpu");
Console.WriteLine($"Assembly location: {assembly.Location}");

var rendererHandle = assembly.GetType("VelloSharp.Ffi.Gpu.VelloRendererHandle", throwOnError: true);
var sceneHandle = assembly.GetType("VelloSharp.Ffi.Gpu.VelloSceneHandle", throwOnError: true);
var nativeMethods = assembly.GetType("VelloSharp.NativeMethods", throwOnError: true);
var helpers = assembly.GetType("VelloSharp.Ffi.Gpu.GpuNativeHelpers", throwOnError: true);

Console.WriteLine($"Resolved handle types: {rendererHandle?.FullName}, {sceneHandle?.FullName}.");
Console.WriteLine($"Resolved helper type: {helpers?.FullName}.");
Console.WriteLine($"Resolved native method container: {nativeMethods?.FullName}.");

Console.WriteLine("VelloSharp.Ffi.Gpu integration test completed.");
