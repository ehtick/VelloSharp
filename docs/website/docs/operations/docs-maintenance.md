# Documentation Maintenance Guide

The documentation stack now uses DocFX to emit Markdown reference files that are hosted inside the Docusaurus site. These commands keep the generated API pages in sync and run the local preview.

## Sync the .NET API Markdown

```bash
./scripts/sync-dotnet-api-docs.sh
```

```powershell
pwsh ./scripts/sync-dotnet-api-docs.ps1
```

Both scripts restore the DocFX tool, generate metadata in Markdown form, and copy the results into `docs/website/generated/dotnet-api/`. The helper copies the curated `docs/docfx/api/index.md` landing page so the API section always has a stable entry point.

You can also trigger the sync from within the website directory:

```bash
cd docs/website
npm run sync:dotnet-api
```

The `prestart` and `prebuild` hooks run the same sync automatically before `npm start` and `npm run build`.

## Preview the Site Locally

```bash
./scripts/run-docs-site.sh --port 3000
```

```powershell
pwsh ./scripts/run-docs-site.ps1 -Port 3000
```

These helpers first refresh the API Markdown (unless `--no-sync`/`-NoSync` is supplied) and then launch the Docusaurus dev server on `http://localhost:3000`. Hot reload works for both curated docs and regenerated API files.

## Build the Static Site

```bash
cd docs/website
npm run build
```

The `prebuild` hook ensures the API Markdown is up to date before bundling the production site into `docs/website/build/`.

## Clean Generated Artifacts

```powershell
Remove-Item -Recurse -Force docs/docfx/obj
Remove-Item -Recurse -Force docs/website/generated/dotnet-api
Remove-Item -Recurse -Force docs/website/build
```

These commands delete DocFX intermediates, the copied Markdown, and the compiled static site. Run `git clean -fdX docs/docfx/obj docs/website/generated` if you need a pristine tree.
