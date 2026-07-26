#!/usr/bin/env bash
#
# `npm audit` фронтенда с явным списком принятых уязвимостей.
#
# Голый `npm audit --audit-level high` в этом проекте всегда красный: часть advisory
# висит в транзитивных зависимостях сборочного тулчейна, где исправленной версии просто
# нет. Гейт из-за этого либо выключают, либо перестают читать. Поэтому: падаем на любом
# high/critical, кроме тех, что явно приняты в .quality/frontend-audit-allowlist.json
# с причиной и датой пересмотра. Новая уязвимость валит гейт всегда.
#
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ALLOWLIST="$ROOT/.quality/frontend-audit-allowlist.json"
FRONTEND="$ROOT/frontend"

command -v jq >/dev/null 2>&1 || { echo "error: нужен jq" >&2; exit 2; }
[ -f "$ALLOWLIST" ] || { echo "error: не найден список принятых уязвимостей: $ALLOWLIST" >&2; exit 2; }

REPORT="$(cd "$FRONTEND" && npm audit --json 2>/dev/null)"
jq -e 'has("vulnerabilities")' <<< "$REPORT" >/dev/null 2>&1 \
  || { echo "error: npm audit не вернул отчёт (нет сети или сломан package-lock.json)" >&2; exit 2; }

# Advisory уровня high/critical: по одной записи на уязвимость, без дублей по URL.
FOUND="$(jq -r '
  [ .vulnerabilities[] | .via[] | select(type == "object")
    | select(.severity == "high" or .severity == "critical") ]
  | unique_by(.url) | sort_by(.url)
  | .[] | "\(.url)\t\(.severity)\t\(.name)\t\(.title)"' <<< "$REPORT")"

ACCEPTED="$(jq -r '.accepted[].advisory' "$ALLOWLIST")"

BLOCKING=()
while IFS=$'\t' read -r url severity name title; do
  [ -n "$url" ] || continue
  grep -qxF "$url" <<< "$ACCEPTED" || BLOCKING+=("$severity  $name — $title ($url)")
done <<< "$FOUND"

# Принятая уязвимость, которой в отчёте больше нет: её пора убрать из списка.
STALE=()
while IFS= read -r url; do
  [ -n "$url" ] || continue
  grep -qF "$url" <<< "$FOUND" || STALE+=("$url")
done <<< "$ACCEPTED"

total="$(jq -r '.metadata.vulnerabilities.total // 0' <<< "$REPORT")"
echo "npm audit: всего уязвимостей $total, high/critical $(grep -c . <<< "$FOUND" 2>/dev/null || echo 0), принято явно $(jq '.accepted | length' "$ALLOWLIST")"

if [ ${#STALE[@]} -gt 0 ]; then
  echo ""
  echo "уже исправлено — убери из $ALLOWLIST:"
  for x in "${STALE[@]}"; do echo "  - $x"; done
fi

if [ ${#BLOCKING[@]} -eq 0 ]; then
  echo ""
  echo "Непринятых high/critical уязвимостей нет."
  exit 0
fi

echo ""
echo "непринятые уязвимости high/critical:"
for x in "${BLOCKING[@]}"; do echo "  ✘ $x"; done
echo ""
echo "Чини через npm audit fix / обновление зависимости. Если исправления нет, добавь advisory"
echo "в .quality/frontend-audit-allowlist.json с причиной и датой пересмотра — и обоснуй в коммите."
exit 1
