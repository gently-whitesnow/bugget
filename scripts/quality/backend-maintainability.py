#!/usr/bin/env python3
"""Бюджет поддерживаемости бекенда с ratchet-бейзлайном (гейт backend-maintainability).

Шесть метрик на C#-код: строки в файле, строки в типе, строки в методе, цикломатическая
сложность метода, число зависимостей конструктора, fan-out файла по using-директивам.

Правило то же, что у фронтового LOC-гейта: нарушение, которого нет в бейзлайне, — красный;
нарушение из бейзлайна не блокирует, но расти ему нельзя. Так новый код держит бюджет,
а 39 тысяч строк легаси не приходится разгребать до первого зелёного прогона (ADR-0002).

Профили и лимиты — в .quality/backend-maintainability.json, снимок — в
.quality/backend-maintainability-baseline.json. Правятся они, не скрипт.

  backend-maintainability.py                     проверить (профиль по умолчанию из конфига)
  backend-maintainability.py --profile strict    посмотреть целевой профиль, не блокируя
  backend-maintainability.py --update            пересобрать бейзлайн после рефакторинга

Разбор C# здесь лексический, без Roslyn: это осознанный размен — гейт не требует сборки
и работает за секунды. Семантику стережёт компилятор с TreatWarningsAsErrors.
"""

from __future__ import annotations

import argparse
import pathlib
import re
import sys
from dataclasses import dataclass

import quality_csharp as q

CONFIG = q.ROOT / ".quality" / "backend-maintainability.json"
BASELINE = q.ROOT / ".quality" / "backend-maintainability-baseline.json"

TYPE_KEYWORDS = {"class", "struct", "record", "interface", "enum"}
# Слова, за которыми тоже идёт «(...) {», но методом они не являются.
NOT_A_METHOD = {"if", "for", "foreach", "while", "switch", "catch", "using", "lock", "fixed", "when"}
CC_WORDS = {"if", "for", "foreach", "while", "case", "catch", "when"}
CC_PUNCTS = {"&&", "||", "??"}

USING_DIRECTIVE = re.compile(
    r"^\s*(?:global\s+)?using(?:\s+static)?\s+"
    r"(?:[A-Za-z_][A-Za-z0-9_]*\s*=\s*)?"
    r"([A-Za-z_][A-Za-z0-9_.]*)\s*;"
)

# Человекочитаемые названия метрик — они же ключи лимитов в конфиге.
METRICS = {
    "FILE_LOC": ("fileMaxLoc", "строк в файле"),
    "TYPE_LOC": ("typeMaxLoc", "строк в типе"),
    "METHOD_LOC": ("methodMaxLoc", "строк в методе"),
    "METHOD_CC": ("methodMaxCyclomaticComplexity", "цикломатическая сложность"),
    "CTOR_DEPS": ("constructorMaxDependencies", "зависимостей конструктора"),
    "FILE_FANOUT": ("fileMaxFanOut", "fan-out по using"),
}


@dataclass(frozen=True)
class Token:
    value: str
    line: int
    kind: str


@dataclass(frozen=True)
class TypeSpan:
    name: str
    is_record: bool
    line: int
    open_index: int
    close_index: int
    primary_ctor_params: int | None


@dataclass(frozen=True)
class Violation:
    code: str
    path: str
    line: int
    subject: str
    actual: int
    limit: int

    @property
    def key(self) -> tuple[str, str, str]:
        """Ключ в бейзлайне. Номер строки не входит: правка выше по файлу не должна
        превращать старое нарушение в новое."""
        return (self.code, self.path, self.subject)


# --------------------------------------------------------------------------------------
# Лексика
# --------------------------------------------------------------------------------------


def tokenize(lines: list[str]) -> tuple[list[Token], list[bool]]:
    """Токены файла и флаг «в строке есть код» для каждой строки."""
    tokens: list[Token] = []
    has_code: list[bool] = []
    scanner = q.LineScanner()

    for number, line in enumerate(lines, start=1):
        found = False

        def collect(kind: str, value: str, number=number) -> None:
            nonlocal found
            found = True
            tokens.append(Token(value, number, kind))

        scanner.scan(line, collect)
        has_code.append(found)

    return tokens, has_code


def count_loc(has_code: list[bool], start_line: int, end_line: int) -> int:
    return sum(1 for flag in has_code[start_line - 1 : end_line] if flag)


def build_brace_map(tokens: list[Token]) -> dict[int, int]:
    stack: list[int] = []
    mapping: dict[int, int] = {}
    for index, token in enumerate(tokens):
        if token.value == "{":
            stack.append(index)
        elif token.value == "}" and stack:
            opened = stack.pop()
            mapping[opened] = index
            mapping[index] = opened
    return mapping


def match_forward(tokens: list[Token], left_index: int, left: str, right: str, stop: int) -> int | None:
    depth = 0
    for index in range(left_index, min(stop, len(tokens))):
        value = tokens[index].value
        if value == left:
            depth += 1
        elif value == right:
            depth -= 1
            if depth == 0:
                return index
    return None


def match_backward(tokens: list[Token], right_index: int, left: str, right: str) -> int | None:
    depth = 0
    for index in range(right_index, -1, -1):
        value = tokens[index].value
        if value == right:
            depth += 1
        elif value == left:
            depth -= 1
            if depth == 0:
                return index
    return None


def count_parameters(tokens: list[Token], open_paren: int, close_paren: int) -> int:
    """Число параметров между скобками: запятые верхнего уровня плюс один."""
    inner = tokens[open_paren + 1 : close_paren]
    if not inner:
        return 0

    count = 1
    depth = 0
    for token in inner:
        if token.value in "([<":
            depth += 1
        elif token.value in ")]>":
            depth = max(0, depth - 1)
        elif token.value == "," and depth == 0:
            count += 1
    return count


def find_type_spans(tokens: list[Token], brace_map: dict[int, int]) -> list[TypeSpan]:
    spans: list[TypeSpan] = []
    index = 0
    while index < len(tokens):
        token = tokens[index]
        if token.kind != "word" or token.value not in TYPE_KEYWORDS:
            index += 1
            continue

        is_record = token.value == "record"
        cursor = index + 1
        if is_record and cursor < len(tokens) and tokens[cursor].value in {"class", "struct"}:
            cursor += 1

        # Имя типа — первое слово после ключевого слова.
        name_index = None
        for probe in range(cursor, min(len(tokens), cursor + 4)):
            if tokens[probe].kind == "word":
                name_index = probe
                break
        if name_index is None:
            index += 1
            continue

        open_brace = None
        primary_params = None
        stop = min(len(tokens), name_index + 200)
        cursor = name_index + 1
        while cursor < stop:
            value = tokens[cursor].value
            if value == ";":
                break
            if value == "(" and primary_params is None:
                close_paren = match_forward(tokens, cursor, "(", ")", stop)
                if close_paren is None:
                    break
                primary_params = count_parameters(tokens, cursor, close_paren)
                cursor = close_paren + 1
                continue
            if value == "{":
                open_brace = cursor
                break
            cursor += 1

        if open_brace is None or open_brace not in brace_map:
            index += 1
            continue

        spans.append(
            TypeSpan(
                name=tokens[name_index].value,
                is_record=is_record,
                line=token.line,
                open_index=open_brace,
                close_index=brace_map[open_brace],
                primary_ctor_params=primary_params,
            )
        )
        index = name_index + 1

    return spans


def find_methods(tokens: list[Token], brace_map: dict[int, int], span: TypeSpan):
    """Тела методов типа: (имя, индекс начала сигнатуры, индекс {, индекс }, параметров).

    Метод опознаётся по форме «имя ( ... ) {» на верхнем уровне тела типа. Лямбды и
    выражения-тела отсекаются: у них перед блоком стоит «=>».
    """
    methods = []
    index = span.open_index + 1
    while index < span.close_index:
        if tokens[index].value != "{" or index not in brace_map:
            index += 1
            continue

        previous = index - 1
        if previous <= span.open_index or tokens[previous].value != ")":
            index += 1
            continue

        open_paren = match_backward(tokens, previous, "(", ")")
        if open_paren is None or open_paren <= span.open_index:
            index += 1
            continue

        name_index = open_paren - 1
        if name_index <= span.open_index or tokens[name_index].kind != "word":
            index += 1
            continue
        if tokens[name_index].value in NOT_A_METHOD:
            index += 1
            continue

        # Сигнатура начинается после предыдущего разделителя верхнего уровня.
        signature_start = span.open_index + 1
        for probe in range(open_paren, span.open_index, -1):
            if tokens[probe].value in {";", "}", "{"}:
                signature_start = probe + 1
                break

        if any(tokens[i].value == "=>" for i in range(signature_start, index)):
            index += 1
            continue

        close_index = brace_map[index]
        methods.append(
            (
                tokens[name_index].value,
                signature_start,
                index,
                close_index,
                count_parameters(tokens, open_paren, previous),
            )
        )
        index = close_index + 1

    return methods


def cyclomatic(tokens: list[Token], start: int, end: int) -> int:
    complexity = 1
    for index in range(start, end + 1):
        token = tokens[index]
        if token.kind == "word" and token.value in CC_WORDS:
            complexity += 1
        elif token.kind == "punct" and token.value in CC_PUNCTS:
            complexity += 1
    return complexity


def fan_out(lines: list[str], ignore_prefixes: list[str]) -> int:
    namespaces = set()
    for line in lines:
        match = USING_DIRECTIVE.match(line)
        if not match:
            continue
        namespace = match.group(1)
        if any(namespace == p or namespace.startswith(p + ".") for p in ignore_prefixes):
            continue
        namespaces.add(namespace)
    return len(namespaces)


# --------------------------------------------------------------------------------------
# Замер
# --------------------------------------------------------------------------------------


def measure_file(path: pathlib.Path, limits: dict, ignore_prefixes: list[str]) -> list[Violation]:
    lines = q.read_lines(path)
    tokens, has_code = tokenize(lines)
    brace_map = build_brace_map(tokens)
    relative = q.rel(path)
    found: list[Violation] = []

    def check(code: str, line: int, subject: str, actual: int) -> None:
        limit = limits[METRICS[code][0]]
        if actual > limit:
            found.append(Violation(code, relative, line, subject, actual, limit))

    check("FILE_LOC", 1, relative, count_loc(has_code, 1, len(lines)))
    check("FILE_FANOUT", 1, relative, fan_out(lines, ignore_prefixes))

    for span in find_type_spans(tokens, brace_map):
        start = tokens[span.open_index].line
        end = tokens[span.close_index].line
        check("TYPE_LOC", span.line, span.name, count_loc(has_code, start, end))

        # У record-ов параметры первичного конструктора — это поля данных,
        # а не внедрённые зависимости, поэтому бюджет DI к ним не применяется.
        if not span.is_record and span.primary_ctor_params:
            check("CTOR_DEPS", span.line, f"{span.name} (первичный конструктор)", span.primary_ctor_params)

        for name, signature_start, open_index, close_index, parameters in find_methods(tokens, brace_map, span):
            line = tokens[signature_start].line
            subject = f"{span.name}.{name}"
            check("METHOD_LOC", line, subject, count_loc(has_code, line, tokens[close_index].line))
            check("METHOD_CC", line, subject, cyclomatic(tokens, open_index, close_index))
            if not span.is_record and name == span.name:
                check("CTOR_DEPS", line, subject, parameters)

    return found


def measure(config: dict, profile_name: str) -> tuple[list[Violation], int]:
    profiles = config.get("profiles") or {}
    profile = profiles.get(profile_name)
    if not isinstance(profile, dict):
        raise SystemExit(f"нет такого профиля: {profile_name} (есть: {', '.join(sorted(profiles)) or '—'})")

    limits = profile.get("limits") or {}
    missing = [key for key, _ in METRICS.values() if key not in limits]
    if missing:
        raise SystemExit(f"в профиле {profile_name} не заданы лимиты: {', '.join(missing)}")

    files = q.find_csharp_files(config.get("roots") or [], config.get("exclude") or [])
    ignore_prefixes = config.get("fanOutIgnorePrefixes") or []

    violations: list[Violation] = []
    for path in files:
        violations.extend(measure_file(path, limits, ignore_prefixes))

    violations.sort(key=lambda v: (v.path, v.line, v.code, v.subject))
    return violations, len(files)


# --------------------------------------------------------------------------------------
# Бейзлайн
# --------------------------------------------------------------------------------------


def load_baseline() -> tuple[str, dict[tuple[str, str, str], int]]:
    if not BASELINE.exists():
        return "", {}
    payload = q.read_json(BASELINE)
    entries = {}
    for item in payload.get("violations") or []:
        entries[(item["code"], item["path"], item["subject"])] = int(item["actual"])
    return payload.get("profile", ""), entries


def save_baseline(profile_name: str, violations: list[Violation]) -> None:
    q.write_json(
        BASELINE,
        {
            "$comment": (
                "Снимок нарушений бюджета поддерживаемости на момент ввода гейта. "
                "Читается scripts/quality/backend-maintainability.py. Записи не блокируют, "
                "но значение может только уменьшаться. Пересобрать после рефакторинга: "
                "python3 scripts/quality/backend-maintainability.py --update. "
                "Рост записи или новая запись — это ослабление гейта, оно требует "
                "обоснования в теле коммита (ADR-0002)."
            ),
            "profile": profile_name,
            "violations": [
                {"code": v.code, "path": v.path, "subject": v.subject, "actual": v.actual}
                for v in violations
            ],
        },
    )


# --------------------------------------------------------------------------------------
# Вывод
# --------------------------------------------------------------------------------------


def describe(violation: Violation) -> str:
    _, title = METRICS[violation.code]
    return (
        f"{violation.path}:{violation.line} — {violation.subject}: "
        f"{title} {violation.actual} (лимит {violation.limit})"
    )


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Бюджет поддерживаемости бекенда с ratchet-бейзлайном.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    parser.add_argument("--profile", help="профиль лимитов; по умолчанию defaultProfile из конфига")
    parser.add_argument("--update", action="store_true", help="пересобрать бейзлайн из текущего состояния")
    parser.add_argument("--max", type=int, default=40, help="сколько нарушений печатать (по умолчанию 40)")
    args = parser.parse_args()

    config = q.read_json(CONFIG)
    default_profile = config.get("defaultProfile") or "legacy"
    profile_name = args.profile or default_profile

    violations, scanned = measure(config, profile_name)

    print(
        f"Бюджет поддерживаемости: профиль {profile_name}, "
        f"проверено файлов: {scanned}, нарушений: {len(violations)}"
    )

    if args.update:
        if profile_name != default_profile:
            raise SystemExit(
                f"бейзлайн ведётся для профиля {default_profile}; "
                f"--update с --profile {profile_name} записал бы чужой снимок"
            )
        save_baseline(profile_name, violations)
        print(f"Бейзлайн пересобран: {q.rel(BASELINE)} ({len(violations)} запис(ей))")
        return 0

    baseline_profile, baseline = load_baseline()

    # Чужой профиль (например, целевой strict) сравнивать со снимком нельзя —
    # такой прогон только показывает картину и гейт не валит.
    if profile_name != default_profile:
        for violation in violations[: args.max]:
            print(f"  · {describe(violation)}")
        if len(violations) > args.max:
            print(f"  … ещё {len(violations) - args.max}")
        print()
        print(f"Профиль {profile_name} — справочный прогон, бейзлайн ведётся для {default_profile}.")
        return 0

    if baseline_profile and baseline_profile != profile_name:
        raise SystemExit(
            f"бейзлайн снят с профиля {baseline_profile}, а проверка идёт по {profile_name}; "
            f"пересобери снимок: python3 scripts/quality/backend-maintainability.py --update"
        )

    current = {v.key: v for v in violations}
    new = [v for v in violations if v.key not in baseline]
    grown = [v for v in violations if v.key in baseline and v.actual > baseline[v.key]]
    shrunk = [v for v in violations if v.key in baseline and v.actual < baseline[v.key]]
    gone = [key for key in baseline if key not in current]

    if shrunk or gone:
        print()
        print("бейзлайн можно подтянуть (--update):")
        for violation in shrunk[: args.max]:
            print(f"  - {describe(violation)}, в бейзлайне {baseline[violation.key]}")
        for code, path, subject in sorted(gone)[: args.max]:
            print(f"  - {path} — {subject}: нарушения {code} больше нет")

    if not new and not grown:
        print()
        print("Бюджет соблюдён: новых нарушений и роста относительно бейзлайна нет.")
        return 0

    print()
    if new:
        print(f"новые нарушения ({len(new)}) — этого в бейзлайне нет:")
        for violation in new[: args.max]:
            print(f"  ✘ {describe(violation)}")
    if grown:
        print(f"нарушения из бейзлайна выросли ({len(grown)}) — им можно только уменьшаться:")
        for violation in grown[: args.max]:
            print(f"  ✘ {describe(violation)}, было {baseline[violation.key]}")

    print()
    print("Чини причину: разбей файл, вынеси метод, сократи конструктор.")
    print("Если превышение осознанное — пересобери бейзлайн отдельным коммитом с обоснованием:")
    print("  python3 scripts/quality/backend-maintainability.py --update")
    return 1


if __name__ == "__main__":
    sys.exit(main())
