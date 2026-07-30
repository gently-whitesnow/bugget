#!/usr/bin/env python3
"""Round-trip имён полей body-схем: wire -> camelCase -> wire.

Часть гейта frontend-contracts (`frontend-openapi-check.sh`).

Фронт держит одну границу представлений: тело HTTP описано контрактом в
`snake_case`, а код фронта работает с `camelCase`; перекладывает ключи
интерсептор `frontend/src/shared/api/instances/base.ts`, типы выводятся из
сгенерированных схем через `Camelized<T>`. Проекция обратима не для любого
имени: `user_ID` уедет в `userID` и вернётся как `user_i_d`, то есть клиент
отправит на проводе не то имя, которое описано в контракте, — и заметно это
станет в рантайме у заказчика, а не на ревью.

Поэтому имя, не переживающее round-trip, останавливает контрактную проверку
до того, как контракт кто-то начнёт использовать: чинится оно в
`specs/contracts/**/openapi.yaml`, а не обходится исключением на фронте.

Проверяются имена свойств в `components.schemas` сгенерированных
`frontend/src/shared/api/generated/*.d.ts` — это ровно тела запросов и ответов.
Query- и path-параметры лежат в `components.parameters` и в `operations`,
конверсию не проходят и здесь не проверяются.

Использование:
  scripts/quality/frontend-case-roundtrip.py            # каталог по умолчанию
  BUGGET_GENERATED_DIR=... scripts/quality/frontend-case-roundtrip.py
  scripts/quality/frontend-case-roundtrip.py --self-test
"""

from __future__ import annotations

import os
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
DEFAULT_GENERATED = ROOT / "frontend/src/shared/api/generated"

# Схемы, которые к границе wire<->UI не попадают: `application/problem+json`
# интерсептор не конвертирует вовсе (в `errors` лежат wire-имена полей формы,
# ADR-0008). Их имена (`traceId`) round-trip и не обязаны проходить.
EXEMPT_SCHEMA_SUFFIX = "ProblemDetails"

PROPERTY = re.compile(r'^(?P<indent> *)(?P<name>[A-Za-z_][A-Za-z0-9_]*)\??:')
SCHEMA_INDENT = 8


def capitalize(value: str) -> str:
    return value[:1].upper() + value[1:]


def snake_to_camel(value: str) -> str:
    pascal = "".join(capitalize(part) for part in value.split("_"))
    return pascal[:1].lower() + pascal[1:]


def camel_to_snake(value: str) -> str:
    # Мимикрия под String.prototype.split(/(?=[A-Z])/) из convertCases.ts:
    # JS не отдаёт пустой первый элемент при совпадении в нулевой позиции.
    parts = [part for part in re.split(r"(?=[A-Z])", value) if part != ""]
    return "_".join(part.lower() for part in parts)


def round_trips(name: str) -> bool:
    return camel_to_snake(snake_to_camel(name)) == name


def schema_property_names(text: str) -> list[tuple[str, str]]:
    """(имя схемы, имя свойства) для всего блока components.schemas."""
    lines = text.splitlines()
    try:
        start = lines.index("    schemas: {")
    except ValueError:
        return []

    found: list[tuple[str, str]] = []
    schema = "?"
    for line in lines[start + 1:]:
        if line == "    };":
            break
        match = PROPERTY.match(line)
        if not match:
            continue
        indent = len(match.group("indent"))
        name = match.group("name")
        if indent <= SCHEMA_INDENT:
            schema = name
            continue
        found.append((schema, name))
    return found


def check(generated_dir: pathlib.Path) -> int:
    files = sorted(generated_dir.glob("*.d.ts"))
    if not files:
        print(f"error: в {generated_dir} нет сгенерированных .d.ts", file=sys.stderr)
        return 2

    failures: list[str] = []
    checked = 0
    for path in files:
        for schema, name in schema_property_names(path.read_text(encoding="utf-8")):
            if schema.endswith(EXEMPT_SCHEMA_SUFFIX):
                continue
            checked += 1
            if round_trips(name):
                continue
            camel = snake_to_camel(name)
            failures.append(
                f"{path.name}: {schema}.{name} -> {camel} -> {camel_to_snake(camel)}"
            )

    if failures:
        print("round-trip имён полей не сходится:", file=sys.stderr)
        for failure in failures:
            print(f"  {failure}", file=sys.stderr)
        print(
            "\nИмя поля тела обязано переживать wire -> camelCase -> wire: фронт\n"
            "читает тело в camelCase и отправляет обратно в snake_case. Переименуйте\n"
            "поле в specs/contracts/**/openapi.yaml в обычный snake_case.",
            file=sys.stderr,
        )
        return 1

    print(f"round-trip имён полей сходится ({checked} имён в {len(files)} файлах)")
    return 0


def self_test() -> int:
    cases = [
        ("report_id", True),
        ("is_excluded_from_analytics", True),
        ("id", True),
        ("counts", True),
        ("user_ID", False),
        ("traceId", False),
        ("report__id", False),
        ("Report_id", False),
    ]
    failed = 0
    for name, want in cases:
        got = round_trips(name)
        if got != want:
            print(
                f"  ПРОВАЛ  {name}: round_trips={got}, ожидалось {want} "
                f"({name} -> {snake_to_camel(name)} -> {camel_to_snake(snake_to_camel(name))})",
                file=sys.stderr,
            )
            failed += 1
        else:
            print(f"  ok      {name}")
    if failed:
        print(f"самопроверка round-trip не прошла: провалов {failed}", file=sys.stderr)
        return 1
    print("самопроверка round-trip пройдена")
    return 0


def main() -> int:
    if "--self-test" in sys.argv[1:]:
        return self_test()
    generated = pathlib.Path(os.environ.get("BUGGET_GENERATED_DIR", DEFAULT_GENERATED))
    return check(generated)


if __name__ == "__main__":
    sys.exit(main())
