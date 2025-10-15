#pragma warning disable CS0436
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace VelloSharp;

internal static class NativeLibraryLoader
{
    private static readonly object Sync = new();
    private static bool _initialized;
    private static readonly HashSet<string> NativeLibraries = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> CustomProbeRoots = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<Assembly> AssembliesWithResolver = new();
    private static bool? _androidOverride;
    private static bool? _iosOverride;
    private static bool? _macCatalystOverride;
    private const int AppModelErrorNoPackage = 15700;
    private const int ErrorInsufficientBuffer = 122;
    private static readonly Lazy<AppContainerInfo> AppContainer = new(GetAppContainerInfo);

    internal static void SetPlatformOverrides(bool? isAndroid = null, bool? isIos = null, bool? isMacCatalyst = null)
    {
        _androidOverride = isAndroid;
        _iosOverride = isIos;
        _macCatalystOverride = isMacCatalyst;
    }

    internal static void ResetPlatformOverrides()
    {
        _androidOverride = null;
        _iosOverride = null;
        _macCatalystOverride = null;
    }

#pragma warning disable CA2255 // Module initializers limited use warning suppressed intentionally for native resolver registration.
    [ModuleInitializer]
    internal static void Initialize() => EnsureInitialized();
#pragma warning restore CA2255

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void RegisterNativeLibrary(string libraryName)
    {
        if (string.IsNullOrWhiteSpace(libraryName))
        {
            return;
        }

        RegisterResolverForAssembly(Assembly.GetCallingAssembly());

        lock (Sync)
        {
            NativeLibraries.Add(NormalizeLibraryName(libraryName));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void RegisterNativeLibraries(params string[] libraryNames)
    {
        if (libraryNames is null || libraryNames.Length == 0)
        {
            return;
        }

        RegisterResolverForAssembly(Assembly.GetCallingAssembly());

        lock (Sync)
        {
            foreach (var libraryName in libraryNames)
            {
                if (string.IsNullOrWhiteSpace(libraryName))
                {
                    continue;
                }

                NativeLibraries.Add(NormalizeLibraryName(libraryName));
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void RegisterProbingPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        RegisterResolverForAssembly(Assembly.GetCallingAssembly());

        lock (Sync)
        {
            CustomProbeRoots.Add(path);
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

            RegisterResolverUnsafe(typeof(NativeLibraryLoader).Assembly);
            _initialized = true;
        }
    }

    internal static void RegisterResolverForAssembly(Assembly assembly)
    {
        if (assembly is null)
        {
            return;
        }

        EnsureInitialized();

        lock (Sync)
        {
            RegisterResolverUnsafe(assembly);
        }
    }

    private static void RegisterResolverUnsafe(Assembly assembly)
    {
        if (assembly is null || AssembliesWithResolver.Contains(assembly))
        {
            return;
        }

        NativeLibrary.SetDllImportResolver(assembly, Resolve);
        AssembliesWithResolver.Add(assembly);
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        var normalizedName = NormalizeLibraryName(libraryName);
        bool shouldProbe;
        string[] customRoots;
        lock (Sync)
        {
            shouldProbe = NativeLibraries.Contains(normalizedName);
            customRoots = CustomProbeRoots.ToArray();
        }

        if (!shouldProbe)
        {
            return IntPtr.Zero;
        }

        foreach (var candidate in EnumerateProbePaths(assembly, normalizedName, customRoots))
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

    private static IEnumerable<string> EnumerateProbePaths(Assembly assembly, string libraryName, IReadOnlyCollection<string> customRoots)
    {
        var fileName = GetLibraryFileName(libraryName);
        foreach (var path in EnumerateDirectoryProbePaths(AppContext.BaseDirectory, fileName))
        {
            yield return path;
        }

        foreach (var path in EnumerateAppContainerProbePaths(fileName))
        {
            yield return path;
        }

        foreach (var path in EnumerateMauiSpecificPaths(AppContext.BaseDirectory, fileName))
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

                foreach (var path in EnumerateMauiSpecificPaths(assemblyDir, fileName))
                {
                    yield return path;
                }
            }
        }

        if (customRoots.Count > 0)
        {
            foreach (var root in customRoots)
            {
                foreach (var path in EnumerateDirectoryProbePaths(root, fileName))
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

    private static IEnumerable<string> EnumerateAppContainerProbePaths(string fileName)
    {
        var info = AppContainer.Value;
        if (!info.IsPackaged)
        {
            yield break;
        }

        foreach (var root in new[] { info.PackagePath, info.LocalStatePath, info.TempStatePath })
        {
            if (string.IsNullOrEmpty(root))
            {
                continue;
            }

            foreach (var path in EnumerateDirectoryProbePaths(root, fileName))
            {
                yield return path;
            }
        }
    }

    private static AppContainerInfo GetAppContainerInfo()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new AppContainerInfo(false, null, null, null);
        }

        try
        {
            var packagePath = TryGetPackagePath();
            var familyName = TryGetPackageFamilyName();

            if (string.IsNullOrEmpty(packagePath) && string.IsNullOrEmpty(familyName))
            {
                return new AppContainerInfo(false, null, null, null);
            }

            string? localState = null;
            string? tempState = null;

            if (!string.IsNullOrEmpty(familyName))
            {
                try
                {
                    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    if (!string.IsNullOrEmpty(localAppData))
                    {
                        var packagesRoot = Path.Combine(localAppData, "Packages", familyName);
                        localState = Path.Combine(packagesRoot, "LocalState");
                        tempState = Path.Combine(packagesRoot, "TempState");
                    }
                }
                catch
                {
                    localState = null;
                    tempState = null;
                }
            }

            return new AppContainerInfo(true, packagePath, localState, tempState);
        }
        catch
        {
            return new AppContainerInfo(false, null, null, null);
        }
    }

    private static string? TryGetPackagePath()
    {
        try
        {
            var length = 0;
            var result = GetCurrentPackagePath(ref length, null);
            if (result == AppModelErrorNoPackage)
            {
                return null;
            }

            if ((result != ErrorInsufficientBuffer && result != 0) || length <= 0)
            {
                return null;
            }

            if (length == 0)
            {
                return null;
            }

            var buffer = new StringBuilder(length);
            result = GetCurrentPackagePath(ref length, buffer);
            if (result != 0)
            {
                return null;
            }

            var value = buffer.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value.TrimEnd('\0');
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetPackageFamilyName()
    {
        try
        {
            var length = 0;
            var result = GetCurrentPackageFamilyName(ref length, null);
            if (result == AppModelErrorNoPackage)
            {
                return null;
            }

            if ((result != ErrorInsufficientBuffer && result != 0) || length <= 0)
            {
                return null;
            }

            var buffer = new StringBuilder(length);
            result = GetCurrentPackageFamilyName(ref length, buffer);
            if (result != 0)
            {
                return null;
            }

            var value = buffer.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value.TrimEnd('\0');
        }
        catch
        {
            return null;
        }
    }

    private sealed class AppContainerInfo
    {
        internal AppContainerInfo(bool isPackaged, string? packagePath, string? localStatePath, string? tempStatePath)
        {
            IsPackaged = isPackaged;
            PackagePath = packagePath;
            LocalStatePath = localStatePath;
            TempStatePath = tempStatePath;
        }

        internal bool IsPackaged { get; }

        internal string? PackagePath { get; }

        internal string? LocalStatePath { get; }

        internal string? TempStatePath { get; }
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

    internal static IEnumerable<string> EnumerateMauiSpecificPaths(string? baseDirectory, string fileName)
    {
        if (string.IsNullOrEmpty(baseDirectory))
        {
            yield break;
        }

        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool TryAdd(string? candidate, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            normalized = Path.GetFullPath(candidate);
            return emitted.Add(normalized);
        }

        if (IsAndroidPlatform())
        {
            var libDirectory = Path.Combine(baseDirectory, "lib");
            if (TryAdd(Path.Combine(libDirectory, fileName), out var normalized))
            {
                yield return normalized;
            }

            foreach (var abi in GetAndroidAbiCandidates())
            {
                if (string.IsNullOrEmpty(abi))
                {
                    continue;
                }

                if (TryAdd(Path.Combine(libDirectory, abi, fileName), out normalized))
                {
                    yield return normalized;
                }
            }
        }

        if (IsMacCatalystPlatform())
        {
            if (TryAdd(Path.Combine(baseDirectory, fileName), out var normalized))
            {
                yield return normalized;
            }
            if (TryAdd(Path.Combine(baseDirectory, "Frameworks", fileName), out normalized))
            {
                yield return normalized;
            }
            if (TryAdd(Path.Combine(baseDirectory, "..", "Frameworks", fileName), out normalized))
            {
                yield return normalized;
            }
            if (TryAdd(Path.Combine(baseDirectory, "MonoBundle", fileName), out normalized))
            {
                yield return normalized;
            }
            if (TryAdd(Path.Combine(baseDirectory, "..", "MonoBundle", fileName), out normalized))
            {
                yield return normalized;
            }
        }

        if (IsIosPlatform())
        {
            if (TryAdd(Path.Combine(baseDirectory, fileName), out var normalized))
            {
                yield return normalized;
            }
            if (TryAdd(Path.Combine(baseDirectory, "Frameworks", fileName), out normalized))
            {
                yield return normalized;
            }
            if (TryAdd(Path.Combine(baseDirectory, "..", "Frameworks", fileName), out normalized))
            {
                yield return normalized;
            }
        }

        if (OperatingSystem.IsMacOS() && !IsMacCatalystPlatform())
        {
            if (TryAdd(Path.Combine(baseDirectory, "Frameworks", fileName), out var normalized))
            {
                yield return normalized;
            }
            if (TryAdd(Path.Combine(baseDirectory, "..", "Frameworks", fileName), out normalized))
            {
                yield return normalized;
            }
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

    private static IEnumerable<string> GetAndroidAbiCandidates()
    {
        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string? current = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm => "armeabi-v7a",
            Architecture.Arm64 => "arm64-v8a",
            Architecture.X86 => "x86",
            Architecture.X64 => "x86_64",
            _ => null,
        };

        if (!string.IsNullOrEmpty(current) && emitted.Add(current))
        {
            yield return current;
        }

        foreach (var abi in new[] { "arm64-v8a", "armeabi-v7a", "x86_64", "x86" })
        {
            if (emitted.Add(abi))
            {
                yield return abi;
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
            var candidates = new[]
            {
                Path.Combine(current.FullName, "VelloSharp", "bin"),
                Path.Combine(current.FullName, "bindings", "VelloSharp", "bin"),
                Path.Combine(current.FullName, "src", "VelloSharp", "bin"),
            };

            foreach (var candidate in candidates)
            {
                if (!Directory.Exists(candidate))
                {
                    continue;
                }

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

    private static bool IsAndroidPlatform() => _androidOverride ?? OperatingSystem.IsAndroid();

    private static bool IsIosPlatform() => _iosOverride ?? OperatingSystem.IsIOS();

    private static bool IsMacCatalystPlatform() => _macCatalystOverride ?? OperatingSystem.IsMacCatalyst();

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

    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackagePath(ref int length, StringBuilder? path);

    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFamilyName(ref int length, StringBuilder? packageFamilyName);
}
#pragma warning restore CS0436

