#!/usr/bin/env bash
# Runs the engine tests (real files, real fonts, Google Fonts downloads). Needs .NET 10. Add --offline to skip the network part.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
export IBRACARE_SHAPING_OUT="$(mktemp --suffix=.ttf)"
dotnet run -c Release --project "$here/IbraCare.Tests" -- "$here/data" "$@"
if python3 -c "import uharfbuzz, fontTools" 2>/dev/null; then
  echo; echo "== HarfBuzz shaping of a converted old Arabic font"
  python3 "$here/check-shaping.py" "$IBRACARE_SHAPING_OUT"
else
  echo "(skipping the HarfBuzz check: pip install uharfbuzz fonttools)"
fi
