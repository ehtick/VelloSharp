using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace VelloSharp;

internal static class NativeLibraryLoader
{
    private static readonly object Sync = new();
    private static bool _initialized;
    private static readonly string[] NativeLibraries =
    {
        "vello_ffi",
        "kurbo_ffi",
        "peniko_ffi",
        "winit_ffi",
    };

#pragma warning disable CA2255 // Module initializers limited use warning suppressed intentionally for native resolver registration.
    [ModuleInitializer]
    internal static void Initialize() => EnsureInitialized();
#pragma warning restore CA2255

    private static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }

            NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, Resolve);
            _initialized = true;
        }
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (Array.IndexOf(NativeLibraries, libraryName) < 0)
        {
            return IntPtr.Zero;
        }

        foreach (var candidate in EnumerateProbePaths(assembly, libraryName))
        {
            if (!string.IsNullOrWhiteSpace(candidate) && NativeLibrary.TryLoad(candidate, out var handle))
            {
                return handle;
            }
        }

        var fileName = GetLibraryFileName(libraryName);
        return NativeLibrary.Load(fileName, assembly, searchPath);
    }

    private static IEnumerable<string> EnumerateProbePaths(Assembly assembly, string libraryName)
    {
        var fileName = GetLibraryFileName(libraryName);
        var rid = RuntimeInformation.RuntimeIdentifier;

        if (!string.IsNullOrEmpty(AppContext.BaseDirectory))
        {
            if (!string.IsNullOrEmpty(rid))
            {
                yield return Path.Combine(AppContext.BaseDirectory, "runtimes", rid, "native", fileName);

                var ridBase = GetRidBase(rid);
                if (!string.Equals(ridBase, rid, StringComparison.Ordinal))
                {
                    yield return Path.Combine(AppContext.BaseDirectory, "runtimes", ridBase, "native", fileName);
                }
            }
            yield return Path.Combine(AppContext.BaseDirectory, fileName);
        }

        var assemblyLocation = assembly.Location;
        if (!string.IsNullOrWhiteSpace(assemblyLocation))
        {
            var assemblyDir = Path.GetDirectoryName(assemblyLocation);
            if (!string.IsNullOrEmpty(assemblyDir))
            {
                if (!string.IsNullOrEmpty(rid))
                {
                    yield return Path.Combine(assemblyDir, "runtimes", rid, "native", fileName);

                    var ridBase = GetRidBase(rid);
                    if (!string.Equals(ridBase, rid, StringComparison.Ordinal))
                    {
                        yield return Path.Combine(assemblyDir, "runtimes", ridBase, "native", fileName);
                    }
                }
                yield return Path.Combine(assemblyDir, fileName);
            }
        }

        yield return fileName;
    }

    private static string GetRidBase(string rid)
    {
        if (string.IsNullOrEmpty(rid))
        {
            return string.Empty;
        }

        var dashIndex = rid.IndexOf('-');
        return dashIndex > 0 ? rid[..dashIndex] : rid;
    }

    private static string GetLibraryFileName(string libraryName)
    {
        if (OperatingSystem.IsWindows())
        {
            return $"{libraryName}.dll";
        }

        if (OperatingSystem.IsMacOS())
        {
            return $"lib{libraryName}.dylib";
        }

        return $"lib{libraryName}.so";
    }
}
