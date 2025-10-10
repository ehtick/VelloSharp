using System.Reflection;
using VelloSharp;
using VelloSharp.Rendering;

Console.WriteLine("Verifying VelloSharp.Rendering package usage…");

var descriptor = new RenderTargetDescriptor(256, 256, RenderFormat.Bgra8, StrideBytes: 1024);
var renderParams = new RenderParams(128, 128, RgbaColor.FromBytes(32, 64, 96));

Span<byte> buffer = stackalloc byte[descriptor.RequiredBufferSize];
descriptor.EnsureBuffer(buffer);

var negotiated = descriptor.Apply(renderParams);
Console.WriteLine($"Negotiated render size: {negotiated.Width}x{negotiated.Height} in {negotiated.Format} format.");

var assembly = Assembly.Load("VelloSharp.Rendering");
Console.WriteLine($"Assembly location: {assembly.Location}");

Console.WriteLine("VelloSharp.Rendering integration test completed.");
