# Script Reference

This folder hosts the automation scripts that back the CI pipeline and simplify local development. Unless noted
otherwise, run the scripts from the repository root so paths resolve correctly. Bash scripts assume a POSIX shell
(`bash` 4+) and PowerShell scripts target `pwsh` 7 or newer.

## Bootstrap

### `bootstrap-linux.sh`
- **Usage**: `./bootstrap-linux.sh`
- **Description**: Validates that the script runs on Linux, ensures `curl` is available, and installs the minimal
  `rustup` toolchain when neither `cargo` nor `rustup` is detected. Emits a reminder to add `~/.cargo/bin` to `PATH`.

### `bootstrap-macos.sh`
- **Usage**: `./bootstrap-macos.sh`
- **Description**: macOS bootstrapper that checks for the Xcode Command Line Tools, verifies the .NET SDK, and installs
  `rustup` if necessary. Aborts with guidance if the Command Line Tools prompt is still pending.

### `bootstrap-windows.ps1`
- **Usage**: `pwsh ./bootstrap-windows.ps1`
- **Description**: Windows bootstrapper that must be run from an elevated PowerShell session. Confirms administrator
  rights, checks for the .NET SDK, and installs the minimal `rustup` toolchain when the Rust environment is missing.

## Native builds

All native build scripts enumerate the FFI crates under `ffi/`, build the set that produces shared/static libraries,
and copy the resulting binaries into `artifacts/runtimes/<rid>/native/`.

### `build-native-linux.sh`
- **Usage**: `./build-native-linux.sh [target] [profile] [rid]`
- **Defaults**: `target=x86_64-unknown-linux-gnu`, `profile=release`, RID inferred from the target triple.
- **Description**: Cross-compiles the FFI crates for GNU/Linux targets. Accepts any installed Rust triple (for example
  `aarch64-unknown-linux-gnu`) and an optional explicit RID override.

### `build-native-macos.sh`
- **Usage**: `./build-native-macos.sh [target] [profile] [sdk] [rid]`
- **Defaults**: `target=x86_64-apple-darwin`, `profile=release`, SDK unset, RID inferred from the target triple.
- **Description**: Builds the macOS/iOS FFI binaries. Pass an Apple SDK name such as `macosx`, `iphoneos`, or
  `iphonesimulator` when targeting non-default SDKs. For iOS targets the script emits `.a` static archives.

### `build-native-windows.ps1`
- **Usage**: `pwsh ./build-native-windows.ps1 [-Target <triple>] [-Profile <profile>] [-Rid <rid>]`
- **Defaults**: `Target=x86_64-pc-windows-msvc`, `Profile=release`, RID inferred from the target triple.
- **Description**: Wraps the Windows MSVC builds, producing `.dll` assets for each FFI crate and copying them into the
  runtime layout. Supports `aarch64-pc-windows-msvc` and arbitrary custom profiles.

### `build-native-android.sh`
- **Usage**: `./build-native-android.sh [target] [profile] [rid]`
- **Defaults**: `target=aarch64-linux-android`, `profile=release`, `rid=android-arm64`.
- **Description**: Builds the Android (`.so`) payloads using the configured NDK. Requires `ANDROID_NDK_HOME` and adds
  the appropriate LLVM toolchain to `PATH` during the build.

### `build-native-wasm.sh`
- **Usage**: `./build-native-wasm.sh [target] [profile] [rid]`
- **Defaults**: `target=wasm32-unknown-unknown`, `profile=release`, `rid=browser-wasm`.
- **Description**: Compiles the WebAssembly variants of the FFI crates, copying either the produced `.wasm` artifact or
  the fallback static library into the runtime directory.

## Managed builds

### `build-integrations.sh` / `build-integrations.ps1`
- **Usage (bash)**: `./build-integrations.sh [-c <cfg>] [-f <tfm>] [-- ...dotnet args...]`
- **Usage (PowerShell)**: `pwsh ./build-integrations.ps1 [-Configuration <cfg>] [-Framework <tfm>] [-DotNetArgument <args...>]`
- **Defaults**: Configuration `Release`, framework unset, additional `dotnet` arguments empty. Both scripts auto-discover every `.csproj`
  under `integration/` and skip Windows/macOS/Linux-specific projects when run on unsupported hosts.
- **Description**: Bulk builds the managed and native integration projects, forwarding any extra arguments directly
  to `dotnet build`. Useful for quick validation after modifying package metadata or the runtime copy layout.

### `build-samples.sh` / `build-samples.ps1`
- **Usage (bash)**: `./build-samples.sh [-c <cfg>] [-f <tfm>] [-- ...dotnet args...]`
- **Usage (PowerShell)**: `pwsh ./build-samples.ps1 [-Configuration <cfg>] [-Framework <tfm>] [-DotNetArgument <args...>]`
- **Defaults**: Configuration `Release`, framework unset, additional arguments empty. Automatically skips platform-specific samples
  (for example WinForms/WPF on non-Windows hosts).
- **Description**: Iterates over every sample project beneath `samples/`, invoking `dotnet build` with the requested configuration
  and target framework. Ideal for verifying template/sample health without opening each project manually.

## Artifact collection and propagation

### `collect-native-artifacts.sh`
- **Usage**: `./collect-native-artifacts.sh [source-dir] [dest-dir]`
- **Defaults**: `source-dir=artifacts`, `dest-dir=artifacts/runtimes`.
- **Description**: Scans the source tree for `native/` directories, maps folder names to runtime identifiers, and
  normalises each payload into `dest-dir/<rid>/native/`. Primarily used in CI prior to packing.

### `copy-runtimes.sh` / `copy-runtimes.ps1`
- **Usage (bash)**: `./copy-runtimes.sh [artifacts-dir] [targets...]`
- **Usage (PowerShell)**: `pwsh ./copy-runtimes.ps1 [-ArtifactsDir <path>] [-Targets <paths...>]`
- **Defaults**: `artifacts-dir=artifacts/runtimes`, targets covering the main libraries, integrations, and samples.
- **Description**: Propagates the collected runtimes into the `bin/<configuration>/<tfm>/runtimes` folders of each
  target project and synchronises the assets into `packaging/VelloSharp.Native.*`. Honour the
  `COPY_CONFIGURATIONS`/`COPY_TARGET_FRAMEWORKS` environment variables (or their PowerShell counterparts) to override
  the build configurations and target frameworks.

### `remove-runtimes.sh` / `remove-runtimes.ps1`
- **Usage (bash)**: `./remove-runtimes.sh [targets...]`
- **Usage (PowerShell)**: `pwsh ./remove-runtimes.ps1 [-Targets <paths...>]`
- **Defaults**: Targets mirror the default set used by `copy-runtimes`.
- **Description**: Deletes `runtimes/` folders from project roots and from `bin/<configuration>/<tfm>/runtimes` to keep
  working trees clean. Respect the `REMOVE_RUNTIMES_CONFIGURATIONS` and `REMOVE_RUNTIMES_TARGET_FRAMEWORKS` environment
  variables to customise the sweep.

## Packaging

### `pack-native-nugets.sh` / `pack-native-nugets.ps1`
- **Usage (bash)**: `./pack-native-nugets.sh [runtimes-dir] [output-dir]`
- **Usage (PowerShell)**: `pwsh ./pack-native-nugets.ps1 [-RuntimesRoot <path>] [-OutputDir <path>]`
- **Defaults**: `runtimes-dir=artifacts/runtimes`, `output-dir=artifacts/nuget`.
- **Description**: Iterates each RID folder under the runtimes directory and packs the corresponding
  `VelloSharp.Native.<Component>.<rid>` NuGet packages with `dotnet pack`.

### `pack-managed-nugets.sh` / `pack-managed-nugets.ps1`
- **Usage (bash)**: `./pack-managed-nugets.sh [output-dir] [native-feed]`
- **Usage (PowerShell)**: `pwsh ./pack-managed-nugets.ps1 [-OutputDir <path>] [-NativeFeed <path>]`
- **Defaults**: `output-dir=artifacts/nuget`, `native-feed=<output-dir>`.
- **Description**: Builds the managed solution in `Release`, registers a temporary NuGet feed pointing at the native
  packages, and packs every managed component. Populate the feed with `pack-native-nugets` before invoking this script.

## Documentation

### `build-docs.sh` / `build-docs.ps1`
- **Usage (bash)**: `./build-docs.sh [docfx-args...]`
- **Usage (PowerShell)**: `pwsh ./build-docs.ps1 [-DocFxArgs <args...>]`
- **Defaults**: No additional DocFX arguments. Both scripts restore local tools automatically.
- **Description**: Generates the DocFX site with `EnableWindowsTargeting=true` so Windows-targeted assemblies build on
  non-Windows hosts. Pass extra DocFX arguments to tweak the build (for example `--serve`).

## Diagnostics

### `report_skia_usage.sh`
- **Usage**: `./report_skia_usage.sh [output-file]`
- **Description**: Crawls the Avalonia Skia sources to catalog `SK*` symbols used by the SkiaSharp integration layers.
  Writes a CSV report to stdout or to the provided output file.

## Integration validation

### `run-integration-tests.sh` / `run-integration-tests.ps1`
- **Usage (bash)**: `./run-integration-tests.sh [--configuration <cfg>] [--framework <tfm>] [--platform <linux|macos|windows>] [--managed-only|--native-only]`
- **Usage (PowerShell)**: `pwsh ./run-integration-tests.ps1 [-Configuration <cfg>] [-Framework <tfm>] [-Platform <linux|macos|windows>] [-ManagedOnly] [-NativeOnly]`
- **Defaults**: Configuration `Release`, framework unset, platform detected from the host OS.
- **Description**: Executes every managed integration console and the native RID validation project for the requested
  platform. Use `--platform`/`-Platform` to target a different RID set, and supply `--managed-only` or `--native-only`
  to restrict execution.
