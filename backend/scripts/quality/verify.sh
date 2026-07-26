#!/usr/bin/env bash
# Single quality verify entrypoint for bugget-api.
# Реальная логика — в verify.py; этот файл — тонкая bash-обёртка для
# совместимости с документацией и привычным `bash scripts/quality/verify.sh`.
#
# Usage:
#   scripts/quality/verify.sh                           # все включённые gates
#   scripts/quality/verify.sh --fast                    # без slow gates (~30 сек)
#   scripts/quality/verify.sh --only backend-format     # один gate
#   scripts/quality/verify.sh --skip backend-test-integration
#   scripts/quality/verify.sh --list                    # перечислить gates
#   scripts/quality/verify.sh --dry-run                 # план без запуска
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
exec python3 "$ROOT/scripts/quality/verify.py" "$@"
