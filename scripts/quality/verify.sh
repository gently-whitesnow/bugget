#!/usr/bin/env bash
#
# Единая точка входа проверок качества Bugget.
# Одну и ту же команду запускают агент перед сдачей хода, человек локально и CI.
#
# Набор гейтов декларируется в .quality/quality.config.json — правится он, не этот скрипт.
#
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONFIG="$ROOT/.quality/quality.config.json"

MODE="run"          # run | list | dry-run
FAST=0
SCOPES=()
ONLY=()
SKIP=()

#-----------------------------------------------------------------------------------------
# Вывод
#-----------------------------------------------------------------------------------------

if [ -t 1 ] && [ -z "${NO_COLOR:-}" ]; then
  C_RESET=$'\033[0m'; C_BOLD=$'\033[1m'; C_DIM=$'\033[2m'
  C_RED=$'\033[31m'; C_GREEN=$'\033[32m'; C_YELLOW=$'\033[33m'; C_CYAN=$'\033[36m'
else
  C_RESET=''; C_BOLD=''; C_DIM=''; C_RED=''; C_GREEN=''; C_YELLOW=''; C_CYAN=''
fi

info() { printf '%s\n' "$*"; }
warn() { printf '%s\n' "${C_YELLOW}warning:${C_RESET} $*" >&2; }
die()  { printf '%s\n' "${C_RED}error:${C_RESET} $*" >&2; exit 2; }

usage() {
  cat <<'EOF'
verify.sh — единая точка входа проверок качества Bugget.

Использование:
  scripts/quality/verify.sh [флаги]

Флаги:
  --list                  Показать гейты из конфига и выйти.
  --dry-run               Показать план запуска (какие гейты и какие команды) без выполнения.
  --fast                  Пропустить гейты с "slow": true.
  --scope <backend|frontend>
                          Ограничить прогон областью. Флаг повторяемый.
  --only <gate-id>        Запустить только указанный гейт. Флаг повторяемый.
  --skip <gate-id>        Исключить указанный гейт. Флаг повторяемый.
  --config <path>         Путь к конфигу (по умолчанию .quality/quality.config.json).
  -h, --help              Эта справка.

Поведение:
  По умолчанию (stopOnFirstFail: false в конфиге) прогоняются все выбранные гейты, даже
  если что-то упало, — в конце печатается сводка со статусом и временем каждого гейта.
  Ненулевой exit code, если упал хотя бы один гейт.
  Команды внутри гейта по умолчанию выполняются fail-fast. Поле continueOnFail: true
  выполняет все команды гейта, но сохраняет ненулевой итог при любом падении.
  Подготовка области всегда выполняется fail-fast.

Примеры:
  scripts/quality/verify.sh                       # всё
  scripts/quality/verify.sh --fast                # без медленных гейтов
  scripts/quality/verify.sh --scope backend       # только бекенд
  scripts/quality/verify.sh --only frontend-lint  # ровно один гейт
EOF
}

#-----------------------------------------------------------------------------------------
# Разбор аргументов
#-----------------------------------------------------------------------------------------

need_value() { [ -n "${2:-}" ] || die "флаг $1 требует значения"; }

while [ $# -gt 0 ]; do
  case "$1" in
    --list)     MODE="list"; shift ;;
    --dry-run)  MODE="dry-run"; shift ;;
    --fast)     FAST=1; shift ;;
    --scope)    need_value "$1" "${2:-}"; SCOPES+=("$2"); shift 2 ;;
    --only)     need_value "$1" "${2:-}"; ONLY+=("$2"); shift 2 ;;
    --skip)     need_value "$1" "${2:-}"; SKIP+=("$2"); shift 2 ;;
    --config)   need_value "$1" "${2:-}"; CONFIG="$2"; shift 2 ;;
    -h|--help)  usage; exit 0 ;;
    *)          die "неизвестный аргумент: $1 (см. --help)" ;;
  esac
done

command -v jq >/dev/null 2>&1 || die "нужен jq — им читается $CONFIG"
[ -f "$CONFIG" ] || die "конфиг не найден: $CONFIG"
jq -e . "$CONFIG" >/dev/null 2>&1 || die "конфиг не является валидным JSON: $CONFIG"

#-----------------------------------------------------------------------------------------
# Чтение конфига
#-----------------------------------------------------------------------------------------

STOP_ON_FIRST_FAIL="$(jq -r '.stopOnFirstFail // false' "$CONFIG")"

ALL_IDS=()
while IFS= read -r line; do
  [ -n "$line" ] && ALL_IDS+=("$line")
done < <(jq -r '.gates[].id' "$CONFIG")

[ ${#ALL_IDS[@]} -gt 0 ] || die "в конфиге нет ни одного гейта: $CONFIG"

# Гейты ищутся по id, поэтому дубль id — это не «два гейта», а склеенные поля обоих
# и мусор в выводе. Ловим сразу.
DUP_IDS="$(printf '%s\n' "${ALL_IDS[@]}" | sort | uniq -d)"
[ -z "$DUP_IDS" ] || die "в конфиге повторяются id гейтов: $(printf '%s' "$DUP_IDS" | tr '\n' ' ')"

# Поля гейта по id.
gate_field() { jq -r --arg id "$1" --arg f "$2" '.gates[] | select(.id == $id) | .[$f] // ""' "$CONFIG"; }
gate_commands() { jq -r --arg id "$1" '.gates[] | select(.id == $id) | .commands[]' "$CONFIG"; }
scope_field() { jq -r --arg s "$1" --arg f "$2" '.scopes[$s][$f] // ""' "$CONFIG"; }
scope_prepare() { jq -r --arg s "$1" '.scopes[$s].prepare // [] | .[]' "$CONFIG"; }

# Рабочая директория гейта: своя, иначе от scope, иначе корень репозитория.
gate_workdir() {
  local wd; wd="$(gate_field "$1" workdir)"
  [ -n "$wd" ] || wd="$(scope_field "$(gate_field "$1" scope)" workdir)"
  [ -n "$wd" ] || wd="."
  printf '%s' "$wd"
}

contains() {
  local needle="$1"; shift
  local x
  for x in "$@"; do [ "$x" = "$needle" ] && return 0; done
  return 1
}

#-----------------------------------------------------------------------------------------
# --list
#-----------------------------------------------------------------------------------------

if [ "$MODE" = "list" ]; then
  info "${C_BOLD}Гейты качества${C_RESET} ${C_DIM}($CONFIG)${C_RESET}"
  info ""
  for id in "${ALL_IDS[@]}"; do
    local_enabled="$(gate_field "$id" enabled)"
    local_slow="$(gate_field "$id" slow)"
    flags=""
    [ "$local_enabled" = "true" ] || flags="${flags} ${C_YELLOW}[выключен]${C_RESET}"
    [ "$local_slow" = "true" ] && flags="${flags} ${C_DIM}[slow]${C_RESET}"
    printf '  %s%-22s%s %-9s%s\n' "$C_CYAN" "$id" "$C_RESET" "$(gate_field "$id" scope)" "$flags"
    printf '    %s%s%s\n\n' "$C_DIM" "$(gate_field "$id" description)" "$C_RESET"
  done
  exit 0
fi

#-----------------------------------------------------------------------------------------
# Отбор гейтов
#-----------------------------------------------------------------------------------------

# Неизвестные id в --only/--skip — ошибка, а не молчаливый пропуск.
for id in ${ONLY[@]+"${ONLY[@]}"} ${SKIP[@]+"${SKIP[@]}"}; do
  contains "$id" "${ALL_IDS[@]}" || die "нет такого гейта: $id (см. --list)"
done

for s in ${SCOPES[@]+"${SCOPES[@]}"}; do
  [ "$(jq -r --arg s "$s" 'has("scopes") and (.scopes | has($s))' "$CONFIG")" = "true" ] \
    || die "нет такой области: $s (см. --list)"
done

SELECTED=()
for id in "${ALL_IDS[@]}"; do
  if [ ${#ONLY[@]} -gt 0 ] && ! contains "$id" "${ONLY[@]}"; then continue; fi
  if [ ${#SKIP[@]} -gt 0 ] && contains "$id" "${SKIP[@]}"; then continue; fi
  if [ ${#SCOPES[@]} -gt 0 ] && ! contains "$(gate_field "$id" scope)" "${SCOPES[@]}"; then continue; fi

  if [ "$(gate_field "$id" enabled)" != "true" ]; then
    # Выключенный гейт не запускаем никогда, но если его попросили явно — говорим об этом громко.
    if [ ${#ONLY[@]} -gt 0 ]; then
      warn "гейт $id выключен в конфиге (enabled: false) и пропущен"
    fi
    continue
  fi

  if [ "$FAST" = "1" ] && [ "$(gate_field "$id" slow)" = "true" ]; then continue; fi

  SELECTED+=("$id")
done

if [ ${#SELECTED[@]} -eq 0 ]; then
  # Явный --only, не давший ни одного гейта, — это ошибка вызова, а не зелёный прогон:
  # иначе выключенный или отфильтрованный гейт читается как «проверка прошла».
  [ ${#ONLY[@]} -gt 0 ] && die "запрошенные через --only гейты не попали в прогон (выключены в конфиге или отсечены --fast/--scope)"
  warn "под заданные фильтры не подошёл ни один гейт"
  exit 0
fi

#-----------------------------------------------------------------------------------------
# Области, задействованные отобранными гейтами (в порядке появления)
#-----------------------------------------------------------------------------------------

ACTIVE_SCOPES=()
for id in "${SELECTED[@]}"; do
  s="$(gate_field "$id" scope)"
  contains "$s" ${ACTIVE_SCOPES[@]+"${ACTIVE_SCOPES[@]}"} || ACTIVE_SCOPES+=("$s")
done

#-----------------------------------------------------------------------------------------
# --dry-run
#-----------------------------------------------------------------------------------------

if [ "$MODE" = "dry-run" ]; then
  info "${C_BOLD}План прогона${C_RESET} ${C_DIM}(${#SELECTED[@]} гейт(ов), ничего не выполняется)${C_RESET}"
  info ""
  for s in "${ACTIVE_SCOPES[@]}"; do
    while IFS= read -r cmd; do
      [ -n "$cmd" ] && printf '  %sподготовка %-11s%s %s  %s(в %s)%s\n' \
        "$C_CYAN" "$s" "$C_RESET" "$cmd" "$C_DIM" "$(scope_field "$s" workdir)" "$C_RESET"
    done < <(scope_prepare "$s")
  done
  info ""
  for id in "${SELECTED[@]}"; do
    printf '  %s%s%s  %s(в %s)%s\n' "$C_CYAN" "$id" "$C_RESET" "$C_DIM" "$(gate_workdir "$id")" "$C_RESET"
    while IFS= read -r cmd; do
      [ -n "$cmd" ] && printf '      %s\n' "$cmd"
    done < <(gate_commands "$id")
  done
  exit 0
fi

#-----------------------------------------------------------------------------------------
# Прогон
#-----------------------------------------------------------------------------------------

now_ms() {
  if [ -n "${EPOCHREALTIME:-}" ]; then
    local t="${EPOCHREALTIME/[.,]/}"
    echo $(( 10#$t / 1000 ))
  else
    echo $(( $(date +%s) * 1000 ))
  fi
}

fmt_ms() { printf '%d.%01ds' $(( $1 / 1000 )) $(( ($1 % 1000) / 100 )); }

run_in() {
  # run_in <workdir> <continue-on-fail> — команды читаются со stdin и выполняются по очереди.
  local wd="$1"
  local continue_on_fail="${2:-false}"
  local dir="$ROOT/$wd"
  [ -d "$dir" ] || { printf '%s\n' "нет директории $dir" >&2; return 1; }
  local cmd
  local failed=0
  while IFS= read -r cmd; do
    [ -n "$cmd" ] || continue
    printf '%s$ (%s) %s%s\n' "$C_DIM" "$wd" "$cmd" "$C_RESET"
    if ! ( cd "$dir" && eval "$cmd" ); then
      failed=1
      [ "$continue_on_fail" = "true" ] || return 1
    fi
  done
  [ "$failed" -eq 0 ]
}

STATUSES=()
DURATIONS=()
FAILED=0

for s in "${ACTIVE_SCOPES[@]}"; do
  prepare="$(scope_prepare "$s")"
  [ -n "$prepare" ] || continue
  info ""
  info "${C_BOLD}== подготовка: $s ==${C_RESET}"
  if ! printf '%s\n' "$prepare" | run_in "$(scope_field "$s" workdir)" false; then
    printf '%s\n' "${C_RED}подготовка области $s не прошла${C_RESET}" >&2
    exit 1
  fi
done

for id in "${SELECTED[@]}"; do
  info ""
  info "${C_BOLD}== гейт: $id ==${C_RESET} ${C_DIM}$(gate_field "$id" description)${C_RESET}"
  started="$(now_ms)"
  if gate_commands "$id" | run_in "$(gate_workdir "$id")" "$(gate_field "$id" continueOnFail)"; then
    STATUSES+=("ok")
  else
    STATUSES+=("fail")
    FAILED=$(( FAILED + 1 ))
  fi
  DURATIONS+=("$(( $(now_ms) - started ))")

  if [ "$FAILED" -gt 0 ] && [ "$STOP_ON_FIRST_FAIL" = "true" ]; then
    warn "stopOnFirstFail: true — прогон остановлен на первом падении"
    break
  fi
done

#-----------------------------------------------------------------------------------------
# Сводка
#-----------------------------------------------------------------------------------------

info ""
info "${C_BOLD}== сводка ==${C_RESET}"
i=0
while [ "$i" -lt ${#STATUSES[@]} ]; do
  if [ "${STATUSES[$i]}" = "ok" ]; then
    mark="${C_GREEN}OK  ${C_RESET}"
  else
    mark="${C_RED}FAIL${C_RESET}"
  fi
  printf '  %s  %-22s %8s\n' "$mark" "${SELECTED[$i]}" "$(fmt_ms "${DURATIONS[$i]}")"
  i=$(( i + 1 ))
done

skipped=$(( ${#SELECTED[@]} - ${#STATUSES[@]} ))
[ "$skipped" -gt 0 ] && info "  ${C_DIM}не запускались: $skipped${C_RESET}"

info ""
if [ "$FAILED" -gt 0 ]; then
  info "${C_RED}${C_BOLD}Упало гейтов: $FAILED из ${#STATUSES[@]}${C_RESET}"
  exit 1
fi
info "${C_GREEN}${C_BOLD}Все гейты прошли: ${#STATUSES[@]}${C_RESET}"
