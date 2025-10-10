# Documentation Maintenance Guide

Use these commands to build, preview, and tidy the DocFX site when working locally.

## Build the Documentation

```bash
./scripts/build-docs.sh
```

```powershell
pwsh ./scripts/build-docs.ps1
```

The metadata YAML and static site output are generated under `docs/docfx/api` and `docs/docfx/_site`.
DocFX now sets `EnableWindowsTargeting=true` via `docs/docfx/docfx.json`, and the helper scripts forward the same MSBuild switch so Windows-specific assemblies build on any host OS.
The metadata step auto-discovers all packable project files under `bindings/` and `src/`, so new libraries are included without editing `docfx.json`.

## Preview Locally

```bash
./scripts/build-docs.sh --serve --port 8080
```

```powershell
pwsh ./scripts/build-docs.ps1 -DocFxArgs @('--serve', '--port', '8080')
```

Navigate to `http://localhost:8080` to browse the rendered site with live reload.

## Clean Generated Artifacts

```powershell
Remove-Item -Recurse -Force docs/docfx/_site
Get-ChildItem docs/docfx/api -Filter *.yml | Remove-Item
Remove-Item docs/docfx/api/.manifest -ErrorAction Ignore
```

These commands delete the generated static site and metadata files while leaving the curated markdown pages intact. If you need a pristine tree, run `git clean -fd docs/docfx/_site docs/docfx/api` instead.
