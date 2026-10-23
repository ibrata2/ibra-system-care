#!/usr/bin/env bash
# Checks on Linux that the WPF app's code compiles, against WPF reference assemblies built from the official
# github.com/dotnet/wpf sources (ref/*.cs). Needs .NET 10 and git. The real app is still built on Windows.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
refs="${WPFREF_DIR:-$HOME/wpfref}"
if [ ! -f "$refs/bin/Release/net10.0/WpfAll.dll" ]; then
  mkdir -p "$refs"
  [ -d "$refs/wpf" ] || git clone --depth 1 --filter=blob:none --sparse https://github.com/dotnet/wpf.git "$refs/wpf"
  (cd "$refs/wpf" && git sparse-checkout set --no-cone '**/ref/*.cs')
  cp "$here/typecheck/WpfAll.csproj.txt" "$refs/WpfAll.csproj"
  cp "$here/typecheck/Stubs.cs.txt" "$refs/Stubs.cs"
  dotnet build -c Release "$refs/WpfAll.csproj" -p:WpfSrc="$refs/wpf/src/Microsoft.DotNet.Wpf/src"
fi
dotnet build -c Release "$here/typecheck/Typecheck.csproj" -p:WpfRef="$refs/bin/Release/net10.0/WpfAll.dll"
