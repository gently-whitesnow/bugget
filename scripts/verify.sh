#!/usr/bin/env bash
#
# Сборка, тесты, контракты и линтеры. Рамку репозитория проверяет `harness check`.
#
#   scripts/verify.sh            # всё
#   scripts/verify.sh backend    # только бекенд (интеграционным тестам нужен Docker)
#   scripts/verify.sh frontend   # только фронтенд
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCOPE="${1:-all}"

step() { printf '\n== %s ==\n' "$*"; }

verify_backend() {
  cd "$ROOT/backend"
  step "backend: build";        dotnet build Bugget.slnx -c Release
  step "backend: format";       dotnet format Bugget.slnx --verify-no-changes --no-restore
  step "backend: tests";        dotnet test Bugget.slnx -c Release --no-build
  cd "$ROOT"
  step "backend: contracts";    scripts/contracts/openapi-check.sh
  step "realtime contract";     python3 scripts/contracts/realtime-contract.py
                                python3 scripts/contracts/realtime-contract.py --self-test
}

verify_frontend() {
  cd "$ROOT/frontend"
  [ -d node_modules ] && [ ! package-lock.json -nt node_modules ] || npm ci
  cd "$ROOT"
  step "frontend: contracts";   scripts/contracts/frontend-openapi-check.sh
                                scripts/contracts/frontend-openapi-check.sh --self-test
  step "frontend: api inventory"; node scripts/contracts/frontend-api-inventory.mjs
                                node scripts/contracts/frontend-api-inventory.mjs --self-test
  step "realtime contract";     python3 scripts/contracts/realtime-contract.py
                                python3 scripts/contracts/realtime-contract.py --self-test
  cd "$ROOT/frontend"
  step "frontend: format";      npm run format:check
  step "frontend: typecheck";   npx tsc --noEmit
  step "frontend: lint";        npm run lint
  step "frontend: tests";       npm run test
  step "frontend: architecture"; npx steiger ./src --fail-on-warnings
  step "frontend: build";       npx vite build
  step "frontend: audit";       npm audit --audit-level=high
}

case "$SCOPE" in
  backend)  verify_backend ;;
  frontend) verify_frontend ;;
  all)      verify_backend; verify_frontend ;;
  *)        echo "usage: scripts/verify.sh [backend|frontend]" >&2; exit 2 ;;
esac

printf '\nverify: ok (%s)\n' "$SCOPE"
