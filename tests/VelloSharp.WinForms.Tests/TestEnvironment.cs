using System;
using Xunit.Sdk;

namespace VelloSharp.WinForms.Tests;

internal static class TestEnvironment
{
    public static bool IsCi =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CI")) ||
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TF_BUILD")) ||
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

    public static void SkipIfCi(string reason)
    {
        if (IsCi)
        {
            throw SkipException.ForSkip(reason);
        }
    }
}
