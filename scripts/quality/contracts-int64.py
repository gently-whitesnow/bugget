#!/usr/bin/env python3
"""Гейт публичного Int64: в specs/contracts/** нет `format: int64`, а канон описан схемой.

Зачем. У API ровно один клиент, и JSON-число в нём — IEEE-754 double. Поле, объявленное
`type: integer, format: int64`, оба генератора превращают в число (`long` на бекенде,
`number` на фронте), и всё, что больше 2^53−1, теряет точность молча: `9007199254740993`
доезжает до UI как `9007199254740992`, а ссылка, ключ списка и следующий запрос уходят на
соседнюю запись. Компилятор такую потерю не видит — оба типа валидны.

Поэтому публичный неотрицательный Int64 ходит строкой канона `Int64String`
(specs/contracts/shared.yaml), а этот гейт держит два инварианта:

  1) ни в одном контракте нет `format: int64` — ни у схемы, ни у параметра;
  2) `Int64String` на месте, это строка с `pattern`, и pattern описывает канон точно:
     принимает `0` и `[1-9][0-9]*` до 9223372036854775807 включительно и отвергает
     всё остальное — знак, ведущие нули, экспоненту, разделители, не-ASCII цифры и
     любое значение вне диапазона.

Второй инвариант проверяется вектором значений, а не глазами: «pattern есть» и «pattern
описывает то, что обещает описание» — разные утверждения, и первое без второго ничего
не стоит.

Сравнение с pattern повторяет семантику .NET RegularExpressionAttribute: совпадение
обязано покрыть строку целиком, иначе `123\\n` прошёл бы за счёт `$`.

  contracts-int64.py              проверить
  contracts-int64.py --self-test  проверить, что гейт краснеет там, где обязан
"""

from __future__ import annotations

import argparse
import pathlib
import re
import shutil
import sys
import tempfile

ROOT = pathlib.Path(__file__).resolve().parents[2]
CONTRACTS = "specs/contracts"
SHARED = "shared.yaml"
SCHEMA = "Int64String"

# Ключ `format: int64` именно как узел документа: те же слова внутри description —
# это объяснение, а не объявление типа, и краснеть на нём нечему.
FORMAT_INT64 = re.compile(r"^\s*format:\s*['\"]?int64['\"]?\s*(#.*)?$")

# Схема shared.yaml: заголовок по отступу, тело — до следующего заголовка того же уровня.
SCHEMA_HEADER = re.compile(rf"^(?P<indent>\s+){SCHEMA}:\s*$")

MAX_INT64 = "9223372036854775807"

# Вектор канона. Значения подобраны по границам, а не по вкусу: 2^53±1 — там, где
# ломается double у клиента; 9223372036854775807/…808 — верхняя граница Int64 и первое
# значение за ней; остальное — формы записи, которые каноном не являются.
ACCEPTED = (
    "0",
    "1",
    "9007199254740992",
    "9007199254740993",
    "999999999999999999",
    "1000000000000000000",
    "922337203685477580",
    "9223372036854775806",
    MAX_INT64,
)

REJECTED = (
    "",
    " ",
    "-1",
    "+1",
    "00",
    "007",
    "0.0",
    "1.0",
    "1e3",
    "1_000",
    "1 000",
    " 1",
    "1 ",
    "abc",
    "0x10",
    "١٢٣",
    "9223372036854775808",
    "9223372036854775810",
    "9999999999999999999",
    "99999999999999999999",
    "9007199254740993\n",
)


def matches(pattern: re.Pattern[str], value: str) -> bool:
    """Совпадение по правилам .NET RegularExpressionAttribute: строка покрыта целиком."""
    match = pattern.match(value)
    return match is not None and match.start() == 0 and match.end() == len(value)


def read_pattern(shared: str) -> tuple[str | None, list[str]]:
    """Достаёт `pattern` схемы Int64String из текста shared.yaml."""
    problems: list[str] = []
    lines = shared.splitlines()

    start = None
    indent = ""
    for index, line in enumerate(lines):
        header = SCHEMA_HEADER.match(line)
        if header:
            start = index + 1
            indent = header.group("indent")
            break

    if start is None:
        problems.append(
            f"{CONTRACTS}/{SHARED}: схема {SCHEMA} не найдена — публичному Int64 нечем "
            f"быть, а модулям не на что ссылаться"
        )
        return None, problems

    body = []
    for line in lines[start:]:
        if line.strip() and not line.startswith(indent + " "):
            break
        body.append(line)

    text = "\n".join(body)
    if not re.search(r"^\s*type:\s*string\s*$", text, re.MULTILINE):
        problems.append(f"{CONTRACTS}/{SHARED}: {SCHEMA} обязана быть `type: string`")

    pattern = re.search(r"^\s*pattern:\s*'(?P<value>.+)'\s*$", text, re.MULTILINE)
    if not pattern:
        problems.append(
            f"{CONTRACTS}/{SHARED}: у {SCHEMA} нет `pattern` — без него схема описывает "
            f"любую строку, а не канон Int64"
        )
        return None, problems

    return pattern.group("value"), problems


def check(contracts: pathlib.Path) -> list[str]:
    problems: list[str] = []

    for spec in sorted(contracts.rglob("*.yaml")):
        relative = spec.relative_to(contracts.parent.parent)
        for number, line in enumerate(spec.read_text(encoding="utf-8").splitlines(), 1):
            if FORMAT_INT64.match(line):
                problems.append(
                    f"{relative}:{number}: `format: int64` в публичном контракте — "
                    f"замените схему на $ref '../{SHARED}#/components/schemas/{SCHEMA}'"
                )

    shared = contracts / SHARED
    if not shared.is_file():
        problems.append(f"{CONTRACTS}/{SHARED} не найден")
        return problems

    pattern_text, pattern_problems = read_pattern(shared.read_text(encoding="utf-8"))
    problems.extend(pattern_problems)
    if pattern_text is None:
        return problems

    try:
        pattern = re.compile(pattern_text)
    except re.error as error:
        problems.append(f"{CONTRACTS}/{SHARED}: pattern схемы {SCHEMA} не компилируется: {error}")
        return problems

    for value in ACCEPTED:
        if not matches(pattern, value):
            problems.append(
                f"{CONTRACTS}/{SHARED}: pattern схемы {SCHEMA} отвергает каноничное "
                f"значение {value!r}"
            )

    for value in REJECTED:
        if matches(pattern, value):
            problems.append(
                f"{CONTRACTS}/{SHARED}: pattern схемы {SCHEMA} принимает неканоничное "
                f"значение {value!r}"
            )

    return problems


def self_test() -> int:
    """Гейт, который не краснеет там, где обязан, хуже отсутствующего."""
    source = ROOT / CONTRACTS
    scenarios: list[tuple[str, object, bool]] = [
        ("контракты как есть", lambda _: None, False),
        (
            "в модуль вернули `format: int64`",
            lambda box: _patch(
                box / "reports" / "openapi.yaml",
                "          $ref: '../shared.yaml#/components/schemas/Int64String'",
                "          type: integer\n          format: int64",
                once=True,
            ),
            True,
        ),
        (
            "flow-style `format: int64` тоже запрещён",
            lambda box: _patch(
                box / "reports" / "openapi.yaml",
                "        total:\n          $ref: '../shared.yaml#/components/schemas/Int64String'",
                "        total: { type: integer, format: int64 }",
                once=True,
            ),
            True,
        ),
        (
            "pattern ослаблен до любых цифр — пускает значение за Int64",
            lambda box: _patch_pattern(box, r"^[0-9]+$"),
            True,
        ),
        (
            "pattern пускает ведущие нули",
            lambda box: _patch_pattern(box, r"^[0-9]{1,19}$"),
            True,
        ),
        (
            "pattern отвергает каноничное значение (потерян ноль)",
            lambda box: _patch_pattern(box, r"^[1-9][0-9]{0,17}$"),
            True,
        ),
        (
            "схему Int64String удалили",
            lambda box: _drop_schema(box),
            True,
        ),
    ]

    failed = 0
    for title, mutate, must_be_red in scenarios:
        with tempfile.TemporaryDirectory() as tmp:
            box = pathlib.Path(tmp) / "contracts"
            shutil.copytree(source, box)
            mutate(box)
            is_red = bool(check(box))
            if is_red == must_be_red:
                print(f"  ok   {title}: {'красный' if is_red else 'зелёный'}")
            else:
                failed += 1
                print(f"  ФЕЙЛ {title}: ожидался {'красный' if must_be_red else 'зелёный'}, вышло наоборот")

    print()
    if failed:
        print(f"Самопроверка не прошла: сценариев с неверным результатом — {failed}.")
        return 1
    print(f"Самопроверка прошла: {len(scenarios)} сценариев, гейт краснеет ровно там, где обязан.")
    return 0


def _patch(path: pathlib.Path, old: str, new: str, once: bool = False) -> None:
    text = path.read_text(encoding="utf-8")
    path.write_text(text.replace(old, new, 1 if once else -1), encoding="utf-8")


def _patch_pattern(box: pathlib.Path, pattern: str) -> None:
    shared = box / SHARED
    text = shared.read_text(encoding="utf-8")
    text = re.sub(r"^(\s*)pattern: '.+'$", rf"\1pattern: '{pattern}'", text, count=1, flags=re.MULTILINE)
    shared.write_text(text, encoding="utf-8")


def _drop_schema(box: pathlib.Path) -> None:
    shared = box / SHARED
    lines = shared.read_text(encoding="utf-8").splitlines()
    kept, skipping, indent = [], False, ""
    for line in lines:
        header = SCHEMA_HEADER.match(line)
        if header:
            skipping, indent = True, header.group("indent")
            continue
        if skipping and (not line.strip() or line.startswith(indent + " ")):
            continue
        skipping = False
        kept.append(line)
    shared.write_text("\n".join(kept) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(
        description=f"Гейт публичного Int64: нет `format: int64`, канон описан схемой {SCHEMA}."
    )
    parser.add_argument("--self-test", action="store_true", help="проверить, что гейт краснеет там, где обязан")
    args = parser.parse_args()

    if args.self_test:
        print("Самопроверка гейта contracts-int64\n")
        return self_test()

    problems = check(ROOT / CONTRACTS)
    if not problems:
        print(
            f"Публичный Int64 в норме: `format: int64` в {CONTRACTS}/** нет, "
            f"pattern схемы {SCHEMA} совпадает с каноном "
            f"({len(ACCEPTED)} принимаемых и {len(REJECTED)} отвергаемых значений)."
        )
        return 0

    print(f"Публичный Int64 разошёлся с каноном ({len(problems)}):")
    for problem in problems:
        print(f"  ✘ {problem}")
    print()
    print("Неотрицательный 64-битный идентификатор или счётчик уходит наружу строкой:")
    print(f"числом он теряет точность у единственного клиента API. Канон — {CONTRACTS}/{SHARED}.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
