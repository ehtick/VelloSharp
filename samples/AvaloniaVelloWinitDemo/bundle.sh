#!/usr/bin/env bash
set -euo pipefail

# Determine the script directory and jump there so the build runs next to the csproj
cd "$(cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd)"

arch="x64"
if [[ $(uname -m) == "arm64" ]]; then
  arch="arm64"
fi

# Restore all dependencies with the correct RuntimeIdentifier before bundling
dotnet restore -r osx-$arch
# Generate the macOS .app bundle using Dotnet.Bundle's MSBuild target
dotnet msbuild -t:BundleApp -p:RuntimeIdentifier=osx-$arch
