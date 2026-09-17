#!/usr/bin/env python3
"""Общее для python-гейтов качества: корень репозитория, JSON и сопоставление путей."""

from __future__ import annotations

import fnmatch
import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent.parent

def read_json(path: pathlib.Path) -> dict:
    if not path.exists():
        raise SystemExit(f"конфиг не найден: {rel(path)}")
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise SystemExit(f"конфиг не является валидным JSON: {rel(path)} ({exc})")


def write_json(path: pathlib.Path, payload: dict) -> None:
    path.write_text(
        json.dumps(payload, indent=2, ensure_ascii=False, sort_keys=False) + "\n",
        encoding="utf-8",
    )


def rel(path: pathlib.Path) -> str:
    """Путь относительно корня репозитория — так его видно и в выводе, и в git."""
    try:
        return path.resolve().relative_to(ROOT).as_posix()
    except ValueError:
        return path.as_posix()


def matches_any(path: str, patterns) -> bool:
    for pattern in patterns:
        normalized = pattern.replace("\\", "/")
        if fnmatch.fnmatch(path, normalized):
            return True
        # "**/Foo/**" должен ловить и "Foo/bar.cs" в корне области сканирования.
        if normalized.startswith("**/") and fnmatch.fnmatch(path, normalized[3:]):
            return True
    return False
