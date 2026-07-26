#!/usr/bin/env python3
"""Кросс-файловое дублирование в бекенде (гейт backend-duplicates).

Ищет одинаковые куски кода в разных файлах: строки нормализуются (идентификаторы → I,
числа → N, литералы → S), по ним едет окно из N логических строк, окна хешируются,
одинаковые хеши группируются. Переименование переменных дубликат не прячет.

Гейт advisory: группы печатаются, код возврата всегда 0. Причина — на текущем коде
дублирование массовое и осмысленно оно снимается не запретом, а рефакторингом
(ADR-0001, ADR-0002). Отчёт нужен, чтобы новую копипасту было видно на ревью;
блокирующим гейт станет, когда цифра перестанет быть трёхзначной.

Настройки — .quality/backend-duplicates.json. Правится он, не скрипт.

  backend-duplicates.py                  отчёт по конфигу
  backend-duplicates.py --window 12      более длинное окно: меньше шума, грубее сигнал
  backend-duplicates.py --max 0          показать все группы
"""

from __future__ import annotations

import argparse
import hashlib
import pathlib
import sys
from dataclasses import dataclass

import quality_csharp as q

CONFIG = q.ROOT / ".quality" / "backend-duplicates.json"

# Ключевые слова остаются собой: без них «var I = I;» совпадёт с «return I;».
KEYWORDS = {
    "abstract", "as", "async", "await", "base", "bool", "break", "byte", "case", "catch",
    "char", "checked", "class", "const", "continue", "decimal", "default", "delegate",
    "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
    "fixed", "float", "for", "foreach", "get", "global", "goto", "if", "implicit", "in",
    "init", "int", "interface", "internal", "is", "lock", "long", "namespace", "new",
    "null", "object", "operator", "out", "override", "params", "partial", "private",
    "protected", "public", "readonly", "record", "ref", "required", "return", "sbyte",
    "sealed", "set", "short", "sizeof", "stackalloc", "static", "string", "struct",
    "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
    "unsafe", "ushort", "using", "var", "virtual", "void", "volatile", "when", "where",
    "while", "with", "yield",
}

# Строки, которые сами по себе ничего не значат: из них состоит любой C#-файл.
NOISE = {"{", "}", ";", "};", ");", "});", "}", "()", "],", "},"}


@dataclass(frozen=True)
class Window:
    path: str
    start_line: int
    end_line: int


def normalize(path: pathlib.Path) -> list[tuple[int, str]]:
    """Логические строки файла в нормализованном виде: (номер строки, текст)."""
    scanner = q.LineScanner()
    result: list[tuple[int, str]] = []

    for number, line in enumerate(q.read_lines(path), start=1):
        parts: list[str] = []

        def collect(kind: str, value: str) -> None:
            if kind == "word":
                parts.append(value if value in KEYWORDS else "I")
            elif kind == "number":
                parts.append("N")
            elif kind == "string":
                parts.append("S")
            else:
                parts.append(value)

        scanner.scan(line, collect)
        text = "".join(parts)
        if text and text not in NOISE:
            result.append((number, text))

    return result


def collect_groups(files: list[pathlib.Path], window_lines: int) -> dict[str, list[Window]]:
    groups: dict[str, list[Window]] = {}
    for path in files:
        lines = normalize(path)
        if len(lines) < window_lines:
            continue
        relative = q.rel(path)
        for start in range(len(lines) - window_lines + 1):
            chunk = lines[start : start + window_lines]
            digest = hashlib.sha1("\n".join(text for _, text in chunk).encode("utf-8")).hexdigest()
            groups.setdefault(digest, []).append(Window(relative, chunk[0][0], chunk[-1][0]))
    return groups


def unique_windows(windows: list[Window]) -> list[Window]:
    """Внутри одного файла окна одной группы часто наезжают друг на друга — оставляем
    по одному представителю на непрерывный участок."""
    result: list[Window] = []
    last_end: dict[str, int] = {}
    for window in sorted(windows, key=lambda w: (w.path, w.start_line)):
        if window.start_line > last_end.get(window.path, -1):
            result.append(window)
            last_end[window.path] = window.end_line
    return result


def collapse_shifted(groups: list[list[Window]]) -> list[list[Window]]:
    """Убрать группы, сдвинутые на строку-две относительно уже показанной.

    Один и тот же дубликат даёт столько групп, на сколько строк его можно сдвинуть.
    Идём от самых массовых групп и пропускаем те, все окна которых пересекаются с уже
    показанными участками, — иначе отчёт раздувается в разы и читать его невозможно.
    """
    covered: dict[str, list[tuple[int, int]]] = {}
    result: list[list[Window]] = []

    def is_covered(window: Window) -> bool:
        return any(
            start <= window.end_line and window.start_line <= end
            for start, end in covered.get(window.path, [])
        )

    for windows in sorted(groups, key=lambda g: (-len(g), g[0].path, g[0].start_line)):
        if all(is_covered(window) for window in windows):
            continue
        for window in windows:
            covered.setdefault(window.path, []).append((window.start_line, window.end_line))
        result.append(windows)

    return result


def main() -> int:
    config = q.read_json(CONFIG)

    parser = argparse.ArgumentParser(
        description="Кросс-файловое дублирование в бекенде (advisory).",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    parser.add_argument("--window", type=int, default=int(config.get("windowLines", 8)),
                        help="размер окна в логических строках")
    parser.add_argument("--max", type=int, default=int(config.get("maxGroups", 15)),
                        help="сколько групп печатать; 0 — все")
    args = parser.parse_args()

    if args.window < 4:
        raise SystemExit("окно меньше четырёх строк даёт шум на идиоматичном коде")

    min_occurrences = int(config.get("minOccurrences", 2))
    cross_file_only = bool(config.get("crossFileOnly", True))

    files = q.find_csharp_files(config.get("roots") or [], config.get("exclude") or [])
    raw = collect_groups(files, args.window)

    groups = []
    for windows in raw.values():
        windows = unique_windows(windows)
        if len(windows) < min_occurrences:
            continue
        if cross_file_only and len({w.path for w in windows}) < 2:
            continue
        groups.append(windows)

    groups = collapse_shifted(groups)
    total_copies = sum(len(g) for g in groups)

    print(
        f"Дубликаты: окно {args.window} строк, проверено файлов: {len(files)}, "
        f"групп: {len(groups)}, повторов: {total_copies}"
    )

    shown = groups if args.max == 0 else groups[: args.max]
    for windows in shown:
        print()
        print(f"  повторов {len(windows)}, окно {args.window} строк:")
        for window in windows:
            print(f"    {window.path}:{window.start_line}-{window.end_line}")

    if len(groups) > len(shown):
        print()
        print(f"… ещё {len(groups) - len(shown)} групп(ы). Все: --max 0")

    print()
    print("Гейт advisory: группы напечатаны, код возврата 0.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
