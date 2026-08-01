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

Что разобрать не удалось, значением не считается и краснеет отдельно: неразрешённый
алиас, тег на значении `format`, узел с явным ключом `?`. Гейт fail-closed по
построению — молчаливый пропуск неразобранного и есть механизм обхода.

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

# Индикатор блочного скаляра (`|`, `>-`, `|+2`): дальше идёт текст узла, а не узлы.
BLOCK_SCALAR = re.compile(r"^[|>](?:[+-]?\d*|\d*[+-]?)$")

# Якорь (`&fmt`) и тег (`!!str`) перед значением.
DECORATION = re.compile(r"^([&!])(\S*)(?:\s+|$)")

# Элемент блочного списка перед ключом: `- format: int64`.
SEQUENCE_ITEM = re.compile(r"^-(?:\s+|$)")

# Простой ключ отделяется от значения двоеточием с пробелом или концом строки: иначе
# `format:int64` — это один скаляр, а не отображение.
PLAIN_SEPARATOR = re.compile(r":(?=\s|$)")

# Явный ключ (`? format`) в контрактах не встречается, и разбирать его гейт не умеет.
# Молча пропускать неразобранное нельзя: ровно так гейт и обходится.
EXPLICIT_KEY = re.compile(r"^\s*(?:-\s+)*\?(\s|$)")

# Экранирование внутри двойных кавычек: `"int\x36\x34"` — это тот же int64.
ESCAPES = {
    "0": "\0", "a": "\a", "b": "\b", "t": "\t", "\t": "\t", "n": "\n", "v": "\v",
    "f": "\f", "r": "\r", "e": "\x1b", " ": " ", '"': '"', "/": "/", "\\": "\\",
    "N": "\x85", "_": "\xa0", "L": " ", "P": " ",
}
NUMERIC_ESCAPES = {"x": 2, "u": 4, "U": 8}

# Схема shared.yaml: заголовок по отступу, тело — до следующего заголовка того же уровня.
SCHEMA_HEADER = re.compile(rf"^(?P<indent>\s+){SCHEMA}:\s*$")

MAX_INT64 = "9223372036854775807"

FOUND = (
    f"`format: int64` в публичном контракте — "
    f"замените схему на $ref '../{SHARED}#/components/schemas/{SCHEMA}'"
)

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


def entries(text: str) -> list[tuple[int, int, str, str | None]]:
    """Документ → плоский список узлов `(строка, отступ, текст, тело блочного скаляра)`.

    Свой разбор, а не PyYAML: гейт обязан одинаково работать на машине разработчика и
    на раннере CI, где ставится только dotnet и node (та же причина, что у
    realtime-contract.py). Разбирается не «строка похожа на объявление», а документ:

      * flow-стиль разворачивается в те же записи, что и блочный: `{ type: integer,
        format: int64 }` даёт две записи, а не одну строку без совпадения;
      * тело блочного скаляра (`|`, `>`) — это текст своего узла, а не узлы документа:
        оно возвращается значением этого узла и внутри не разбирается. Поэтому
        `format: |-` со значением ниже разрешается, а `format: int64` внутри
        description по-прежнему объясняет, а не объявляет;
      * кавычка живёт между строками, поэтому многострочный скаляр не рассыпается на
        куски, в которых видно мнимое объявление;
      * `#` вне кавычек начинает комментарий: закомментированное — не объявление.
    """
    lines = text.splitlines()
    result: list[tuple[int, int, str, str | None]] = []

    buffer = ""
    start = 0
    indent = 0
    quote: str | None = None
    index = 0

    def flush() -> None:
        nonlocal buffer
        if buffer.strip():
            result.append((start, indent, buffer.strip(), None))
        buffer = ""

    while index < len(lines):
        line = lines[index]
        number = index + 1
        index += 1

        position = 0
        while position < len(line):
            char = line[position]
            if quote:
                buffer += char
                if char == "\\" and quote == '"' and position + 1 < len(line):
                    buffer += line[position + 1]
                    position += 2
                    continue
                if char == quote:
                    if quote == "'" and line[position + 1 : position + 2] == "'":
                        buffer += "'"
                        position += 2
                        continue
                    quote = None
                position += 1
                continue

            if char == "#" and (position == 0 or line[position - 1] in " \t"):
                break
            if not buffer.strip():
                start, indent = number, len(line) - len(line.lstrip())
            if char in "'\"":
                quote = char
                buffer += char
                position += 1
                continue
            if char in "{}[],":
                flush()
                position += 1
                continue
            buffer += char
            position += 1

        if quote:
            buffer += "\n"
            continue

        count = len(result)
        flush()
        if len(result) == count:
            continue

        node_line, node_indent, segment, _ = result[-1]
        if not is_block_scalar(segment):
            continue
        # Тело отбивается от того, к чему индикатор относится: у `format: |-` это сам
        # ключ (в элементе списка — ключ после дефиса, а не дефис), у отдельной строки
        # `|-` — ключ уровнем выше, поэтому тело вправе стоять с тем же отступом, что и
        # индикатор.
        outer = node_indent + len(segment) - len(strip_sequence(segment))
        if not split_entry(segment):
            outer -= 1
        body: list[str] = []
        while index < len(lines):
            following = lines[index]
            if following.strip() and len(following) - len(following.lstrip()) <= outer:
                break
            body.append(following)
            index += 1
        result[-1] = (node_line, node_indent, segment, "\n".join(body))

    flush()
    return result


def is_block_scalar(segment: str) -> bool:
    """Узел, значение которого записано блочным скаляром (`|`, `>-`, `format: |+`)."""
    entry = split_entry(segment)
    _, _, value = decorated(entry[1] if entry else strip_sequence(segment))
    return bool(BLOCK_SCALAR.match(value))


def strip_sequence(segment: str) -> str:
    """Снимает дефисы элементов блочного списка: `- - format: int64`."""
    while True:
        match = SEQUENCE_ITEM.match(segment)
        if not match:
            return segment
        segment = segment[match.end() :]


def split_entry(segment: str) -> tuple[str, str] | None:
    """Узел `ключ: значение` → сырые ключ и значение как записаны; иначе None."""
    text = strip_sequence(segment)
    if not text:
        return None

    if text[0] in "'\"":
        end = closing_quote(text)
        if end is None or not text[end + 1 :].lstrip().startswith(":"):
            return None
        # После ключа в кавычках двоеточие может стоять вплотную: `{"format":"int64"}`.
        return text[: end + 1], text[end + 1 :].lstrip()[1:].strip()

    separator = PLAIN_SEPARATOR.search(text)
    if not separator or not text[: separator.start()].strip():
        return None
    return text[: separator.start()].strip(), text[separator.end() :].strip()


def closing_quote(text: str) -> int | None:
    """Позиция закрывающей кавычки скаляра, начинающегося с `text[0]`."""
    quote = text[0]
    position = 1
    while position < len(text):
        char = text[position]
        if char == "\\" and quote == '"':
            position += 2
            continue
        if char == quote:
            if quote == "'" and text[position + 1 : position + 2] == "'":
                position += 2
                continue
            return position
        position += 1
    return None


def decorated(raw: str) -> tuple[str | None, bool, str]:
    """Снимает якорь и тег: `&fmt !!str int64` → (`fmt`, True, `int64`)."""
    anchor, tagged = None, False
    raw = raw.strip()
    while raw[:1] in ("&", "!"):
        match = DECORATION.match(raw)
        if not match:
            break
        if match.group(1) == "&":
            anchor = match.group(2)
        else:
            tagged = True
        raw = raw[match.end() :].strip()
    return anchor, tagged, raw


def resolve(raw: str, body: str | None, anchors: dict[str, str]) -> tuple[str | None, str]:
    """Запись значения → само значение; `(None, причина)`, если разобрать не удалось.

    Гейт сравнивает значения, а не их запись: `int64`, `'int64'`, `"int\\x36\\x34"`,
    блочный скаляр и алиас на якорь с тем же значением — одно и то же объявление.
    Что разобрать не удалось, значением не считается и краснеет отдельно: молчаливый
    пропуск неразобранного — это и есть обход гейта.
    """
    if body is not None:
        return normalize(body), ""
    if not raw:
        return "", ""
    if raw.startswith("*"):
        alias = raw[1:].strip()
        if alias in anchors:
            return anchors[alias], ""
        return None, f"алиас `*{alias}` не разрешён"
    if raw[0] in "'\"":
        end = closing_quote(raw)
        if end is None or raw[end + 1 :].strip():
            return None, "скаляр в кавычках разобрать не удалось"
        inner = raw[1:end]
        if raw[0] == "'":
            return normalize(inner.replace("''", "'")), ""
        return normalize(unescape(inner)), ""
    return normalize(raw), ""


def unescape(text: str) -> str:
    """Экранирование двойных кавычек: `int\\x36\\x34` → `int64`.

    Обратный слеш перед физическим переносом удаляет и перенос, и отступ строки
    продолжения: YAML разбирает ``"int\\\n  64"`` как ``int64``.
    """
    result = ""
    position = 0
    while position < len(text):
        char = text[position]
        if char != "\\" or position + 1 >= len(text):
            result += char
            position += 1
            continue
        code = text[position + 1]
        if code == "\n":
            position += 2
            while position < len(text) and text[position] in " \t":
                position += 1
            continue
        size = NUMERIC_ESCAPES.get(code)
        if size:
            digits = text[position + 2 : position + 2 + size]
            if len(digits) == size:
                try:
                    result += chr(int(digits, 16))
                    position += 2 + size
                    continue
                except ValueError:
                    pass
        result += ESCAPES.get(code, code)
        position += 2
    return result


def normalize(value: str) -> str:
    """Значение по существу: перенос строки и отступ продолжения — это пробел.

    Свёртка строк, обрезка блочного скаляра и отступ продолжения на смысл значения
    не влияют, поэтому `|-`, `|`, `>` и перенос внутри кавычек сводятся к одному
    виду: гейт краснеет на объявлении, а не на способе его записать.
    """
    return " ".join(value.split())


def scan(text: str) -> list[tuple[int, str]]:
    """Строки, где объявлен `format: int64`, и записи, которые разобрать не удалось."""
    found: list[tuple[int, str]] = []
    anchors: dict[str, str] = {}
    pending: tuple[int, int] | None = None

    def judge(number: int, value: str | None, reason: str) -> None:
        if value is None:
            found.append((number, f"значение `format` не разобрано: {reason}"))
        elif value == "int64":
            found.append((number, FOUND))

    for number, indent, segment, body in entries(text):
        if EXPLICIT_KEY.match(segment):
            found.append((number, "узел с явным ключом `?` — гейт его не разбирает"))
            pending = None
            continue

        entry = split_entry(segment)
        raw = entry[1] if entry else strip_sequence(segment)
        anchor, tagged, raw = decorated(raw)
        value, reason = resolve(raw, body, anchors)
        if tagged:
            value, reason = None, "тег на значении гейт не разбирает"
        if anchor and value is not None:
            anchors[anchor] = value

        if entry is None:
            # Значение ключа, перенесённое на следующую строку: `format:`, ниже с
            # большим отступом `int64` — то же объявление, другой перенос.
            if pending and indent > pending[1]:
                judge(pending[0], value, reason)
            pending = None
            continue

        pending = None
        key, _ = resolve(decorated(entry[0])[2], None, anchors)
        if key != "format":
            continue
        if not raw and body is None:
            pending = (number, indent)
            continue
        judge(number, value, reason)

    return found


def check(contracts: pathlib.Path) -> list[str]:
    problems: list[str] = []

    for spec in sorted(contracts.rglob("*.yaml")):
        relative = spec.relative_to(contracts.parent.parent)
        for number, note in scan(spec.read_text(encoding="utf-8")):
            problems.append(f"{relative}:{number}: {note}")

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
