# CI and Release Process

This document captures how the VelloSharp repository builds, tests, and publishes
NuGet packages through GitHub Actions.

## Overview

- All continuous integration (CI) and release automation is defined under
  `.github/workflows/`.
- Pull requests and pushes to `main` run `.github/workflows/ci.yml`, which is a
  thin wrapper that invokes the reusable workflow in
  `.github/workflows/build-pack.yml`.
- Tags matching `v*` trigger `.github/workflows/release.yml`; that workflow
  reuses the same build pipeline and then pushes packages to NuGet.org and
  updates the GitHub Release page.

## Reusable build pipeline

The reusable workflow (`build-pack.yml`) drives every build. Its jobs are
designed to produce native artifacts, assemble NuGet packages, and validate the
resulting packages across the supported operating systems.

### Native build jobs

- Jobs `native-linux`, `native-macos`, `native-android`, `native-wasm`, and
  `native-windows` build the Rust FFI crates for each runtime identifier (RID).
- Each job checks out the repository with submodules, installs the required Rust
  toolchain via `dtolnay/rust-toolchain@stable`, and calls the matching helper
  script (`scripts/build-native-*.{sh,ps1}`) to compile every crate for the
  target RID.
- The build output is staged under `artifacts/runtimes/<rid>/native/` and
  uploaded as an action artifact whose name encodes the RID (for example,
  `native-linux-x64`).

### Native NuGet packaging

- Job `pack-native` waits on all native builds, downloads their artifacts, and
  aggregates the files with `scripts/collect-native-artifacts.sh`.
- It fans the native binaries into the repository layout via
  `scripts/copy-runtimes.sh` so every runtime-specific packaging project has the
  expected `runtimes/<rid>/native` structure.
- `scripts/pack-native-nugets.sh` produces the RID-specific runtime packages and
  the meta packages under `artifacts/nuget/`. The job uploads the resulting
  packages as the `native-nuget-packages` artifact.

### Managed NuGet packaging

- Job `pack-managed` downloads the previously built native packages, registers
  them as a temporary local NuGet source, and runs
  `scripts/pack-managed-nugets.sh`.
- The script skips rebuilding the Rust crates (`VelloSkipNativeBuild=true`) and
  packs every managed project as a NuGet, wiring dependencies to the published
  native packages. The output `artifacts/nuget/` folder is uploaded as
  `nuget-packages`.

### Cross-platform validation

- Job `test-packages` restores and runs integration projects for Linux,
  macOS, and Windows matrices.
- Each matrix entry runs a managed smoke project (`dotnet run`) and a native
  validation project to ensure the freshly created packages can be restored and
  load their native dependencies correctly from the local packages feed.
- The Windows matrix additionally builds the WinUI/UWP bindings (`VelloSharp.WinUI`, `VelloSharp.Uwp`, `VelloSharp.Windows.Shared`) and executes `scripts/verify-winui-native-assets.ps1` / `scripts/verify-uwp-native-assets.ps1` to confirm GPU backends (D3D12/Vulkan/WARP) and AccessKit FFIs are present.

## Release workflow

The release workflow adds publication responsibilities on top of the reusable
build pipeline.

1. The `build` job simply reuses `build-pack.yml`, guaranteeing releases and CI
   share the same build artifacts.
2. The `publish` job downloads the `nuget-packages` artifact and pushes every
   `.nupkg`/`.snupkg` to NuGet.org using `dotnet nuget push`, including the Windows host packages (`VelloSharp.WinUI`, `VelloSharp.Uwp`, `VelloSharp.Windows.Shared`) alongside the existing bindings. The API key is
   provided via the `NUGET_API_KEY` secret; duplicates are ignored to keep
   re-runs idempotent.
3. After NuGet publication succeeds, the workflow queries GitHub for an existing
   release that matches the tag. It skips re-uploading assets that already
   exist, then either updates or creates the GitHub Release with the new
   artifacts and auto-generated release notes.

## Required secrets and configuration

- `NUGET_API_KEY` must be configured in the repository secrets for the release
  workflow to publish packages.
- Releases are initiated by creating and pushing a tag that matches `v*`
  (for example `v0.5.0-alpha.2`). The release workflow derives the version from
  the tag name.
- Both the CI and release workflows expose `workflow_dispatch`; maintainers can
  run them manually from the Actions tab when troubleshooting or preparing dry
  runs.

## Local reproduction

- Run the helper scripts directly (`scripts/build-native-*.sh`,
  `scripts/pack-native-nugets.sh`, `scripts/pack-managed-nugets.sh`) to mimic
  each stage locally. These are the same entry points the workflows execute.
- The integration harness under `integration/` mirrors what the `test-packages`
  job runs; invoking one of the `integration/.../*.csproj` projects with
  `dotnet run -c Release` validates that a local package drop behaves the same
  as the CI output.
