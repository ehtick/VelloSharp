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
    private static readonly HashSet<string> NativeLibraries = new(StringComparer.OrdinalIgnoreCase)
    {
        "vello_ffi",
        "vello_sparse_ffi",
        "kurbo_ffi",
        "peniko_ffi",
    };

#pragma warning disable CA2255 // Module initializers limited use warning suppressed intentionally for native resolver registration.
    [ModuleInitializer]
    internal static void Initialize() => EnsureInitialized();
#pragma warning restore CA2255

    internal static void RegisterNativeLibrary(string libraryName)
    {
        if (string.IsNullOrWhiteSpace(libraryName))
        {
            return;
        }

        lock (Sync)
        {
            NativeLibraries.Add(NormalizeLibraryName(libraryName));
        }
    }

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
        var normalizedName = NormalizeLibraryName(libraryName);
        bool shouldProbe;
        lock (Sync)
        {
            shouldProbe = NativeLibraries.Contains(normalizedName);
        }

        if (!shouldProbe)
        {
            return IntPtr.Zero;
        }

        foreach (var candidate in EnumerateProbePaths(assembly, normalizedName))
        {
            if (!string.IsNullOrWhiteSpace(candidate) && NativeLibrary.TryLoad(candidate, out var handle))
            {
                return handle;
            }
        }

        var fileName = GetLibraryFileName(normalizedName);
        return NativeLibrary.Load(fileName, assembly, searchPath);
    }

    private static string NormalizeLibraryName(string libraryName)
    {
        if (string.IsNullOrWhiteSpace(libraryName))
        {
            return libraryName;
        }

        var normalized = libraryName;

        if (normalized.StartsWith("lib", StringComparison.Ordinal))
        {
            normalized = normalized[3..];
        }

        normalized = normalized
            .Replace(".dll", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(".dylib", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(".so", string.Empty, StringComparison.OrdinalIgnoreCase);

        return normalized;
    }

    private static IEnumerable<string> EnumerateProbePaths(Assembly assembly, string libraryName)
    {
        var fileName = GetLibraryFileName(libraryName);
        foreach (var path in EnumerateDirectoryProbePaths(AppContext.BaseDirectory, fileName))
        {
            yield return path;
        }

        var assemblyLocation = assembly.Location;
        if (!string.IsNullOrWhiteSpace(assemblyLocation))
        {
            var assemblyDir = Path.GetDirectoryName(assemblyLocation);
            if (!string.IsNullOrEmpty(assemblyDir))
            {
                foreach (var path in EnumerateDirectoryProbePaths(assemblyDir, fileName))
                {
                    yield return path;
                }
            }
        }

        foreach (var path in EnumerateRepositoryProbePaths(AppContext.BaseDirectory, fileName))
        {
            yield return path;
        }

        foreach (var path in EnumerateRepositoryProbePaths(Path.GetDirectoryName(assembly.Location), fileName))
        {
            yield return path;
        }

        yield return fileName;
    }

    private static IEnumerable<string> EnumerateDirectoryProbePaths(string? directory, string fileName)
    {
        if (string.IsNullOrEmpty(directory))
        {
            yield break;
        }

        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rid in GetRidCandidates())
        {
            if (string.IsNullOrEmpty(rid))
            {
                continue;
            }

            var candidate = Path.Combine(directory, "runtimes", rid, "native", fileName);
            if (emitted.Add(candidate))
            {
                yield return candidate;
            }
        }

        var direct = Path.Combine(directory, fileName);
        if (emitted.Add(direct))
        {
            yield return direct;
        }
    }

    private static IEnumerable<string> GetRidCandidates()
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        if (!string.IsNullOrEmpty(rid))
        {
            yield return rid;
        }

        var baseRid = GetRidBase(rid);
        if (!string.IsNullOrEmpty(baseRid))
        {
            yield return baseRid;

            var archSuffix = GetArchitectureSuffix();
            if (!string.IsNullOrEmpty(archSuffix))
            {
                yield return $"{baseRid}-{archSuffix}";
            }
        }
    }

    private static IEnumerable<string> EnumerateRepositoryProbePaths(string? startDirectory, string fileName)
    {
        if (string.IsNullOrEmpty(startDirectory))
        {
            yield break;
        }

        var current = new DirectoryInfo(startDirectory);
        var depth = 0;
        while (current is not null && depth++ < 6)
        {
            var candidate = Path.Combine(current.FullName, "VelloSharp", "bin");
            if (Directory.Exists(candidate))
            {
                foreach (var configuration in new[] { "Debug", "Release" })
                {
                    var basePath = Path.Combine(candidate, configuration, "net8.0");
                    foreach (var rid in GetRidCandidates())
                    {
                        var runtimePath = Path.Combine(basePath, "runtimes", rid, "native", fileName);
                        yield return runtimePath;
                    }

                    yield return Path.Combine(basePath, fileName);
                }
            }

            current = current.Parent;
        }
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

    private static string GetArchitectureSuffix()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            Architecture.Wasm => "wasm",
            Architecture.S390x => "s390x",
            Architecture.LoongArch64 => "loongarch64",
            _ => string.Empty,
        };
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
