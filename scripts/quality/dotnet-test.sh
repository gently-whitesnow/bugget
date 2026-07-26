#!/usr/bin/env bash
#
# Запуск тестов бекенда одной из двух категорий: unit или integration.
#
# Категория определяется именем проекта: `*.IntegrationTests` — интеграционные (поднимают
# реальные зависимости в Docker через Testcontainers), всё остальное с
# `<IsTestProject>true</IsTestProject>` — юнит. Списка проектов нигде нет: они находятся
# поиском по backend/, поэтому новый тестовый проект не может молча не запускаться нигде.
#
# Юнит-проект со ссылкой на Testcontainers валит запуск: по имени он попал в быструю
# категорию, а по содержимому требует Docker. Так уже было — восемь тестов с контейнером
# Keycloak лежали в Authorization.Tests и тянули Docker в быстрый гейт.
#
# Прогоняются все проекты категории, даже если что-то упало: падение первого не должно
# скрывать состояние остальных.
#
#   dotnet-test.sh unit
#   dotnet-test.sh integration
#
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
BACKEND="$ROOT/backend"

CATEGORY="${1:-}"
case "$CATEGORY" in
  unit|integration) ;;
  -h|--help) sed -n '2,18p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
  *) printf 'error: нужна категория: unit или integration\n' >&2; exit 2 ;;
esac

[ -d "$BACKEND" ] || { printf 'error: нет директории %s\n' "$BACKEND" >&2; exit 2; }
cd "$BACKEND" || exit 2

projects=()
while IFS= read -r proj; do
  [ -n "$proj" ] || continue
  case "$proj" in
    *.IntegrationTests/*) [ "$CATEGORY" = "integration" ] && projects+=("$proj") ;;
    *)                    [ "$CATEGORY" = "unit" ]        && projects+=("$proj") ;;
  esac
done < <(grep -rl --include='*.csproj' '<IsTestProject>true</IsTestProject>' . | sed 's|^\./||' | sort)

if [ ${#projects[@]} -eq 0 ]; then
  printf 'error: в backend/ не нашлось тестовых проектов категории %s\n' "$CATEGORY" >&2
  exit 1
fi

# Юнит-тесты не ходят в I/O: ссылка на Testcontainers в быстрой категории — ошибка раскладки.
if [ "$CATEGORY" = "unit" ]; then
  leaked=0
  for proj in "${projects[@]}"; do
    if grep -qE 'PackageReference[^>]*Include="Testcontainers' "$proj"; then
      printf 'error: %s ссылается на Testcontainers, но по имени попал в unit.\n' "$proj" >&2
      printf '       Либо убрать зависимость, либо переименовать проект в *.IntegrationTests.\n' >&2
      leaked=1
    fi
  done
  [ "$leaked" -eq 0 ] || exit 1
fi

printf 'категория %s: %s проект(ов)\n' "$CATEGORY" "${#projects[@]}"
printf '  %s\n' "${projects[@]}"

failed=()
for proj in "${projects[@]}"; do
  printf '\n== %s ==\n' "$proj"
  dotnet test "$proj" -c Release --no-restore || failed+=("$proj")
done

if [ ${#failed[@]} -gt 0 ]; then
  printf '\nупало проектов: %s из %s\n' "${#failed[@]}" "${#projects[@]}" >&2
  printf '  %s\n' "${failed[@]}" >&2
  exit 1
fi

printf '\nпрошли все проекты категории %s: %s\n' "$CATEGORY" "${#projects[@]}"
