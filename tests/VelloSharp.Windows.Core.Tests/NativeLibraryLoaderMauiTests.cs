#nullable enable

using System;
using System.IO;
using System.Linq;
using VelloSharp;
using Xunit;

namespace VelloSharp.Windows.Core.Tests;

public sealed class NativeLibraryLoaderMauiTests : IDisposable
{
    [Fact]
    public void EnumerateMauiPaths_AndroidIncludesLibAndAbiFolders()
    {
        NativeLibraryLoader.SetPlatformOverrides(isAndroid: true);

        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var results = NativeLibraryLoader
            .EnumerateMauiSpecificPaths(baseDir, "libvello.so")
            .ToArray();

        Assert.Contains(Path.GetFullPath(Path.Combine(baseDir, "lib", "libvello.so")), results);
        Assert.Contains(Path.GetFullPath(Path.Combine(baseDir, "lib", "x86_64", "libvello.so")), results);
        Assert.Contains(Path.GetFullPath(Path.Combine(baseDir, "lib", "arm64-v8a", "libvello.so")), results);
    }

    [Fact]
    public void EnumerateMauiPaths_MacCatalystIncludesFrameworksAndMonoBundle()
    {
        NativeLibraryLoader.SetPlatformOverrides(isMacCatalyst: true);

        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var results = NativeLibraryLoader
            .EnumerateMauiSpecificPaths(baseDir, "libvello.dylib")
            .ToArray();

        Assert.Contains(Path.GetFullPath(Path.Combine(baseDir, "libvello.dylib")), results);
        Assert.Contains(Path.GetFullPath(Path.Combine(baseDir, "Frameworks", "libvello.dylib")), results);
        Assert.Contains(Path.GetFullPath(Path.Combine(baseDir, "..", "Frameworks", "libvello.dylib")), results);
        Assert.Contains(Path.GetFullPath(Path.Combine(baseDir, "MonoBundle", "libvello.dylib")), results);
    }

    [Fact]
    public void EnumerateMauiPaths_IosIncludesFrameworks()
    {
        NativeLibraryLoader.SetPlatformOverrides(isIos: true);

        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var results = NativeLibraryLoader
            .EnumerateMauiSpecificPaths(baseDir, "libvello.dylib")
            .ToArray();

        Assert.Contains(Path.GetFullPath(Path.Combine(baseDir, "libvello.dylib")), results);
        Assert.Contains(Path.GetFullPath(Path.Combine(baseDir, "Frameworks", "libvello.dylib")), results);
        Assert.Contains(Path.GetFullPath(Path.Combine(baseDir, "..", "Frameworks", "libvello.dylib")), results);
    }

    [Fact]
    public void EnumerateMauiPaths_DefaultPlatformReturnsEmptyOnWindows()
    {
        NativeLibraryLoader.ResetPlatformOverrides();

        var baseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var results = NativeLibraryLoader
            .EnumerateMauiSpecificPaths(baseDir, "libvello.so")
            .ToArray();

        Assert.Empty(results);
    }

    public void Dispose()
    {
        NativeLibraryLoader.ResetPlatformOverrides();
    }
}
