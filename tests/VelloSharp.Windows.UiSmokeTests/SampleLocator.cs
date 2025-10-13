using System;
using System.IO;

namespace VelloSharp.Tests.Windows;

internal static class SampleLocator
{
    private const string SolutionFileName = "VelloSharp.sln";

    public static string GetRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(Path.Combine(directory, SolutionFileName)))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException($"Unable to locate repository root. Expected to find '{SolutionFileName}' in a parent directory of '{AppContext.BaseDirectory}'.");
    }

    public static string GetSampleExecutable(string relativeProjectPath, string executableName, string targetFramework = "net8.0-windows10.0.19041")
    {
        var root = GetRepositoryRoot();
        var configuration = Environment.GetEnvironmentVariable("BUILD_CONFIGURATION");
        if (string.IsNullOrWhiteSpace(configuration))
        {
            configuration = "Release";
        }

        var candidates = new[]
        {
            Path.Combine(root, relativeProjectPath, "bin", configuration, targetFramework, "win10-x64", executableName),
            Path.Combine(root, relativeProjectPath, "bin", configuration, targetFramework, "win-x64", executableName),
            Path.Combine(root, relativeProjectPath, "bin", configuration, targetFramework, executableName),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            $"Sample executable '{executableName}' was not found. Build '{relativeProjectPath}' in the '{configuration}' configuration before running UI smoke tests.",
            Path.Combine(root, relativeProjectPath));
    }
}
