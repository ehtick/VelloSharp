using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

namespace AvaloniaVelloSkiaSharpSample.Diagnostics;

public sealed class SkiaCaptureRecorder
{
    public SkiaCaptureRecorder(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        RootPath = rootPath;
    }

    public string RootPath { get; }

    public Task<string> SaveSnapshotAsync(SKSurface surface, string label, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        throw new NotSupportedException("Image encoding is not yet supported by the SkiaSharp shim.");
    }

    private static string Sanitise(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '-');
        }

        return value;
    }
}
