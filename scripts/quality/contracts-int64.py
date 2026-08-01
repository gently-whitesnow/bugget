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

Первый инвариант проверяется по значению узла, а не по виду строки. Запрещено само
объявление, а не одна его запись: блочный и flow-стиль, кавычки и экранирование
(`"int\\x36\\x34"`), блочный скаляр, алиас на якорь с тем же значением, значение на
следующей строке — это всё один и тот же `format: int64`. Гейт, который ловит одну
форму записи из десяти, обходится случайно, без злого умысла.

Документы разбирает закреплённый `js-yaml`, которым уже пользуется OpenAPI-toolchain.
Гейт рекурсивно обходит полученный AST, а ошибка YAML краснит проверку целиком.

  contracts-int64.py              проверить
  contracts-int64.py --self-test  проверить, что гейт краснеет там, где обязан
"""

from __future__ import annotations

import argparse
import json
import pathlib
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = pathlib.Path(__file__).resolve().parents[2]
CONTRACTS = "specs/contracts"
SHARED = "shared.yaml"
SCHEMA = "Int64String"

# Схема shared.yaml: заголовок по отступу, тело — до следующего заголовка того же уровня.
SCHEMA_HEADER = re.compile(rf"^(?P<indent>\s+){SCHEMA}:\s*$")
AST_SCANNER = ROOT / "scripts/quality/contracts-int64-ast.mjs"

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


def parse_contracts(contracts: pathlib.Path) -> dict[str, object]:
    """Разобрать все YAML настоящим parser toolchain и вернуть результат AST-обхода."""
    try:
        result = subprocess.run(
            ["node", str(AST_SCANNER), str(contracts)],
            check=False,
            capture_output=True,
            text=True,
        )
    except OSError as error:
        return {"problems": [f"AST-парсер не запущен: {error}"]}

    if result.returncode != 0:
        detail = result.stderr.strip() or result.stdout.strip() or f"exit {result.returncode}"
        return {"problems": [f"AST-парсер завершился с ошибкой: {detail}"]}

    try:
        return json.loads(result.stdout)
    except json.JSONDecodeError as error:
        return {"problems": [f"AST-парсер вернул невалидный JSON: {error}"]}


def check(contracts: pathlib.Path) -> list[str]:
    parsed = parse_contracts(contracts)
    problems = [str(problem) for problem in parsed.get("problems", [])]
    schema = parsed.get("schema")
    if not isinstance(schema, dict):
        problems.append(
            f"{CONTRACTS}/{SHARED}: схема {SCHEMA} не найдена — публичному Int64 нечем "
            "быть, а модулям не на что ссылаться"
        )
        return problems

    if schema.get("type") != "string":
        problems.append(f"{CONTRACTS}/{SHARED}: {SCHEMA} обязана быть `type: string`")

    pattern_text = schema.get("pattern")
    if not isinstance(pattern_text, str):
        problems.append(
            f"{CONTRACTS}/{SHARED}: у {SCHEMA} нет `pattern` — без него схема описывает "
            "любую строку, а не канон Int64"
        )
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
            "ключ и значение в кавычках тоже запрещены",
            lambda box: _patch(
                box / "reports" / "openapi.yaml",
                "        total:\n          $ref: '../shared.yaml#/components/schemas/Int64String'",
                '        total: { "type": "integer", "format": "int64" }',
                once=True,
            ),
            True,
        ),
        (
            "значение на следующей строке тоже запрещено",
            lambda box: _patch(
                box / "reports" / "openapi.yaml",
                "        total:\n          $ref: '../shared.yaml#/components/schemas/Int64String'",
                "        total:\n          type: integer\n          format:\n            int64",
                once=True,
            ),
            True,
        ),
        (
            "блочный скаляр в значении тоже запрещён",
            lambda box: _patch(
                box / "reports" / "openapi.yaml",
                "        total:\n          $ref: '../shared.yaml#/components/schemas/Int64String'",
                "        total:\n          type: integer\n          format: |-\n            int64",
                once=True,
            ),
            True,
        ),
        (
            "экранированное значение тоже запрещено",
            lambda box: _patch(
                box / "reports" / "openapi.yaml",
                "        total:\n          $ref: '../shared.yaml#/components/schemas/Int64String'",
                '        total: { type: integer, format: "int\\x36\\x34" }',
                once=True,
            ),
            True,
        ),
        (
            "экранированный перенос в двойных кавычках тоже запрещён",
            lambda box: _patch(
                box / "reports" / "openapi.yaml",
                "        total:\n          $ref: '../shared.yaml#/components/schemas/Int64String'",
                '        total:\n          type: integer\n          format: "int\\\n            64"',
                once=True,
            ),
            True,
        ),
        (
            "алиас на якорь с тем же значением тоже запрещён",
            lambda box: _patch(
                box / "reports" / "openapi.yaml",
                "        total:\n          $ref: '../shared.yaml#/components/schemas/Int64String'",
                "        total:\n          x-format: &fmt int64\n          format: *fmt",
                once=True,
            ),
            True,
        ),
        (
            "алиас на другое значение объявлением не становится",
            lambda box: _patch(
                box / "reports" / "openapi.yaml",
                "        total:\n          $ref: '../shared.yaml#/components/schemas/Int64String'",
                "        total:\n          x-format: &fmt date-time\n          format: *fmt",
                once=True,
            ),
            False,
        ),
        (
            "алиас на ключ `format` тоже запрещён",
            lambda box: _patch(
                box / "reports" / "openapi.yaml",
                "        total:\n          $ref: '../shared.yaml#/components/schemas/Int64String'",
                "        total:\n          x-seed:\n            &fmt format: date-time\n"
                "          ? *fmt\n          : int64",
                once=True,
            ),
            True,
        ),
        (
            "алиас на другой ключ объявлением не становится",
            lambda box: _patch(
                box / "reports" / "openapi.yaml",
                "        total:\n          $ref: '../shared.yaml#/components/schemas/Int64String'",
                "        total:\n          x-seed:\n            &kind type: string\n"
                "          ? *kind\n          : int64",
                once=True,
            ),
            False,
        ),
        (
            "неразрешённый алиас в ключе — красный, а не пропуск",
            lambda box: _patch(
                box / "reports" / "openapi.yaml",
                "        total:\n          $ref: '../shared.yaml#/components/schemas/Int64String'",
                "        total:\n          ? *fmt\n          : int64",
                once=True,
            ),
            True,
        ),
        (
            "тег на ключе — красный, а не пропуск",
            lambda box: _patch(
                box / "reports" / "openapi.yaml",
                "        total:\n          $ref: '../shared.yaml#/components/schemas/Int64String'",
                "        total:\n          !!str format: int64",
                once=True,
            ),
            True,
        ),
        (
            "неразрешённый алиас в значении `format` — красный, а не пропуск",
            lambda box: _patch(
                box / "reports" / "openapi.yaml",
                "        total:\n          $ref: '../shared.yaml#/components/schemas/Int64String'",
                "        total:\n          format: *fmt",
                once=True,
            ),
            True,
        ),
        (
            "тег в значении `format` — красный, а не пропуск",
            lambda box: _patch(
                box / "reports" / "openapi.yaml",
                "        total:\n          $ref: '../shared.yaml#/components/schemas/Int64String'",
                "        total:\n          format: !!str int64",
                once=True,
            ),
            True,
        ),
        (
            "`format: int64` в блочном описании — объяснение, а не объявление",
            lambda box: _patch(
                box / "reports" / "openapi.yaml",
                "        total:\n          $ref:",
                "        total:\n          description: |-\n"
                "            Раньше поле объявлялось как\n"
                "            format: int64\n"
                "          $ref:",
                once=True,
            ),
            False,
        ),
        (
            "`format: int64` внутри многострочной строки в кавычках — тоже не объявление",
            lambda box: _patch(
                box / "reports" / "openapi.yaml",
                "        total:\n          $ref:",
                '        total:\n          description: "Раньше поле объявлялось как\n'
                '            format: int64"\n          $ref:',
                once=True,
            ),
            False,
        ),
        (
            "закомментированный `format: int64` ничего не объявляет",
            lambda box: _patch(
                box / "reports" / "openapi.yaml",
                "        total:\n          $ref:",
                "        total:\n          # format: int64\n          $ref:",
                once=True,
            ),
            False,
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
