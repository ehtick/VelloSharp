using System.Linq;
using System.Runtime.InteropServices;

Console.WriteLine("Verifying VelloSharp native win-arm64 packages…");

var expectedLibraries = new[]
{
    "accesskit_ffi.dll",
    "vello_chart_engine.dll",
    "vello_composition.dll",
    "vello_editor_core.dll",
    "vello_gauges_core.dll",
    "kurbo_ffi.dll",
    "peniko_ffi.dll",
    "vello_scada_runtime.dll",
    "vello_tree_datagrid.dll",
    "vello_ffi.dll",
    "vello_sparse_ffi.dll",
    "winit_ffi.dll",
};

NativeLibraryValidator.ValidateAll(expectedLibraries);

Console.WriteLine("win-arm64 native integration test completed.");

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
