#!/usr/bin/env bash
#
# Гейт backend-contracts: сгенерированный код обязан совпадать с контрактом.
#
# Что делает: перегенерирует всё из specs/contracts/**/openapi.yaml и сравнивает
# результат с тем, что лежит в репозитории. Непустой дифф — красный гейт.
#
# Ловит два случая:
#   * `.g.cs` правили руками — правка не воспроизводится генератором;
#   * контракт поменяли, а генерацию не прогнали — код отстал от yaml.
#
# Расхождение контроллера с контрактом ловится раньше и без этого скрипта:
# контроллеры наследуют сгенерированные абстрактные базы, поэтому лишний,
# пропавший или переименованный метод — ошибка компиляции.
#
# Файлы после прогона остаются перегенерированными: если гейт красный, дифф уже
# в рабочем дереве, и его видно обычным `git diff`.
#
# Использование:
#   scripts/quality/openapi-check.sh
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
BACKEND="$ROOT/backend"

command -v jq >/dev/null 2>&1 || { printf 'error: нужен jq\n' >&2; exit 2; }

# Пути генерации объявлены в конфигах NSwag — второй раз здесь их не перечисляем.
outputs=()
for config in "$BACKEND"/nswag.*.json; do
  [ "$(basename "$config")" = "nswag.template.json" ] && continue
  while IFS= read -r output; do
    [ -n "$output" ] && outputs+=("$output")
  done < <(jq -r '.codeGenerators | to_entries[] | .value.output // empty' "$config")
done

if [ ${#outputs[@]} -eq 0 ]; then
  printf 'error: в backend/nswag.*.json не нашлось ни одного output\n' >&2
  exit 2
fi

before="$(mktemp -d)"
trap 'rm -rf "$before"' EXIT

for output in "${outputs[@]}"; do
  src="$BACKEND/$output"
  if [ -f "$src" ]; then
    mkdir -p "$before/$(dirname "$output")"
    cp "$src" "$before/$output"
  fi
done

bash "$ROOT/scripts/quality/openapi-generate.sh"

failed=0
for output in "${outputs[@]}"; do
  src="$BACKEND/$output"
  snapshot="$before/$output"

  if [ ! -f "$snapshot" ]; then
    printf 'дифф: %s не был закоммичен — генератор создал его сейчас\n' "backend/$output" >&2
    failed=1
    continue
  fi

  if ! diff -u "$snapshot" "$src" >/dev/null; then
    printf '\n== дифф: backend/%s ==\n' "$output" >&2
    diff -u "$snapshot" "$src" | head -80 >&2
    failed=1
  fi
done

if [ "$failed" -ne 0 ]; then
  cat >&2 <<'EOF'

Сгенерированный код разошёлся с контрактом.

Если правили *.g.cs руками — не надо: источник правды specs/contracts/**/openapi.yaml.
Если правили контракт — прогоните scripts/quality/openapi-generate.sh и закоммитьте
результат вместе с yaml (перегенерация уже сделана этим гейтом, дифф в рабочем дереве).
EOF
  exit 1
fi

printf 'сгенерированный код совпадает с контрактом (%d файл(ов))\n' "${#outputs[@]}"
