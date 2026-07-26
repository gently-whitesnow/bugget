#!/usr/bin/env bash
# Usage:
#   scripts/quality/dotnet-test.sh                    # все тесты (unit + integration)
#   scripts/quality/dotnet-test.sh --unit-only        # только Bugget.Tests (без Docker)
#   scripts/quality/dotnet-test.sh --integration-only # только Bugget.IntegrationTests (требует Docker)
#   scripts/quality/dotnet-test.sh --project <path>   # произвольный csproj
#
# Тесты разделены проектами (csproj), а не категориями — поэтому фильтр идёт
# на уровне dotnet test <project>, а не --filter Category=...
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

UNIT_PROJECT="Bugget.Tests/Bugget.Tests.csproj"
INTEGRATION_PROJECT="Bugget.IntegrationTests/Bugget.IntegrationTests.csproj"

PROJECT=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --unit-only)
      PROJECT="$UNIT_PROJECT"
      shift
      ;;
    --integration-only)
      PROJECT="$INTEGRATION_PROJECT"
      shift
      ;;
    --project)
      PROJECT="${2:-}"
      shift 2
      ;;
    --project=*)
      PROJECT="${1#--project=}"
      shift
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

if [[ -n "$PROJECT" ]]; then
  exec dotnet test "$PROJECT" -c Release --nologo
fi

# Default: оба проекта по очереди (через sln, чтобы dotnet сам нашёл *.Tests).
exec dotnet test Bugget.sln -c Release --nologo
