#!/usr/bin/env python3
"""Ratchet поверх послаблений компилятора и анализаторов (гейт backend-suppressions).

Гейт backend-build красный на любом предупреждении — но ровно до тех пор, пока
предупреждение не заглушено. Скрипт следит за тремя способами заглушить его и не даёт
списку расти молча:

  warnaserror  — проекты, которым выключен TreatWarningsAsErrors
                 (сейчас — список из backend/Directory.Build.props);
  nowarn       — коды в <NoWarn> в .csproj и .props;
  pragma       — #pragma warning disable в коде.

Правило: любое послабление, которого нет в снимке .quality/backend-suppress-baseline.json,
валит гейт. Новое допустимо только так: ссылка на ADR или на задачу в комментарии выше
и отдельный коммит с пере-бейзлайном (ADR-0002). Убрали послабление — снимок просто
уменьшается, руками его править не надо.

  backend-suppressions.py            проверить
  backend-suppressions.py --list     показать все найденные послабления
  backend-suppressions.py --update   пересобрать снимок
"""

from __future__ import annotations

import argparse
import pathlib
import re
import sys
from dataclasses import dataclass

import quality_csharp as q

BASELINE = q.ROOT / ".quality" / "backend-suppress-baseline.json"
BACKEND = q.ROOT / "backend"

# Сколько строк выше послабления читаем в поисках обоснования. Пустая строка
# обрывает блок: «то же, что выше» обоснованием не считается.
LOOKBACK = 15
JUSTIFICATION = re.compile(r"\bADR-\d{3,4}\b|\b[A-Z][A-Z0-9]{1,9}-\d{1,6}\b")

PRAGMA = re.compile(r"#pragma\s+warning\s+disable\s+([A-Za-z0-9, ]+)")
NOWARN = re.compile(r"<NoWarn>(.*?)</NoWarn>", re.IGNORECASE)
WARNASERROR_FALSE = re.compile(r"<TreatWarningsAsErrors>\s*false\s*</TreatWarningsAsErrors>", re.IGNORECASE)
PROPERTY_GROUP = re.compile(r"<PropertyGroup\b", re.IGNORECASE)
PROPERTY_GROUP_END = re.compile(r"</PropertyGroup>", re.IGNORECASE)
STARTS_WITH = re.compile(r"MSBuildProjectName\.StartsWith\('([^']+)'\)")
EQUALS = re.compile(r"'\$\(MSBuildProjectName\)'\s*==\s*'([^']+)'")

# Обоснование ищем только в комментариях: код, случайно похожий на ключ задачи,
# обоснованием не является.
COMMENTS = re.compile(r"<!--.*?-->|/\*.*?\*/|//[^\n]*", re.DOTALL)


@dataclass(frozen=True, order=True)
class Entry:
    kind: str
    location: str
    rule: str


def normalize_rule(code: str) -> str:
    """4711 и CS4711 — одно и то же послабление."""
    code = code.strip()
    return f"CS{code}" if code.isdigit() else code.upper()


def justification_above(lines: list[str], line_number: int) -> str:
    """Текст комментариев в LOOKBACK строках над строкой line_number (1-based)."""
    window = "\n".join(lines[max(0, line_number - 1 - LOOKBACK) : line_number - 1])
    found = " ".join(match.group(0) for match in COMMENTS.finditer(window))
    return " ".join(found.replace("<!--", " ").replace("-->", " ").replace("//", " ").split())


def collect() -> dict[Entry, tuple[int, str]]:
    """Все послабления: запись → (сколько раз встречается, обоснование над первым)."""
    found: dict[Entry, tuple[int, str]] = {}

    def add(kind: str, path: pathlib.Path, rule: str, lines: list[str], line_number: int) -> None:
        entry = Entry(kind, q.rel(path), rule)
        count, reason = found.get(entry, (0, ""))
        found[entry] = (count + 1, reason or justification_above(lines, line_number))

    for path in q.find_csharp_files(["backend"], []):
        lines = q.read_lines(path)
        for number, line in enumerate(lines, start=1):
            match = PRAGMA.search(line)
            if match:
                for code in match.group(1).split(","):
                    if code.strip():
                        add("pragma", path, normalize_rule(code), lines, number)

    for path in sorted(list(BACKEND.rglob("*.csproj")) + list(BACKEND.rglob("*.props"))):
        if "/obj/" in q.rel(path) or "/bin/" in q.rel(path):
            continue
        lines = q.read_lines(path)

        for number, line in enumerate(lines, start=1):
            for match in NOWARN.finditer(line):
                for code in match.group(1).split(";"):
                    code = code.strip()
                    # $(NoWarn) — это наследование уже учтённых кодов, а не новое послабление.
                    if code and not code.startswith("$("):
                        add("nowarn", path, normalize_rule(code), lines, number)

        for start, end in property_groups(lines):
            block = "\n".join(lines[start - 1 : end])
            if not WARNASERROR_FALSE.search(block):
                continue
            for project in projects_in_condition(block) or ["(весь файл)"]:
                add("warnaserror", path, project, lines, start)

    return found


def property_groups(lines: list[str]) -> list[tuple[int, int]]:
    """Границы блоков <PropertyGroup> ... </PropertyGroup> как (первая строка, последняя)."""
    groups: list[tuple[int, int]] = []
    start = None
    for number, line in enumerate(lines, start=1):
        if start is None and PROPERTY_GROUP.search(line):
            start = number
        if start is not None and PROPERTY_GROUP_END.search(line):
            groups.append((start, number))
            start = None
    return groups


def projects_in_condition(block: str) -> list[str]:
    """Проекты, на которые распространяется послабление, из Condition блока.

    StartsWith('Users.') читается как 'Users.*' — так снятие одного проекта из списка
    видно в снимке как исчезнувшая строка, а не как правка непонятного условия.
    """
    projects = [f"{prefix}*" for prefix in STARTS_WITH.findall(block)]
    projects += EQUALS.findall(block)
    return sorted(set(projects))


def load_baseline() -> dict[Entry, int]:
    if not BASELINE.exists():
        return {}
    payload = q.read_json(BASELINE)
    return {
        Entry(item["kind"], item["location"], item["rule"]): int(item.get("count", 1))
        for item in payload.get("entries") or []
    }


def save_baseline(current: dict[Entry, tuple[int, str]]) -> None:
    q.write_json(
        BASELINE,
        {
            "$comment": (
                "Снимок послаблений компилятора и анализаторов бекенда. Читается "
                "scripts/quality/backend-suppressions.py. Список может только уменьшаться: "
                "новая запись или рост count — красный гейт. Пересобрать: python3 "
                "scripts/quality/backend-suppressions.py --update, отдельным коммитом "
                "с обоснованием в его теле (ADR-0002)."
            ),
            "entries": [
                {"kind": entry.kind, "location": entry.location, "rule": entry.rule, "count": count}
                for entry, (count, _) in sorted(current.items())
            ],
        },
    )


def show(entry: Entry, count: int) -> str:
    suffix = f" ×{count}" if count > 1 else ""
    return f"[{entry.kind}] {entry.location} :: {entry.rule}{suffix}"


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Ratchet поверх послаблений компилятора и анализаторов.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    parser.add_argument("--list", action="store_true", help="показать все найденные послабления")
    parser.add_argument("--update", action="store_true", help="пересобрать снимок")
    args = parser.parse_args()

    current = collect()
    baseline = load_baseline()
    total = sum(count for count, _ in current.values())

    print(f"Послабления: записей {len(current)} (в снимке {len(baseline)}), всего вхождений {total}")

    if args.list:
        for entry, (count, reason) in sorted(current.items()):
            print(f"  {show(entry, count)}")
            if reason:
                print(f"      обоснование: {reason[:160]}")
        return 0

    if args.update:
        added = [e for e in current if e not in baseline]
        grown = [e for e in current if e in baseline and current[e][0] > baseline[e]]
        save_baseline(current)
        print(f"Снимок пересобран: {q.rel(BASELINE)}")
        if baseline and (added or grown):
            print()
            print("Снимок вырос — это ослабление гейта. Оно допустимо только отдельным")
            print("коммитом, в теле которого сказано, зачем послабление и когда снимается:")
            for entry in sorted(added):
                print(f"  + {show(entry, current[entry][0])}")
            for entry in sorted(grown):
                print(f"  ↑ {show(entry, current[entry][0])}, было {baseline[entry]}")
        return 0

    added = sorted(e for e in current if e not in baseline)
    grown = sorted(e for e in current if e in baseline and current[e][0] > baseline[e])
    shrunk = sorted(e for e in current if e in baseline and current[e][0] < baseline[e])
    gone = sorted(e for e in baseline if e not in current)

    if shrunk or gone:
        print()
        print("снимок можно подтянуть (--update):")
        for entry in gone:
            print(f"  - {show(entry, baseline[entry])}: послабления больше нет")
        for entry in shrunk:
            print(f"  - {show(entry, current[entry][0])}, в снимке было {baseline[entry]}")

    if not added and not grown:
        print()
        print("Новых послаблений относительно снимка нет.")
        return 0

    unjustified = [e for e in added if not JUSTIFICATION.search(current[e][1])]
    justified = [e for e in added if e not in unjustified]

    print()
    if unjustified:
        print(f"новые послабления без обоснования ({len(unjustified)}):")
        for entry in unjustified:
            print(f"  ✘ {show(entry, current[entry][0])}")
            print(f"      выше по файлу нет ссылки на ADR или на задачу (смотрим {LOOKBACK} строк)")
    if justified:
        print(f"новые послабления, которых нет в снимке ({len(justified)}):")
        for entry in justified:
            print(f"  ✘ {show(entry, current[entry][0])}")
    if grown:
        print(f"послаблений стало больше в тех же местах ({len(grown)}):")
        for entry in grown:
            print(f"  ✘ {show(entry, current[entry][0])}, в снимке {baseline[entry]}")

    print()
    print("Чини причину предупреждения, а не глуши его. Если послабление всё-таки нужно:")
    print("  1) комментарием выше сошлись на ADR или на задачу и объясни, когда снимется;")
    print("  2) отдельным коммитом пересобери снимок:")
    print("     python3 scripts/quality/backend-suppressions.py --update")
    return 1


if __name__ == "__main__":
    sys.exit(main())
