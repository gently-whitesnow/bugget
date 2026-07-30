#!/usr/bin/env bash
#
# Самопроверка гейта frontend-contracts: краснеет ли он там, где обязан.
#
# Гейт, который молча зелёный на сломанном входе, хуже отсутствующего — именно так
# и вышло с первой версией проверки на сироту: она сравнивала «до» и «после»
# генерации, а генератор чужие файлы не трогает, поэтому сирота проходила.
# Здесь каждый случай красноты проверяется явно.
#
# Гоняется в песочнице: копии specs/contracts и generated/ во временном каталоге,
# рабочее дерево репозитория не трогается (пути гейт берёт из
# BUGGET_CONTRACTS_DIR / BUGGET_GENERATED_DIR).
#
# Использование:
#   scripts/quality/frontend-openapi-check.sh --self-test
#
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CHECK="$ROOT/scripts/quality/frontend-openapi-check.sh"

SANDBOX="$(mktemp -d)"
trap 'rm -rf "$SANDBOX"' EXIT

FAILED=0

# Прогон гейта в песочнице. Возвращает его код выхода, вывод копит в файл.
run_gate() {
  BUGGET_CONTRACTS_DIR="$SANDBOX/contracts" \
  BUGGET_GENERATED_DIR="$SANDBOX/generated" \
  BUGGET_GENERATED_REL="generated" \
    bash "$CHECK" >"$SANDBOX/out" 2>&1
}

# Эталон согласованного состояния собирается один раз: генерация на каждый случай
# заново стоила бы шести лишних прогонов openapi-typescript.
mkdir -p "$SANDBOX/baseline/generated"
cp -R "$ROOT/specs/contracts" "$SANDBOX/baseline/contracts"
if ! BUGGET_CONTRACTS_DIR="$SANDBOX/baseline/contracts" \
     BUGGET_GENERATED_DIR="$SANDBOX/baseline/generated" \
       bash "$ROOT/scripts/quality/frontend-openapi-generate.sh" >"$SANDBOX/out" 2>&1; then
  printf 'error: не удалось собрать эталон для самопроверки\n' >&2
  cat "$SANDBOX/out" >&2
  exit 2
fi

# Свежая песочница: контракты и сгенерированное в согласованном состоянии.
reset_sandbox() {
  rm -rf "$SANDBOX/contracts" "$SANDBOX/generated"
  cp -R "$SANDBOX/baseline/contracts" "$SANDBOX/contracts"
  cp -R "$SANDBOX/baseline/generated" "$SANDBOX/generated"
}

expect() {
  local name="$1" want="$2" got="$3" needle="${4:-}"
  if [ "$got" != "$want" ]; then
    printf '  ПРОВАЛ  %s: код выхода %s, ожидался %s\n' "$name" "$got" "$want" >&2
    sed 's/^/          /' "$SANDBOX/out" >&2
    FAILED=$(( FAILED + 1 ))
    return
  fi
  if [ -n "$needle" ] && ! grep -q -- "$needle" "$SANDBOX/out"; then
    printf '  ПРОВАЛ  %s: в выводе нет «%s»\n' "$name" "$needle" >&2
    sed 's/^/          /' "$SANDBOX/out" >&2
    FAILED=$(( FAILED + 1 ))
    return
  fi
  printf '  ok      %s\n' "$name"
}

printf 'самопроверка гейта frontend-contracts\n'

# 1. Согласованное состояние — зелёный. Без этого остальные случаи ничего не значат.
reset_sandbox
run_gate
expect "согласованное состояние зелёное" 0 "$?"

# 2. Ручная правка сгенерированного.
reset_sandbox
printf '\n// правка руками\n' >> "$SANDBOX/generated/settings.d.ts"
run_gate
expect "ручная правка .d.ts краснеет" 1 "$?" "дифф:"

# 3. Контракт поменяли, генерацию не прогнали. Правка обязана менять именно типы:
# комментарий или info.title в .d.ts не попадают, поэтому переименовываем поле схемы.
reset_sandbox
sed 's/^        user_sections:$/        user_sections_renamed:/' \
  "$SANDBOX/contracts/settings/openapi.yaml" > "$SANDBOX/settings.patched"
if ! diff -q "$SANDBOX/settings.patched" "$SANDBOX/contracts/settings/openapi.yaml" >/dev/null; then
  mv "$SANDBOX/settings.patched" "$SANDBOX/contracts/settings/openapi.yaml"
  run_gate
  expect "правка yaml без перегенерации краснеет" 1 "$?" "дифф:"
else
  printf '  ПРОВАЛ  правка yaml: поле user_sections не найдено, случай не проверен\n' >&2
  FAILED=$(( FAILED + 1 ))
fi

# 4. Новый модуль контракта без сгенерированных типов.
reset_sandbox
mkdir -p "$SANDBOX/contracts/newmod"
cp "$SANDBOX/contracts/settings/openapi.yaml" "$SANDBOX/contracts/newmod/openapi.yaml"
run_gate
expect "новый модуль без .d.ts краснеет" 1 "$?" "не был закоммичен"

# 5. Сирота: .d.ts без контракта. Ровно тот случай, который первая версия пропускала.
reset_sandbox
cp "$SANDBOX/generated/settings.d.ts" "$SANDBOX/generated/ghost.d.ts"
run_gate
expect "сирота .d.ts краснеет" 1 "$?" "сирота:"

# 6. Удалённый модуль контракта оставляет сироту.
reset_sandbox
rm -rf "$SANDBOX/contracts/external"
run_gate
expect "удалённый контракт краснеет" 1 "$?" "сирота:"

# 7. Имя поля тела, не переживающее round-trip wire -> camelCase -> wire.
# `user_ID` уедет в `userID` и вернётся `user_i_d`: фронт отправил бы на провод
# не то имя, которое описано в контракте.
reset_sandbox
sed 's/^        user_sections:$/        user_ID_sections:/' \
  "$SANDBOX/contracts/settings/openapi.yaml" > "$SANDBOX/settings.roundtrip"
if ! diff -q "$SANDBOX/settings.roundtrip" "$SANDBOX/contracts/settings/openapi.yaml" >/dev/null; then
  mv "$SANDBOX/settings.roundtrip" "$SANDBOX/contracts/settings/openapi.yaml"
  run_gate
  expect "необратимое имя поля краснеет" 1 "$?" "round-trip имён полей не сходится"
else
  printf '  ПРОВАЛ  round-trip: поле user_sections не найдено, случай не проверен\n' >&2
  FAILED=$(( FAILED + 1 ))
fi

# 8. Самопроверка самой проверки round-trip.
if ! python3 "$ROOT/scripts/quality/frontend-case-roundtrip.py" --self-test >"$SANDBOX/out" 2>&1; then
  printf '  ПРОВАЛ  самопроверка frontend-case-roundtrip.py\n' >&2
  sed 's/^/          /' "$SANDBOX/out" >&2
  FAILED=$(( FAILED + 1 ))
else
  printf '  ok      самопроверка frontend-case-roundtrip.py\n'
fi

printf '\n'
if [ "$FAILED" -ne 0 ]; then
  printf 'самопроверка не прошла: провалов %d\n' "$FAILED" >&2
  exit 1
fi

printf 'самопроверка пройдена: гейт краснеет во всех проверяемых случаях\n'
