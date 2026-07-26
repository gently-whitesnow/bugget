#!/usr/bin/env bash
#
# LOC-бюджет фронтенда с ratchet-бейзлайном.
#
# Правило простое: файл `.ts`/`.tsx` не длиннее лимита. Уже существующие превышения
# зафиксированы в бейзлайне и не блокируют работу, но расти им нельзя — только уменьшаться.
# Поэтому новый длинный файл валит гейт, а рефакторинг старого длинного — нет.
#
# Лимит, бейзлайн и исключения лежат в .quality/frontend-loc.json — правится он, не скрипт.
# Пересобрать бейзлайн после осознанного рефакторинга: frontend-loc.sh --update
#
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONFIG="$ROOT/.quality/frontend-loc.json"
UPDATE=0

while [ $# -gt 0 ]; do
  case "$1" in
    --update) UPDATE=1; shift ;;
    -h|--help)
      sed -n '2,12p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
      exit 0 ;;
    *) printf 'error: неизвестный аргумент: %s\n' "$1" >&2; exit 2 ;;
  esac
done

command -v jq >/dev/null 2>&1 || { echo "error: нужен jq — им читается $CONFIG" >&2; exit 2; }
[ -f "$CONFIG" ] || { echo "error: конфиг не найден: $CONFIG" >&2; exit 2; }

LIMIT="$(jq -r '.limit' "$CONFIG")"
[ "$LIMIT" -gt 0 ] 2>/dev/null || { echo "error: некорректный limit в $CONFIG" >&2; exit 2; }

#-----------------------------------------------------------------------------------------
# Замер: путь<TAB>строк, пути относительно корня репозитория, стабильная сортировка.
#-----------------------------------------------------------------------------------------

measure() {
  local roots=() excludes=() find_args=() r p
  while IFS= read -r r; do [ -n "$r" ] && roots+=("$r"); done < <(jq -r '.roots[]' "$CONFIG")
  while IFS= read -r p; do [ -n "$p" ] && excludes+=("$p"); done < <(jq -r '.exclude[]' "$CONFIG")

  for p in ${excludes[@]+"${excludes[@]}"}; do
    find_args+=( -path "$p" -prune -o )
  done

  cd "$ROOT" || return 1
  find "${roots[@]}" ${find_args[@]+"${find_args[@]}"} \
    \( -name '*.ts' -o -name '*.tsx' \) -type f -print0 \
    | sort -z \
    | xargs -0 -n 50 wc -l \
    | awk '$2 != "total" { printf "%s\t%s\n", $2, $1 }'
}

MEASURED="$(measure)" || { echo "error: не удалось посчитать строки" >&2; exit 2; }
[ -n "$MEASURED" ] || { echo "error: под замер не попал ни один файл — проверь roots в $CONFIG" >&2; exit 2; }

# Список путей отдельно: grep по нему идёт из here-string, а не из конвейера, — иначе
# ранний выход `grep -q` роняет левую часть конвейера по SIGPIPE, и pipefail читает это
# как «не нашли».
MEASURED_PATHS="$(printf '%s\n' "$MEASURED" | cut -f1)"

#-----------------------------------------------------------------------------------------
# --update: пересобрать бейзлайн из текущего состояния.
#-----------------------------------------------------------------------------------------

if [ "$UPDATE" = "1" ]; then
  new_baseline="$(printf '%s\n' "$MEASURED" \
    | awk -F'\t' -v lim="$LIMIT" '$2 > lim { printf "%s\t%s\n", $1, $2 }' \
    | jq -R -s 'split("\n") | map(select(length > 0) | split("\t")) | map({(.[0]): (.[1] | tonumber)}) | add // {}')"
  tmp="$CONFIG.tmp"
  jq --argjson b "$new_baseline" '.baseline = $b' "$CONFIG" > "$tmp" && mv "$tmp" "$CONFIG"
  echo "бейзлайн пересобран: $(printf '%s' "$new_baseline" | jq 'length') файл(ов) сверх лимита $LIMIT"
  exit 0
fi

#-----------------------------------------------------------------------------------------
# Проверка
#-----------------------------------------------------------------------------------------

baseline_of() { jq -r --arg f "$1" '.baseline[$f] // ""' "$CONFIG"; }

NEW=()      # превышение лимита, которого нет в бейзлайне
GROWN=()    # бейзлайн-файл вырос
SHRUNK=()   # бейзлайн-файл ужался — бейзлайн можно подтянуть

while IFS=$'\t' read -r file lines; do
  [ -n "$file" ] || continue
  base="$(baseline_of "$file")"

  if [ "$lines" -le "$LIMIT" ]; then
    [ -n "$base" ] && SHRUNK+=("$file: $lines (в бейзлайне $base, теперь в пределах лимита)")
    continue
  fi

  if [ -z "$base" ]; then
    NEW+=("$file: $lines строк (лимит $LIMIT)")
  elif [ "$lines" -gt "$base" ]; then
    GROWN+=("$file: $lines строк, было $base (лимит $LIMIT)")
  elif [ "$lines" -lt "$base" ]; then
    SHRUNK+=("$file: $lines (в бейзлайне $base)")
  fi
done <<< "$MEASURED"

# Записи бейзлайна про файлы, которых больше нет (переименование, удаление).
STALE=()
while IFS= read -r file; do
  [ -n "$file" ] || continue
  grep -qxF "$file" <<< "$MEASURED_PATHS" || STALE+=("$file")
done < <(jq -r '.baseline | keys[]' "$CONFIG")

TOTAL="$(printf '%s\n' "$MEASURED" | wc -l | tr -d ' ')"
echo "LOC-бюджет: лимит $LIMIT строк, проверено файлов: $TOTAL, в бейзлайне: $(jq '.baseline | length' "$CONFIG")"

if [ ${#SHRUNK[@]} -gt 0 ] || [ ${#STALE[@]} -gt 0 ]; then
  echo ""
  echo "бейзлайн можно подтянуть (frontend-loc.sh --update):"
  for x in ${SHRUNK[@]+"${SHRUNK[@]}"}; do echo "  - $x"; done
  for x in ${STALE[@]+"${STALE[@]}"}; do echo "  - $x: файла больше нет"; done
fi

if [ ${#NEW[@]} -eq 0 ] && [ ${#GROWN[@]} -eq 0 ]; then
  echo ""
  echo "LOC-бюджет соблюдён."
  exit 0
fi

echo ""
if [ ${#NEW[@]} -gt 0 ]; then
  echo "новые файлы сверх лимита — разбей на части:"
  for x in "${NEW[@]}"; do echo "  ✘ $x"; done
fi
if [ ${#GROWN[@]} -gt 0 ]; then
  echo "файлы из бейзлайна выросли — им можно только уменьшаться:"
  for x in "${GROWN[@]}"; do echo "  ✘ $x"; done
fi
echo ""
echo "Если превышение осознанное, зафиксируй его в .quality/frontend-loc.json с обоснованием в коммите."
exit 1
