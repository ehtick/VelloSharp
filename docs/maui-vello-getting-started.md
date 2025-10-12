# Getting Started with VelloSharp on .NET MAUI

The MAUI integration packages expose `VelloView` so you can host Vello swapchains inside .NET MAUI heads.

## Prerequisites

1. Install the .NET MAUI workloads:

   ```powershell
   dotnet workload restore maui
   ```

2. Restore native runtimes by either building the FFI crates or installing the published `VelloSharp.Native.*` packages.

3. On Windows install the MSVC build tools (`bootstrap-windows.ps1` handles the prerequisites). On macOS install the Xcode Command Line Tools (`bootstrap-macos.sh`).

## Building the sample gallery

```
# Windows (PowerShell)
pwsh ./scripts/build-samples.ps1 -Configuration Release

# Windows (bash)
./scripts/build-samples.sh -c Release
```

The scripts automatically target `net8.0-windows10.0.19041` for `samples/MauiVelloGallery/MauiVelloGallery.csproj` so the Windows head builds even when other workloads are unavailable.

## Verify native assets

After publishing, confirm the MAUI bundle contains the expected native libraries:

```powershell
pwsh ./scripts/verify-maui-native-assets.ps1 -BundlePath artifacts/publish/android-arm64 -Platform android
pwsh ./scripts/verify-maui-native-assets.ps1 -BundlePath artifacts/publish/maccatalyst -Platform maccatalyst
```

The script reports missing `dll`/`so`/`dylib` payloads before you ship the build.

## Handler tips

- Toggle `SuppressGraphicsViewCompositor="True"` on `VelloView` to disable the fallback Skia compositor when Vello owns the swapchain on Windows MAUI heads.
- Use `IsDiagnosticsEnabled` to surface frame timing through the diagnostics overlay. The presenter honours the toggle and resets the tracking state when diagnostics are disabled.

## Troubleshooting

- If `dotnet workload restore maui` fails, ensure Visual Studio 17.9+ with the MAUI workloads is installed or install the workloads via `dotnet workload install`.
- When native libraries fail to load on devices, confirm `NativeLibraryLoader` can find them by checking the bundle layout described above.

