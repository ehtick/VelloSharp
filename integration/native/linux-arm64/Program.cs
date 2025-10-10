using System.Linq;
using System.Runtime.InteropServices;

Console.WriteLine("Verifying VelloSharp native linux-arm64 packages…");

var expectedLibraries = new[]
{
    "libaccesskit_ffi.so",
    "libvello_chart_engine.so",
    "libvello_composition.so",
    "libvello_editor_core.so",
    "libvello_gauges_core.so",
    "libkurbo_ffi.so",
    "libpeniko_ffi.so",
    "libvello_scada_runtime.so",
    "libvello_tree_datagrid.so",
    "libvello_ffi.so",
    "libvello_sparse_ffi.so",
    "libwinit_ffi.so",
};

NativeLibraryValidator.ValidateAll(expectedLibraries);

Console.WriteLine("linux-arm64 native integration test completed.");

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
