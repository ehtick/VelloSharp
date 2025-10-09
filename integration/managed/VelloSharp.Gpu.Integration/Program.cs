using System.Reflection;
using VelloSharp;

Console.WriteLine("Verifying VelloSharp.Gpu package usage…");

IntegrationAssertions.AssertReferencedType(typeof(AccessKitActionRequest));
IntegrationAssertions.AssertAssemblyLoad("VelloSharp.Gpu");

Console.WriteLine("VelloSharp.Gpu integration test completed.");

static class IntegrationAssertions
{
    public static void AssertReferencedType(Type type)
    {
        Console.WriteLine($"Discovered type: {type.FullName}");
        Console.WriteLine($"Assembly location: {type.Assembly.Location}");
    }

    public static void AssertAssemblyLoad(string assemblyName)
    {
        var assembly = Assembly.Load(assemblyName);
        Console.WriteLine($"Loaded assembly '{assembly.FullName}' from '{assembly.Location}'.");
    }
}
