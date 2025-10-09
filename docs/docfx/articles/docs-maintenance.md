# Documentation Maintenance Guide

Use these commands to build, preview, and tidy the DocFX site when working locally.

## Build the Documentation

```powershell
dotnet tool restore
dotnet tool run docfx docs/docfx/docfx.json
```

The metadata YAML and static site output are generated under `docs/docfx/api` and `docs/docfx/_site`.

## Preview Locally

```powershell
dotnet tool run docfx docs/docfx/docfx.json --serve --port 8080
```

Navigate to `http://localhost:8080` to browse the rendered site with live reload.

## Clean Generated Artifacts

```powershell
Remove-Item -Recurse -Force docs/docfx/_site
Get-ChildItem docs/docfx/api -Filter *.yml | Remove-Item
Remove-Item docs/docfx/api/.manifest -ErrorAction Ignore
```

These commands delete the generated static site and metadata files while leaving the curated markdown pages intact. If you need a pristine tree, run `git clean -fd docs/docfx/_site docs/docfx/api` instead.
