#!/usr/bin/env python3
"""Bugget-api quality verify entrypoint.

Reads .quality/quality.config.json и запускает включённые гейты в порядке
объявления. Тот же оркестратор используется человеком локально и (в будущем)
в CI — единая точка истины «прошёл ли код».

Структура повторяет throne/scripts/quality/verify.py, см. его README.

Exit codes:
  0  every selected gate passed
  1  one or more gates failed
  64 config file missing or invalid
"""

from __future__ import annotations

import argparse
import json
import os
import pathlib
import subprocess
import sys
import time
from typing import Callable


CONFIG_PATH = ".quality/quality.config.json"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Run bugget-api quality gates declared in .quality/quality.config.json."
    )
    parser.add_argument(
        "--scope",
        choices=("all", "backend", "contracts"),
        default="all",
        help="Run only gates with matching scope. Default: all.",
    )
    parser.add_argument(
        "--fast",
        action="store_true",
        help="Skip gates marked slow=true (integration tests etc.).",
    )
    parser.add_argument(
        "--only",
        action="append",
        default=[],
        help="Run only the listed gate id(s). Repeatable.",
    )
    parser.add_argument(
        "--skip",
        action="append",
        default=[],
        help="Skip the listed gate id(s). Repeatable.",
    )
    parser.add_argument(
        "--list",
        action="store_true",
        help="Print configured gates and exit.",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print plan without executing.",
    )
    return parser.parse_args()


def repo_root() -> pathlib.Path:
    here = pathlib.Path(__file__).resolve()
    return here.parent.parent.parent


def load_config(root: pathlib.Path) -> dict:
    path = root / CONFIG_PATH
    if not path.exists():
        print(f"Config not found: {path}", file=sys.stderr)
        raise SystemExit(64)
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        print(f"Config invalid JSON ({path}): {exc}", file=sys.stderr)
        raise SystemExit(64) from exc


def run(cmd: list[str], cwd: pathlib.Path, env: dict[str, str] | None = None) -> int:
    rel = cwd.relative_to(repo_root()) if cwd != repo_root() else pathlib.Path(".")
    print(f"  $ {' '.join(cmd)} (cwd={rel})")
    process_env = os.environ.copy()
    if env:
        process_env.update(env)
    return subprocess.run(cmd, cwd=cwd, env=process_env).returncode


# ---------- gate runners ----------------------------------------------------

def gate_backend_format(_g: dict, root: pathlib.Path) -> int:
    return run(["bash", "scripts/quality/dotnet-format-verify.sh"], root)


def gate_backend_build(_g: dict, root: pathlib.Path) -> int:
    return run(["bash", "scripts/quality/dotnet-build-warnaserror.sh"], root)


def gate_backend_suppressions(_g: dict, root: pathlib.Path) -> int:
    return run(["python3", "scripts/quality/suppression_audit.py", "check"], root)


def gate_backend_maintainability(g: dict, root: pathlib.Path) -> int:
    cmd = ["bash", "scripts/quality/maintainability-budget-check.sh"]
    config = g.get("config")
    if config:
        cmd += ["--config", config]
    profile = g.get("profile")
    if profile:
        cmd += ["--profile", profile]
    ratchet = g.get("ratchet")
    if ratchet:
        cmd += ["--baseline-snapshot", ratchet]
    return run(cmd, root)


def gate_backend_test_unit(_g: dict, root: pathlib.Path) -> int:
    return run(["bash", "scripts/quality/dotnet-test.sh", "--unit-only"], root)


def gate_backend_test_integration(_g: dict, root: pathlib.Path) -> int:
    return run(["bash", "scripts/quality/dotnet-test.sh", "--integration-only"], root)


def gate_contracts(_g: dict, root: pathlib.Path) -> int:
    """Contract-first OpenAPI: генерация + drift-check (ADR-20260518).

    Скрипты живут на уровне bugget/scripts/quality/ (а не bugget-api/),
    потому что покрывают и backend (NSwag), и frontend (openapi-typescript).
    Запускаем по абсолютному пути из cwd=root (bugget-api), чтобы не
    выходить за пределы repo_root() в run()-хелпере.
    """
    bugget_root = root.parent.parent
    scripts = bugget_root / "scripts" / "quality"
    for script in (
        "openapi-generate.sh",
        "openapi-verify-generated-clean.sh",
        "codegen-frontend.sh",
    ):
        rc = run(["bash", str(scripts / script)], root)
        if rc != 0:
            return rc
    return 0


GATE_RUNNERS: dict[str, Callable[[dict, pathlib.Path], int]] = {
    "backend-format": gate_backend_format,
    "backend-build": gate_backend_build,
    "backend-suppressions": gate_backend_suppressions,
    "backend-maintainability": gate_backend_maintainability,
    "backend-test-unit": gate_backend_test_unit,
    "backend-test-integration": gate_backend_test_integration,
    "contracts": gate_contracts,
}


# ---------- orchestration ---------------------------------------------------

def select_gates(config: dict, args: argparse.Namespace) -> list[dict]:
    gates = config.get("gates") or []
    only = set(args.only)
    skip = set(args.skip)

    selected: list[dict] = []
    for gate in gates:
        gate_id = gate.get("id")
        if not gate_id:
            continue
        if not gate.get("enabled", True):
            continue
        if args.scope != "all" and gate.get("scope") != args.scope:
            continue
        if only and gate_id not in only:
            continue
        if gate_id in skip:
            continue
        if args.fast and gate.get("slow", False):
            continue
        selected.append(gate)
    return selected


def list_gates(config: dict) -> None:
    gates = config.get("gates") or []
    print(f"{'id':<32} {'scope':<10} {'enabled':<8} slow")
    print("-" * 60)
    for gate in gates:
        gid = gate.get("id", "?")
        scope = gate.get("scope", "?")
        enabled = "yes" if gate.get("enabled", True) else "no"
        slow = "yes" if gate.get("slow", False) else ""
        print(f"{gid:<32} {scope:<10} {enabled:<8} {slow}")


def main() -> int:
    args = parse_args()
    root = repo_root()
    config = load_config(root)

    if args.list:
        list_gates(config)
        return 0

    selected = select_gates(config, args)
    if not selected:
        print("No gates selected. Use --list to see configured gates.", file=sys.stderr)
        return 0

    results: list[tuple[str, str, float]] = []
    overall = 0
    stop_on_fail = bool(config.get("stopOnFirstFail", False))

    for index, gate in enumerate(selected, 1):
        gate_id = gate["id"]
        runner = GATE_RUNNERS.get(gate_id)
        if runner is None:
            print(f"\n[{index}/{len(selected)}] {gate_id}: UNKNOWN gate id, пропуск", file=sys.stderr)
            results.append((gate_id, "UNKNOWN", 0.0))
            overall = 1
            continue

        print(f"\n==> [{index}/{len(selected)}] {gate_id}")
        if args.dry_run:
            results.append((gate_id, "DRY-RUN", 0.0))
            continue

        started = time.monotonic()
        try:
            rc = runner(gate, root)
        except FileNotFoundError as exc:
            print(f"  COMMAND NOT FOUND: {exc}", file=sys.stderr)
            rc = 127
        elapsed = time.monotonic() - started
        status = "OK" if rc == 0 else f"FAIL({rc})"
        results.append((gate_id, status, elapsed))
        if rc != 0:
            overall = 1
            if stop_on_fail:
                break

    print_summary(results, overall)
    return overall


def print_summary(results: list[tuple[str, str, float]], overall: int) -> None:
    print()
    print("=" * 64)
    print("Quality verify summary")
    print("=" * 64)
    for gate_id, status, elapsed in results:
        suffix = f" ({elapsed:.1f}s)" if elapsed > 0 else ""
        print(f"  {gate_id:<32} {status}{suffix}")
    print("=" * 64)
    print("RESULT:", "PASS" if overall == 0 else "FAIL")


if __name__ == "__main__":
    raise SystemExit(main())
