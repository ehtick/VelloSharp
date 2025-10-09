using System.Reflection;
using VelloSharp;

Console.WriteLine("Verifying VelloSharp package usage…");

IntegrationAssertions.AssertReferencedType(typeof(RendererOptions));
IntegrationAssertions.AssertReferencedType(typeof(RgbaColor));
IntegrationAssertions.AssertAssemblyLoad("VelloSharp");

Console.WriteLine("VelloSharp package integration test completed.");

static class IntegrationAssertions
{
    public static void AssertReferencedType(Type type)
    {
        Console.WriteLine($"Resolved type '{type.FullName}' from '{type.Assembly.Location}'.");
    }

    public static void AssertAssemblyLoad(string assemblyName)
    {
        var assembly = Assembly.Load(assemblyName);
        Console.WriteLine($"Loaded assembly '{assembly.FullName}' from '{assembly.Location}'.");
    }
}
