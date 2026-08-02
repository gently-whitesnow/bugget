#!/usr/bin/env bash

set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
VERIFY="$ROOT/scripts/quality/verify.sh"
mkdir -p "$ROOT/artifacts"
VERIFY_SELF_TEST_DIR="$(mktemp -d "$ROOT/artifacts/verify-self-test.XXXXXX")"
export VERIFY_SELF_TEST_DIR
trap 'rm -rf "$VERIFY_SELF_TEST_DIR"' EXIT

fail() {
  printf 'verify self-test: %s\n' "$*" >&2
  exit 1
}

run_must_fail() {
  local config="$1"
  if "$VERIFY" --config "$config" >"$VERIFY_SELF_TEST_DIR/output" 2>&1; then
    fail "ожидался ненулевой код для $(basename "$config")"
  fi
}

cat >"$VERIFY_SELF_TEST_DIR/continue.json" <<'JSON'
{
  "stopOnFirstFail": false,
  "scopes": { "test": { "workdir": "." } },
  "gates": [{
    "id": "continue",
    "scope": "test",
    "enabled": true,
    "slow": false,
    "continueOnFail": true,
    "commands": [
      "printf 'first\\n' >> \"$VERIFY_SELF_TEST_DIR/continue.log\"; false",
      "printf 'second\\n' >> \"$VERIFY_SELF_TEST_DIR/continue.log\""
    ]
  }]
}
JSON

run_must_fail "$VERIFY_SELF_TEST_DIR/continue.json"
[ "$(cat "$VERIFY_SELF_TEST_DIR/continue.log")" = $'first\nsecond' ] \
  || fail "continueOnFail=true не выполнил обе команды"

cat >"$VERIFY_SELF_TEST_DIR/default.json" <<'JSON'
{
  "stopOnFirstFail": false,
  "scopes": { "test": { "workdir": "." } },
  "gates": [{
    "id": "default",
    "scope": "test",
    "enabled": true,
    "slow": false,
    "commands": [
      "printf 'first\\n' >> \"$VERIFY_SELF_TEST_DIR/default.log\"; false",
      "printf 'second\\n' >> \"$VERIFY_SELF_TEST_DIR/default.log\""
    ]
  }]
}
JSON

run_must_fail "$VERIFY_SELF_TEST_DIR/default.json"
[ "$(cat "$VERIFY_SELF_TEST_DIR/default.log")" = "first" ] \
  || fail "режим по умолчанию не остановился на первой ошибке"

cat >"$VERIFY_SELF_TEST_DIR/prepare.json" <<'JSON'
{
  "stopOnFirstFail": false,
  "scopes": {
    "test": {
      "workdir": ".",
      "prepare": [
        "printf 'prepare-first\\n' >> \"$VERIFY_SELF_TEST_DIR/prepare.log\"; false",
        "printf 'prepare-second\\n' >> \"$VERIFY_SELF_TEST_DIR/prepare.log\""
      ]
    }
  },
  "gates": [{
    "id": "after-prepare",
    "scope": "test",
    "enabled": true,
    "slow": false,
    "continueOnFail": true,
    "commands": ["printf 'gate\\n' >> \"$VERIFY_SELF_TEST_DIR/prepare.log\""]
  }]
}
JSON

run_must_fail "$VERIFY_SELF_TEST_DIR/prepare.json"
[ "$(cat "$VERIFY_SELF_TEST_DIR/prepare.log")" = "prepare-first" ] \
  || fail "подготовка продолжилась после ошибки или запустила зависимый гейт"

printf 'verify self-test: ok\n'
