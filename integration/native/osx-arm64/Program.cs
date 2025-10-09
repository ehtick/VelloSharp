using System.Linq;
using System.Runtime.InteropServices;

Console.WriteLine("Verifying VelloSharp native osx-arm64 packages…");

var expectedLibraries = new[]
{
    "libaccesskit_ffi.dylib",
    "libvello_chart_engine.dylib",
    "libvello_composition.dylib",
    "libvello_editor_core.dylib",
    "libvello_gauges_core.dylib",
    "libkurbo_ffi.dylib",
    "libpeniko_ffi.dylib",
    "libvello_scada_runtime.dylib",
    "libvello_tree_datagrid.dylib",
    "libvello_ffi.dylib",
    "libvello_sparse_ffi.dylib",
    "libwinit_ffi.dylib",
};

NativeLibraryValidator.ValidateAll(expectedLibraries);

Console.WriteLine("osx-arm64 native integration test completed.");

static class NativeLibraryValidator
{
    public static void ValidateAll(IEnumerable<string> libraryNames)
    {
        foreach (var library in libraryNames)
        {
            var path = FindLibrary(library) ??
                throw new InvalidOperationException($"Native library '{library}' was not copied to the output.");

            Console.WriteLine($"Loading '{path}'…");
            var handle = NativeLibrary.Load(path);
            try
            {
                if (handle == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"Native library '{library}' returned a null handle.");
                }
            }
            finally
            {
                NativeLibrary.Free(handle);
            }
        }
    }

    private static string? FindLibrary(string library)
    {
        return Directory.EnumerateFiles(AppContext.BaseDirectory, library, SearchOption.AllDirectories)
            .FirstOrDefault();
    }
}
