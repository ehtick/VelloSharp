using System.Reflection;

Console.WriteLine("Verifying VelloSharp.Ffi.Sparse package usage…");

var assembly = Assembly.Load("VelloSharp.Ffi.Sparse");
Console.WriteLine($"Assembly location: {assembly.Location}");

var renderContextHandle = assembly.GetType("VelloSharp.SparseRenderContextHandle", throwOnError: true);
var simdLevel = assembly.GetType("VelloSharp.SparseSimdLevel", throwOnError: true);
var nativeMethods = assembly.GetType("VelloSharp.SparseNativeMethods", throwOnError: true);
var helpers = assembly.GetType("VelloSharp.SparseNativeHelpers", throwOnError: true);

Console.WriteLine($"Resolved sparse handle type: {renderContextHandle?.FullName}.");
Console.WriteLine($"Resolved SIMD enum type: {simdLevel?.FullName}.");
Console.WriteLine($"Resolved native interop helpers: {helpers?.FullName}.");
Console.WriteLine($"Resolved native methods: {nativeMethods?.FullName}.");

Console.WriteLine("VelloSharp.Ffi.Sparse integration test completed.");
