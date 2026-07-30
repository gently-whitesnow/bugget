#!/usr/bin/env bash
#
# Гейт frontend-contracts: типы фронта обязаны совпадать с контрактом.
#
# Зеркало backend-гейта openapi-check.sh для клиентской половины генерации.
# Перегенерирует frontend/src/shared/api/generated/*.d.ts из
# specs/contracts/**/openapi.yaml и сравнивает с тем, что лежит в репозитории.
# Непустой дифф — красный гейт.
#
# Ловит пять случаев:
#   * сгенерированный .d.ts правили руками — правка не воспроизводится генератором;
#   * контракт поменяли, а генерацию не прогнали — типы фронта отстали от yaml;
#   * завели новый модуль контракта, а типы к нему не сгенерировали;
#   * контракт удалили или переименовали, а .d.ts остался сиротой;
#   * имя поля тела не выражается проекцией фронта — не канон snake_case, не
#     переживает round-trip wire -> camelCase -> wire либо это свободный словарь
#     с ключами клиента (frontend-case-roundtrip.py, ADR-0009).
#
# Ожидаемый набор файлов выводится из specs/contracts/*/openapi.yaml, а НЕ из того,
# что уже лежит в каталоге. Иначе сирота не ловится: генератор её не трогает, и
# сравнение «до/после» на ней сходится.
#
# То, что фронт реально использует эти типы, проверяет не этот гейт, а
# frontend-typecheck.
#
# Файлы после прогона остаются перегенерированными: если гейт красный, дифф уже
# в рабочем дереве, и его видно обычным `git diff`.
#
# Использование:
#   scripts/quality/frontend-openapi-check.sh
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

if [ "${1:-}" = "--self-test" ]; then
  exec bash "$ROOT/scripts/quality/frontend-openapi-selftest.sh"
fi

# Каталоги переопределяются только самопроверкой (--self-test), которая гоняет
# гейт в песочнице. В обычном прогоне действуют пути репозитория.
CONTRACTS="${BUGGET_CONTRACTS_DIR:-$ROOT/specs/contracts}"
OUT_DIR="${BUGGET_GENERATED_DIR:-$ROOT/frontend/src/shared/api/generated}"
REL="${BUGGET_GENERATED_REL:-frontend/src/shared/api/generated}"

# Ожидаемый набор — по одному .d.ts на каталог модуля в specs/contracts.
expected=()
for spec in "$CONTRACTS"/*/openapi.yaml; do
  expected+=("$(basename "$(dirname "$spec")").d.ts")
done

if [ ${#expected[@]} -eq 0 ]; then
  printf 'error: в %s не нашлось ни одного openapi.yaml\n' "$CONTRACTS" >&2
  exit 2
fi

before="$(mktemp -d)"
trap 'rm -rf "$before"' EXIT

shopt -s nullglob
present=("$OUT_DIR"/*.d.ts)
for src in ${present[@]+"${present[@]}"}; do
  cp "$src" "$before/$(basename "$src")"
done
shopt -u nullglob

failed=0

# Сирота: файл в каталоге, которому не соответствует ни один контракт. Проверяем
# до генерации — генератор такие файлы не трогает и сам их не обнаружит.
shopt -s nullglob
for src in "$OUT_DIR"/*.d.ts; do
  name="$(basename "$src")"
  found=0
  for want in "${expected[@]}"; do
    [ "$name" = "$want" ] && { found=1; break; }
  done
  if [ "$found" -eq 0 ]; then
    printf 'сирота: %s/%s не соответствует ни одному контракту в specs/contracts — удалите файл\n' "$REL" "$name" >&2
    failed=1
  fi
done
shopt -u nullglob

bash "$ROOT/scripts/quality/frontend-openapi-generate.sh"

# Имена полей тела: канон провода, обратимость проекции и запрет свободных
# словарей. То, что проекция фронта не выражает, ломает провод молча, поэтому
# контрактная проверка останавливается на этом до того, как контракт начнут
# использовать.
if ! BUGGET_GENERATED_DIR="$OUT_DIR" python3 "$ROOT/scripts/quality/frontend-case-roundtrip.py" >&2; then
  failed=1
fi

# Дифф по ожидаемому набору: и пропавший файл, и разошедшийся содержимым — красные.
for name in "${expected[@]}"; do
  src="$OUT_DIR/$name"
  snapshot="$before/$name"

  if [ ! -f "$src" ]; then
    printf 'error: генератор не создал %s/%s\n' "$REL" "$name" >&2
    failed=1
    continue
  fi

  if [ ! -f "$snapshot" ]; then
    printf 'дифф: %s/%s не был закоммичен — генератор создал его сейчас\n' "$REL" "$name" >&2
    failed=1
    continue
  fi

  if ! diff -u "$snapshot" "$src" >/dev/null; then
    printf '\n== дифф: %s/%s ==\n' "$REL" "$name" >&2
    diff -u "$snapshot" "$src" | head -80 >&2
    failed=1
  fi
done

if [ "$failed" -ne 0 ]; then
  cat >&2 <<'EOF'

Типы фронта разошлись с контрактом.

Если правили generated/*.d.ts руками — не надо: источник правды
specs/contracts/**/openapi.yaml (ADR-0005).
Если правили контракт — прогоните scripts/quality/frontend-openapi-generate.sh
и закоммитьте результат вместе с yaml (перегенерация уже сделана этим гейтом,
дифф в рабочем дереве).
Если удалили модуль контракта — удалите и соответствующий .d.ts.
EOF
  exit 1
fi

printf 'типы фронта совпадают с контрактом (%d файл(ов))\n' "${#expected[@]}"
