#!/usr/bin/env python3
"""Имена полей body-схем: канон wire и обратимость проекции фронта.

Часть гейта frontend-contracts (`frontend-openapi-check.sh`). Нужен python3.

Фронт держит одну границу представлений: тело HTTP описано контрактом в
`snake_case`, а код фронта работает с `camelCase`; перекладывает ключи
интерсептор `frontend/src/shared/api/instances/base.ts`, типы выводятся из
сгенерированных схем через `Camelized<T>` (ADR-0009).

Проверяется три вещи — все на именах свойств `components.schemas` в
`frontend/src/shared/api/generated/*.d.ts`, то есть ровно на телах запросов и
ответов (query и path лежат в `components.parameters`/`operations`, конверсию не
проходят и сюда не попадают):

1. **Канон имени.** Имя обязано быть `snake_case` из строчных букв, цифр и `_`:
   `^[a-z][a-z0-9]*(_[a-z0-9]+)*$`. `bad-name`, `1bad`, `User_id` каноном не
   являются — на проводе их быть не должно.
2. **Обратимость.** `wire -> camelCase -> wire` обязан вернуть исходное имя:
   `user_ID` уехал бы в `userID` и вернулся как `user_i_d`, то есть клиент
   отправил бы имя, которого в контракте нет, и заметно это стало бы в рантайме
   у заказчика, а не на ревью. Канон п.1 это уже покрывает, но проверка идёт
   отдельно: она описывает саму проекцию, а не соглашение об именах.
3. **Свободных ключей нет.** Словарь `{ [ключ клиента]: значение }` в теле
   неразличим с набором имён полей: рекурсивная конверсия переписала бы данные
   клиента вместе с именами. Индексная сигнатура в схеме — красный гейт
   (ADR-0009); ровно поэтому счётчики репортов отдаются массивом.

Исключены схемы `*ProblemDetails`: `application/problem+json` не конвертируется
вовсе, поэтому и `traceId`, и словарь `errors` с wire-именами полей формы —
законны (ADR-0008).

Имена читаются и в кавычках, и без: `openapi-typescript` кавычит всё, что не
является идентификатором TS, и именно такие имена интереснее всего.

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
# интерсептор не конвертирует вовсе (ADR-0008).
EXEMPT_SCHEMA_SUFFIX = "ProblemDetails"

# Свойство схемы: `name?: ...`, `"quoted-name": ...`.
PROPERTY = re.compile(
    r'^(?P<indent> *)(?:"(?P<quoted>[^"]*)"|(?P<bare>[A-Za-z_$][A-Za-z0-9_$]*))\??:'
)
# Индексная сигнатура: `[key: string]: ...` — свободный словарь.
INDEX_SIGNATURE = re.compile(r"^(?P<indent> *)\[[^\]]*\]\??:")

# Канон имени поля на проводе.
WIRE_NAME = re.compile(r"^[a-z][a-z0-9]*(_[a-z0-9]+)*$")

SCHEMA_INDENT = 8

PROPERTY_KIND = "property"
FREE_KEY_KIND = "free-key"


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


def is_wire_name(name: str) -> bool:
    return bool(WIRE_NAME.match(name))


def schema_entries(text: str) -> list[tuple[str, str, str]]:
    """(имя схемы, вид, имя свойства) для всего блока components.schemas.

    Вид — `property` для обычного поля и `free-key` для индексной сигнатуры;
    именем во втором случае служит сам текст сигнатуры, чтобы его было видно
    в отчёте.
    """
    lines = text.splitlines()
    try:
        start = lines.index("    schemas: {")
    except ValueError:
        return []

    found: list[tuple[str, str, str]] = []
    schema = "?"
    for line in lines[start + 1:]:
        if line == "    };":
            break

        index = INDEX_SIGNATURE.match(line)
        if index:
            found.append((schema, FREE_KEY_KIND, line.strip().rstrip(";")))
            continue

        match = PROPERTY.match(line)
        if not match:
            continue
        indent = len(match.group("indent"))
        name = match.group("quoted")
        if name is None:
            name = match.group("bare")
        if indent <= SCHEMA_INDENT and match.group("quoted") is None:
            schema = name
            continue
        found.append((schema, PROPERTY_KIND, name))
    return found


def failures_for(text: str) -> tuple[list[tuple[str, str]], int]:
    """(что нарушено, почему) плюс число проверенных имён."""
    failures: list[tuple[str, str]] = []
    checked = 0
    for schema, kind, name in schema_entries(text):
        if schema.endswith(EXEMPT_SCHEMA_SUFFIX):
            continue
        checked += 1

        if kind == FREE_KEY_KIND:
            failures.append(
                (
                    schema,
                    f"свободный ключ `{name}`: словарь с ключами клиента "
                    "неотличим от имён полей",
                )
            )
            continue

        if not is_wire_name(name):
            failures.append((f"{schema}.{name}", "не snake_case канона провода"))
            continue

        if not round_trips(name):
            camel = snake_to_camel(name)
            failures.append(
                (f"{schema}.{name}", f"{name} -> {camel} -> {camel_to_snake(camel)}")
            )
    return failures, checked


def check(generated_dir: pathlib.Path) -> int:
    files = sorted(generated_dir.glob("*.d.ts"))
    if not files:
        print(f"error: в {generated_dir} нет сгенерированных .d.ts", file=sys.stderr)
        return 2

    failures: list[str] = []
    checked = 0
    for path in files:
        file_failures, file_checked = failures_for(path.read_text(encoding="utf-8"))
        failures.extend(
            f"{path.name}: {where} — {why}" for where, why in file_failures
        )
        checked += file_checked

    if failures:
        print("имена полей body-схем не проходят проверку:", file=sys.stderr)
        for failure in failures:
            print(f"  {failure}", file=sys.stderr)
        print(
            "\nИмя поля тела обязано быть snake_case провода и переживать\n"
            "wire -> camelCase -> wire, а свободных словарей в теле быть не должно:\n"
            "ключ клиента неотличим от имени поля и будет переписан конверсией.\n"
            "Чинится в specs/contracts/**/openapi.yaml (ADR-0009).",
            file=sys.stderr,
        )
        return 1

    print(f"имена полей body-схем в порядке ({checked} имён в {len(files)} файлах)")
    return 0


# Фикстура ровно того вида, который выдаёт openapi-typescript: кавыченные имена,
# индексная сигнатура и исключённая схема problem+json.
SELF_TEST_FIXTURE = """export interface components {
    schemas: {
        Good: {
            good_name: string;
            nested: {
                inner_name: number;
            };
        };
        Bad: {
            "bad-name": string;
            "1bad"?: number;
            user_ID: string;
            "user-ID": string;
        };
        FreeDict: {
            counts: {
                [key: string]: number;
            };
        };
        ProblemDetails: {
            traceId: string;
            errors?: {
                [key: string]: string[];
            };
        };
    };
    responses: {
    };
}
"""


def self_test() -> int:
    failed = 0

    def expect(label: str, got: object, want: object) -> None:
        nonlocal failed
        if got != want:
            print(f"  ПРОВАЛ  {label}: {got!r}, ожидалось {want!r}", file=sys.stderr)
            failed += 1
        else:
            print(f"  ok      {label}")

    for name, want in [
        ("report_id", True),
        ("is_excluded_from_analytics", True),
        ("id", True),
        ("counts", True),
        ("user_ID", False),
        ("traceId", False),
        ("report__id", False),
        ("Report_id", False),
    ]:
        expect(f"round-trip {name}", round_trips(name), want)

    for name, want in [
        ("report_id", True),
        ("count2", True),
        ("bad-name", False),
        ("1bad", False),
        ("User_id", False),
        ("user__id", False),
        ("traceId", False),
    ]:
        expect(f"канон {name}", is_wire_name(name), want)

    # Разбор фикстуры: кавыченные имена и индексная сигнатура обязаны доходить до
    # проверки, а не пропадать молча — первая версия парсера теряла именно их.
    entries = schema_entries(SELF_TEST_FIXTURE)
    expect(
        "разбор: имена схемы Bad",
        [name for schema, kind, name in entries if schema == "Bad"],
        ["bad-name", "1bad", "user_ID", "user-ID"],
    )
    expect(
        "разбор: вложенное поле схемы Good",
        [name for schema, kind, name in entries if schema == "Good"],
        ["good_name", "nested", "inner_name"],
    )
    expect(
        "разбор: свободный ключ виден",
        [(schema, name) for schema, kind, name in entries if kind == FREE_KEY_KIND],
        [("FreeDict", "[key: string]: number"), ("ProblemDetails", "[key: string]: string[]")],
    )

    failures, _ = failures_for(SELF_TEST_FIXTURE)
    expect(
        "фикстура: краснеет на каждом дефекте и не трогает problem+json",
        sorted(where for where, _why in failures),
        ["Bad.1bad", "Bad.bad-name", "Bad.user-ID", "Bad.user_ID", "FreeDict"],
    )

    if failed:
        print(f"самопроверка не прошла: провалов {failed}", file=sys.stderr)
        return 1
    print("самопроверка пройдена")
    return 0


def main() -> int:
    if "--self-test" in sys.argv[1:]:
        return self_test()
    generated = pathlib.Path(os.environ.get("BUGGET_GENERATED_DIR", DEFAULT_GENERATED))
    return check(generated)


if __name__ == "__main__":
    sys.exit(main())
