#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
# Без .editorconfig dotnet format почти ничего не делает — это ожидаемо
# до выполнения шага 3 топ-5 (добавить .editorconfig + Directory.Packages.props).
dotnet format Bugget.sln --verify-no-changes --verbosity minimal
