#!/usr/bin/env python3
"""Общее для бекендовых гейтов качества: конфиг, отбор .cs-файлов, лексика C#.

Модуль ничего не проверяет сам — им пользуются backend-maintainability.py,
backend-duplicates.py и backend-suppressions.py, чтобы три гейта одинаково понимали,
что такое «файл проекта» и «строка кода».
"""

from __future__ import annotations

import fnmatch
import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parent.parent.parent

# Артефакты сборки и сгенерированный код не наш — метрики по нему бессмысленны.
ALWAYS_EXCLUDE = (
    "**/bin/**",
    "**/obj/**",
    "**/Generated/**",
    "**/*.g.cs",
)


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


def find_csharp_files(roots, exclude) -> list[pathlib.Path]:
    """Все .cs под roots (пути от корня репозитория), кроме подходящих под exclude."""
    patterns = list(ALWAYS_EXCLUDE) + list(exclude or [])
    files: list[pathlib.Path] = []
    seen: set[pathlib.Path] = set()

    for root in roots:
        base = ROOT / root
        if not base.exists():
            raise SystemExit(f"область сканирования не найдена: {root}")
        for path in base.rglob("*.cs"):
            if not path.is_file():
                continue
            relative = rel(path)
            if matches_any(relative, patterns):
                continue
            if path in seen:
                continue
            seen.add(path)
            files.append(path)

    if not files:
        raise SystemExit("под замер не попал ни один .cs — проверь roots и exclude в конфиге")
    return sorted(files)


def read_lines(path: pathlib.Path) -> list[str]:
    return path.read_text(encoding="utf-8-sig").splitlines()


# Пунктуация из двух символов: без неё «&&» читается как два «&», а «=>» — как «=» и «>».
TWO_CHAR_PUNCTS = ("=>", "&&", "||", "??", "?.", "?[", "==", "!=", "<=", ">=")


def is_escaped(text: str, index: int) -> bool:
    """Экранирован ли символ по индексу index нечётным числом обратных слэшей."""
    backslashes = 0
    i = index - 1
    while i >= 0 and text[i] == "\\":
        backslashes += 1
        i -= 1
    return backslashes % 2 == 1


class LineScanner:
    """Построчный разбор C# с памятью о состоянии между строками.

    Нужен и бюджету (посчитать логические строки), и поиску дубликатов
    (нормализовать строку). Комментарии и содержимое литералов не должны попадать
    ни туда, ни туда — иначе метрика меряет комментарии, а дубликаты ловят
    одинаковые тексты сообщений.
    """

    def __init__(self) -> None:
        self.in_block_comment = False
        self.in_string = False
        self.verbatim = False

    def scan(self, line: str, on_code) -> None:
        """Пройти строку, вызывая on_code(kind, value) на каждой значимой лексеме.

        kind: "string" — строковый/символьный литерал целиком, "word" — идентификатор
        или ключевое слово, "number" — числовой литерал, "punct" — один символ пунктуации.
        """
        i = 0
        length = len(line)
        while i < length:
            ch = line[i]
            nxt = line[i + 1] if i + 1 < length else ""

            if self.in_block_comment:
                if ch == "*" and nxt == "/":
                    self.in_block_comment = False
                    i += 2
                    continue
                i += 1
                continue

            if self.in_string:
                if self.verbatim and ch == '"' and nxt == '"':
                    i += 2
                    continue
                if ch == '"' and (self.verbatim or not is_escaped(line, i)):
                    self.in_string = False
                    self.verbatim = False
                    on_code("string", '"')
                i += 1
                continue

            if ch == "/" and nxt == "/":
                return
            if ch == "/" and nxt == "*":
                self.in_block_comment = True
                i += 2
                continue
            if ch in "@$" and nxt == '"':
                self.in_string = True
                self.verbatim = ch == "@"
                i += 2
                continue
            if ch == '"':
                self.in_string = True
                self.verbatim = False
                i += 1
                continue
            if ch == "'":
                # Символьный литерал всегда укладывается в одну строку.
                j = i + 1
                while j < length and not (line[j] == "'" and not is_escaped(line, j)):
                    j += 1
                on_code("string", "'")
                i = j + 1
                continue
            if ch.isalpha() or ch == "_":
                j = i
                while j < length and (line[j].isalnum() or line[j] == "_"):
                    j += 1
                on_code("word", line[i:j])
                i = j
                continue
            if ch.isdigit():
                j = i
                while j < length and (line[j].isalnum() or line[j] in "._"):
                    j += 1
                on_code("number", line[i:j])
                i = j
                continue
            if ch + nxt in TWO_CHAR_PUNCTS:
                on_code("punct", ch + nxt)
                i += 2
                continue
            if not ch.isspace():
                on_code("punct", ch)
            i += 1
