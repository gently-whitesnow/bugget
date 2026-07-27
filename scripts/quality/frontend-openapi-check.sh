#!/usr/bin/env bash
#
# Гейт frontend-contracts: типы фронта обязаны совпадать с контрактом.
#
# Зеркало backend-гейта openapi-check.sh для клиентской половины генерации.
# Перегенерирует frontend/src/shared/api/generated/*.d.ts из
# specs/contracts/**/openapi.yaml и сравнивает с тем, что лежит в репозитории.
# Непустой дифф — красный гейт.
#
# Ловит три случая:
#   * сгенерированный .d.ts правили руками — правка не воспроизводится генератором;
#   * контракт поменяли, а генерацию не прогнали — типы фронта отстали от yaml;
#   * завели новый модуль контракта, а типы к нему не сгенерировали.
#
# Лишний .d.ts, которому больше не соответствует ни один контракт, тоже красный:
# иначе удалённый модуль контракта оставлял бы за собой типы-призраки.
#
# То, что фронт реально использует эти типы, проверяет не этот гейт, а
# frontend-typecheck: рукописные DTO выведены из сгенерированных
# (frontend/src/shared/api/contracts/wire.ts), поэтому изменение контракта
# ломает компиляцию в местах обращения.
#
# Файлы после прогона остаются перегенерированными: если гейт красный, дифф уже
# в рабочем дереве, и его видно обычным `git diff`.
#
# Использование:
#   scripts/quality/frontend-openapi-check.sh
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUT_DIR="$ROOT/frontend/src/shared/api/generated"

before="$(mktemp -d)"
trap 'rm -rf "$before"' EXIT

shopt -s nullglob
for src in "$OUT_DIR"/*.d.ts; do
  cp "$src" "$before/$(basename "$src")"
done
shopt -u nullglob

bash "$ROOT/scripts/quality/frontend-openapi-generate.sh"

failed=0
checked=0

for src in "$OUT_DIR"/*.d.ts; do
  name="$(basename "$src")"
  snapshot="$before/$name"
  checked=$((checked + 1))

  if [ ! -f "$snapshot" ]; then
    printf 'дифф: frontend/src/shared/api/generated/%s не был закоммичен — генератор создал его сейчас\n' "$name" >&2
    failed=1
    continue
  fi

  if ! diff -u "$snapshot" "$src" >/dev/null; then
    printf '\n== дифф: frontend/src/shared/api/generated/%s ==\n' "$name" >&2
    diff -u "$snapshot" "$src" | head -80 >&2
    failed=1
  fi
done

# Файл, который лежал до прогона, но не появился после: контракт модуля удалён
# или переименован, а сгенерированный остаток забыли убрать.
for snapshot in "$before"/*.d.ts; do
  [ -e "$snapshot" ] || continue
  name="$(basename "$snapshot")"
  if [ ! -f "$OUT_DIR/$name" ]; then
    printf 'дифф: frontend/src/shared/api/generated/%s не соответствует ни одному контракту — удалите файл\n' "$name" >&2
    failed=1
  fi
done

if [ "$checked" -eq 0 ]; then
  printf 'error: генератор не создал ни одного файла — проверьте specs/contracts\n' >&2
  exit 2
fi

if [ "$failed" -ne 0 ]; then
  cat >&2 <<'EOF'

Типы фронта разошлись с контрактом.

Если правили generated/*.d.ts руками — не надо: источник правды
specs/contracts/**/openapi.yaml (ADR-0005).
Если правили контракт — прогоните scripts/quality/frontend-openapi-generate.sh
и закоммитьте результат вместе с yaml (перегенерация уже сделана этим гейтом,
дифф в рабочем дереве).
EOF
  exit 1
fi

printf 'типы фронта совпадают с контрактом (%d файл(ов))\n' "$checked"
