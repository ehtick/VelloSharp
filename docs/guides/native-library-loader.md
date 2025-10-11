# Native Library Loader

When a managed package depends on native binaries published in `runtimes/<rid>/native`, use `VelloSharp.NativeLibraryLoader` to register the library names and optional probing paths. The loader installs a process-wide `DllImportResolver` that mirrors the logic used by the core bindings, so the same probing rules apply across all projects. The type is intentionally `internal`; link the source into your project so the helper stays private to the assembly.

## Registering a Library

```csharp
using System.Runtime.CompilerServices;
using VelloSharp;

namespace MyPackage;

internal static class NativeBootstrap
{
#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize()
    {
        NativeLibraryLoader.RegisterNativeLibrary("my_native_lib");
    }
}
```

`RegisterNativeLibrary` automatically normalizes the name, so you can pass either `my_native_lib`, `libmy_native_lib.so`, or `my_native_lib.dll`.

## Registering Multiple Libraries

If the package exposes more than one native binary, register them in one call:

```csharp
NativeLibraryLoader.RegisterNativeLibraries("lib_one", "lib_two");
```

## Custom Probe Locations

To probe additional directories (for example, custom build output during testing), register them before calling into the native code:

```csharp
NativeLibraryLoader.RegisterProbingPath(Path.Combine(repoRoot, "artifacts", "runtimes"));
```

The loader always checks:

- `AppContext.BaseDirectory`
- The consuming assembly directory
- Custom probe paths registered via `RegisterProbingPath`
- Local `VelloSharp/bin/<Configuration>/net8.0` fallback
- `NATIVE_DLL_SEARCH_DIRECTORIES`

## Package Author Checklist

1. Add a bootstrap class with a module initializer that registers the native library names.
2. Link the loader into your project so the internal API is available at build time:

   ```xml
   <ItemGroup>
     <Compile Include="..\..\bindings\VelloSharp\NativeLibraryLoader.cs">
       <Link>Internal\NativeLibraryLoader.cs</Link>
     </Compile>
   </ItemGroup>
   ```

3. Pack the native assets under `runtimes/<rid>/native` in your NuGet package.
4. When running tests that produce native outputs in custom locations, point the loader to them via `RegisterProbingPath`.

Following these steps keeps native loading consistent across Linux, macOS, and Windows without duplicating resolver logic in each project.
