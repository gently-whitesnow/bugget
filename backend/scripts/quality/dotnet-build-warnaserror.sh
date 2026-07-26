#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
# -warnaserror включает TreatWarningsAsErrors для каждой компиляции.
# Legacy исключения (nullable / XML doc / etc.) — в Directory.Build.props
# через <WarningsNotAsErrors>. Чистим список постепенно.
dotnet build Bugget.sln -c Release -warnaserror --nologo
