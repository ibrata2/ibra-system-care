#!/usr/bin/env bash
# Runs the engine tests (real files, real fonts, Google Fonts downloads). Needs .NET 10. Add --offline to skip the network part.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
dotnet run -c Release --project "$here/IbraCare.Tests" -- "$here/data" "$@"
