#!/usr/bin/env bash
#
# Перегенерация TypeScript-типов фронтенда из OpenAPI-контрактов.
#
# Источник правды — specs/contracts/<module>/openapi.yaml, тот же, из которого
# генерируется C# (scripts/quality/openapi-generate.sh). Один контракт — две
# генерации: серверные базы контроллеров и клиентские типы. Расхождение фронта
# с бекендом становится ошибкой типов, а не сюрпризом в рантайме (ADR-0005).
#
# Результат ложится в frontend/src/shared/api/generated/<module>.d.ts. Эти файлы
# только генерируются: правки в них перетираются следующим прогоном. Они
# исключены из prettier (frontend/.prettierignore), eslint (frontend/eslint.config.js)
# и LOC-бюджета (.quality/frontend-loc.json).
#
# specs/contracts/shared.yaml отдельным файлом не генерируется: модули ссылаются
# на него относительным $ref, и openapi-typescript инлайнит общие схемы в каждый
# модуль. Отдельный файл потребовал бы ручной сшивки импортов в сгенерированном
# коде — ровно того, что запрещено.
#
# Использование:
#   scripts/quality/frontend-openapi-generate.sh            # все модули
#   scripts/quality/frontend-openapi-generate.sh reports    # только указанные
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONTRACTS="$ROOT/specs/contracts"
FRONTEND="$ROOT/frontend"
OUT_DIR="$FRONTEND/src/shared/api/generated"

if [ "$#" -gt 0 ]; then
  modules=("$@")
else
  modules=()
  for spec in "$CONTRACTS"/*/openapi.yaml; do
    modules+=("$(basename "$(dirname "$spec")")")
  done
fi

[ ${#modules[@]} -gt 0 ] || { printf 'error: в %s не нашлось ни одного openapi.yaml\n' "$CONTRACTS" >&2; exit 2; }

# Версия openapi-typescript закреплена в frontend/package-lock.json: другая версия
# меняет раскладку .d.ts, и дифф гейта перестаёт быть нулевым.
[ -d "$FRONTEND/node_modules" ] || { printf 'error: нет frontend/node_modules — прогоните npm ci в frontend\n' >&2; exit 2; }

mkdir -p "$OUT_DIR"
cd "$FRONTEND"

for module in "${modules[@]}"; do
  spec="$CONTRACTS/$module/openapi.yaml"
  if [ ! -f "$spec" ]; then
    printf 'error: нет контракта %s\n' "$spec" >&2
    exit 2
  fi
  printf '==> %s\n' "$module"
  npx --no-install openapi-typescript "$spec" -o "$OUT_DIR/$module.d.ts"
done
