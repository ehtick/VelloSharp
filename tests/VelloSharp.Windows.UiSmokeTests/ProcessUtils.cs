using System;
using System.Diagnostics;
using System.IO;

namespace VelloSharp.Tests.Windows;

internal static class ProcessUtils
{
    public static void TerminateProcessTree(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return;
        }

        var processName = Path.GetFileNameWithoutExtension(executablePath);
        if (string.IsNullOrWhiteSpace(processName))
        {
            return;
        }

        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                if (!string.Equals(process.MainModule?.FileName, executablePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(TimeSpan.FromSeconds(5));
                }
            }
            catch
            {
                // Best-effort cleanup; ignore failures.
            }
        }
    }
}
