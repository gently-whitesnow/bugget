#!/usr/bin/env bash
#
# Перегенерация C#-кода из OpenAPI-контрактов.
#
# Источник правды — specs/contracts/<module>/openapi.yaml. Файлы *.g.cs только
# генерируются: правки в них перетираются следующим прогоном (см. ADR-0005).
#
# Конфиги NSwag лежат в backend/nswag.<module>.json и запускаются из backend/.
# nswag.template.json — шаблон для нового модуля, он намеренно пропускается.
#
# Использование:
#   scripts/quality/openapi-generate.sh            # перегенерировать все модули
#   scripts/quality/openapi-generate.sh analytics  # только указанные модули
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
BACKEND="$ROOT/backend"

cd "$ROOT"

# Версия NSwag зафиксирована в .config/dotnet-tools.json: другая версия тулчейна
# меняет заголовок и раскладку .g.cs, и дифф перестаёт быть нулевым.
dotnet tool restore >/dev/null

if [ "$#" -gt 0 ]; then
  modules=("$@")
else
  modules=()
  for config in "$BACKEND"/nswag.*.json; do
    name="$(basename "$config")"
    name="${name#nswag.}"
    name="${name%.json}"
    [ "$name" = "template" ] && continue
    modules+=("$name")
  done
fi

cd "$BACKEND"

for module in "${modules[@]}"; do
  config="nswag.$module.json"
  if [ ! -f "$config" ]; then
    printf 'error: нет конфига %s\n' "$BACKEND/$config" >&2
    exit 2
  fi
  printf '==> %s\n' "$config"
  dotnet nswag run "$config"
done
