#!/usr/bin/env bash
# Тонкая bash-обёртка над maintainability_budget_check.py.
# Корень — bugget-api (где живёт .quality/), а не git toplevel: git toplevel здесь — bugget/, на уровень выше.
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"

checker="$script_dir/maintainability_budget_check.py"
if [[ ! -f "$checker" ]]; then
  printf 'maintainability_budget_check.py не найден рядом с %s.\n' "$0" >&2
  exit 66
fi

find_python() {
  if command -v python3 >/dev/null 2>&1; then
    printf 'python3\n'
    return
  fi
  if command -v python >/dev/null 2>&1; then
    printf 'python\n'
    return
  fi
  printf 'Python не найден. Нужен python3 или python.\n' >&2
  return 127
}

python_bin="$(find_python)"
cd "$repo_root"
export PYTHONDONTWRITEBYTECODE=1
exec "$python_bin" "$checker" "$@"
