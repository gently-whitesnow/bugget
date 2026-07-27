#!/usr/bin/env python3
"""Гейт покрытия realtime-контракта: specs/contracts/events.yaml против четырёх сторон.

HTTP-контракт после contract-first защищён гейтом backend-contracts, а SignalR не был
защищён ничем: имена событий жили строковыми литералами в Bugget/Hubs и в enum SocketEvent
на фронте, и разъехаться могли молча — ни компилятор C#, ни tsc про эту связь не знают.

Скрипт сверяет четыре стороны и краснеет на расхождении любой:

  1) контракт      specs/contracts/events.yaml;
  2) объявление    интерфейс публикации (Bugget.DA/WebSockets/IReportPageHubClient.cs);
  3) обработчик    реализация, которая шлёт в группу (Bugget/Hubs/ReportPageHubClient.cs);
  4) подписки      enum SocketEvent и customParsers на фронте.

Что именно проверяется:

  * событие описано, но никто не публикует — и наоборот, публикуется мимо контракта;
  * фронт подписан на событие, которого нет в контракте, — и наоборот, описанное
    событие никто не слушает;
  * publisher события есть и в интерфейсе, и в реализации, а метод интерфейса не
    остался без единого события;
  * число и типы аргументов совпадают с сигнатурой в интерфейсе, а число аргументов —
    ещё и с фактическим вызовом SendAsync;
  * событию с двумя и более аргументами соответствует разборщик в customParsers:
    дефолтное правило фронта берёт только первый аргумент, остальные молча теряются;
  * методы хаба (клиент → сервер) описаны в контракте, объявлены в самом хабе и
    ровно они зовутся через conn.invoke;
  * во всём бекенде нет публикации в обход объявленных publisher-файлов.

  realtime-contract.py              проверить
  realtime-contract.py --list       показать разобранный контракт и то, что найдено в коде
  realtime-contract.py --self-test  проверить, что гейт краснеет там, где обязан
"""

from __future__ import annotations

import argparse
import pathlib
import re
import sys
from dataclasses import dataclass, field

import quality_csharp as q

CONTRACT = "specs/contracts/events.yaml"

# Где ищем публикацию в обход контракта. Тестовые проекты исключены: там живут фейки
# хаб-клиента, они по определению повторяют те же имена событий.
BACKEND_SCAN = "backend"
BACKEND_SKIP = (".Tests/", ".IntegrationTests/", "/obj/", "/bin/")

FRONTEND_SCAN = "frontend/src"

# Аргументы, которые в провод не уходят: ключ группы и connectionId отправителя
# (по нему SignalR исключает автора изменения из рассылки).
NON_WIRE_PARAMS = ("groupKey", "signalRConnectionId")


#-----------------------------------------------------------------------------------------
# Разбор YAML
#
# Свой разборщик, а не PyYAML: гейт обязан одинаково работать на машине разработчика и
# на раннере CI, где ставится только dotnet и node. Поддерживается ровно то подмножество,
# которым написан events.yaml, всё остальное — явная ошибка, а не молчаливый пропуск.
#-----------------------------------------------------------------------------------------

class ContractError(SystemExit):
    pass


def _strip_comment(line: str) -> str:
    quote = None
    for i, ch in enumerate(line):
        if quote:
            if ch == quote:
                quote = None
        elif ch in "\"'":
            quote = ch
        elif ch == "#" and (i == 0 or line[i - 1] in " \t"):
            return line[:i].rstrip()
    return line.rstrip()


def _scalar(raw: str, where: str):
    raw = raw.strip()
    if len(raw) >= 2 and raw[0] == raw[-1] and raw[0] in "\"'":
        return raw[1:-1]
    if raw in ("true", "false"):
        return raw == "true"
    if re.fullmatch(r"-?\d+", raw):
        return int(raw)
    if raw.startswith(("|", ">", "&", "*", "{", "[")):
        raise ContractError(f"{where}: неподдерживаемый синтаксис YAML — {raw!r}")
    return raw


def parse_yaml(text: str, source: str):
    """Подмножество YAML: вложенные отображения, списки, однострочные скаляры."""
    lines: list[tuple[int, str, int]] = []
    for number, raw in enumerate(text.splitlines(), start=1):
        if "\t" in raw[: len(raw) - len(raw.lstrip())]:
            raise ContractError(f"{source}:{number}: отступ табуляцией не поддерживается")
        content = _strip_comment(raw)
        if not content.strip():
            continue
        lines.append((len(content) - len(content.lstrip()), content.strip(), number))

    value, pos = _parse_block(lines, 0, lines[0][0] if lines else 0, source)
    if pos != len(lines):
        raise ContractError(f"{source}:{lines[pos][2]}: неожиданный отступ")
    return value


def _parse_block(lines, pos: int, indent: int, source: str):
    if lines[pos][1].startswith("- "):
        return _parse_sequence(lines, pos, indent, source)
    return _parse_mapping(lines, pos, indent, source)


def _parse_sequence(lines, pos: int, indent: int, source: str):
    result = []
    while pos < len(lines) and lines[pos][0] == indent and lines[pos][1].startswith("- "):
        item_indent, content, number = lines[pos]
        # «- key: value» — это блок, начинающийся правее дефиса: подменяем строку и
        # отдаём вместе с тем, что вложено под ней.
        virtual = [(item_indent + 2, content[2:].strip(), number)]
        pos += 1
        while pos < len(lines) and lines[pos][0] > item_indent:
            virtual.append(lines[pos])
            pos += 1
        value, consumed = _parse_block(virtual, 0, virtual[0][0], source)
        if consumed != len(virtual):
            raise ContractError(f"{source}:{virtual[consumed][2]}: неожиданный отступ")
        result.append(value)
    return result, pos


def _parse_mapping(lines, pos: int, indent: int, source: str):
    result: dict = {}
    while pos < len(lines) and lines[pos][0] == indent:
        _, content, number = lines[pos]
        if content.startswith("- "):
            break
        if ":" not in content:
            raise ContractError(f"{source}:{number}: ожидалось «ключ: значение», получено {content!r}")
        key, _, rest = content.partition(":")
        key = key.strip()
        if key in result:
            raise ContractError(f"{source}:{number}: ключ {key!r} повторяется")
        pos += 1
        if rest.strip():
            result[key] = _scalar(rest, f"{source}:{number}")
            continue
        if pos < len(lines) and lines[pos][0] > indent:
            result[key], pos = _parse_block(lines, pos, lines[pos][0], source)
        else:
            result[key] = None
    return result, pos


#-----------------------------------------------------------------------------------------
# Разбор кода
#-----------------------------------------------------------------------------------------

@dataclass
class Published:
    """Событие, найденное в реализации-обработчике."""
    method: str
    arity: int


@dataclass
class CodeFacts:
    interface_methods: dict[str, list[tuple[str, str]]] = field(default_factory=dict)
    published: dict[str, list[Published]] = field(default_factory=dict)
    hub_methods: set[str] = field(default_factory=set)
    subscriptions: set[str] = field(default_factory=set)
    custom_parsers: set[str] = field(default_factory=set)
    invocations: set[str] = field(default_factory=set)


CSHARP_METHOD = re.compile(r"^\s*(?:public|internal)\s+(?:(?:async|override|sealed)\s+)*Task\s+(\w+)\s*\(", re.MULTILINE)
INTERFACE_METHOD = re.compile(r"^\s*Task\s+(\w+)\s*\(([^)]*)\)\s*;", re.MULTILINE)
OVERRIDE_METHOD = re.compile(r"\boverride\b")
SWITCH_ARM_LITERAL = re.compile(r"=>\s*\"(\w+)\"")
ENUM_MEMBER = re.compile(r"^\s*(\w+)\s*=\s*\"(\w+)\"\s*,?\s*$", re.MULTILINE)
CUSTOM_PARSER_KEY = re.compile(r"^\s*\[(\w+)\.(\w+)\]\s*:", re.MULTILINE)
INVOKE_LITERAL = re.compile(r"\.invoke\(\s*\"(\w+)\"")
STRAY_PUBLISH = re.compile(r"\.SendAsync\(\s*\"(\w+)\"")


def split_args(text: str) -> list[str]:
    """Аргументы верхнего уровня: вложенные скобки и строки не считаем разделителями."""
    args, depth, quote, current = [], 0, None, []
    for ch in text:
        if quote:
            current.append(ch)
            if ch == quote:
                quote = None
            continue
        if ch in "\"'":
            quote = ch
        elif ch in "([{":
            depth += 1
        elif ch in ")]}":
            depth -= 1
        elif ch == "," and depth == 0:
            args.append("".join(current).strip())
            current = []
            continue
        current.append(ch)
    tail = "".join(current).strip()
    if tail:
        args.append(tail)
    return args


def read_call_args(text: str, open_paren: int) -> tuple[list[str], int]:
    depth = 0
    for i in range(open_paren, len(text)):
        if text[i] == "(":
            depth += 1
        elif text[i] == ")":
            depth -= 1
            if depth == 0:
                return split_args(text[open_paren + 1 : i]), i
    raise ContractError("не закрыта скобка вызова — файл разобрать не удалось")


def method_bodies(text: str) -> dict[str, str]:
    """Тела методов C# по имени: от сигнатуры до парной закрывающей фигурной скобки."""
    bodies: dict[str, str] = {}
    for match in CSHARP_METHOD.finditer(text):
        start = text.find("{", match.end())
        if start < 0:
            continue
        depth = 0
        for i in range(start, len(text)):
            if text[i] == "{":
                depth += 1
            elif text[i] == "}":
                depth -= 1
                if depth == 0:
                    bodies[match.group(1)] = text[start : i + 1]
                    break
    return bodies


def parse_interface(text: str) -> dict[str, list[tuple[str, str]]]:
    """Методы интерфейса публикации: имя → аргументы провода [(тип, имя)]."""
    methods: dict[str, list[tuple[str, str]]] = {}
    for match in INTERFACE_METHOD.finditer(text):
        wire: list[tuple[str, str]] = []
        for param in split_args(match.group(2)):
            parts = param.split()
            if len(parts) < 2:
                continue
            type_name, name = " ".join(parts[:-1]), parts[-1]
            if name in NON_WIRE_PARAMS:
                continue
            wire.append((type_name.strip("?"), name))
        methods[match.group(1)] = wire
    return methods


def parse_publisher(text: str) -> dict[str, list[Published]]:
    """События, реально уходящие в группу: имя события → где и с каким числом аргументов."""
    published: dict[str, list[Published]] = {}
    for method, body in method_bodies(text).items():
        # Имена, которые метод выбирает switch-выражением (семейство вложений):
        # в SendAsync они приходят переменной, литерал стоит в ветке.
        indirect = [m.group(1) for m in SWITCH_ARM_LITERAL.finditer(body)]
        for position in (m.start() for m in re.finditer(r"\.SendAsync\(", body)):
            args, _ = read_call_args(body, body.index("(", position))
            if not args:
                continue
            head = args[0]
            arity = len(args) - 1
            names = [head[1:-1]] if head.startswith("\"") else indirect
            if not names:
                raise ContractError(
                    f"{method}: имя события в SendAsync задано выражением {head!r}, "
                    "которое гейт разобрать не может — оставь строковый литерал или switch по литералам"
                )
            for name in names:
                published.setdefault(name, []).append(Published(method, arity))
    return published


def parse_hub(text: str) -> set[str]:
    """Публичные методы хаба, которые зовёт клиент. Переопределения Hub — не контракт."""
    names = set()
    for match in CSHARP_METHOD.finditer(text):
        if not OVERRIDE_METHOD.search(match.group(0)):
            names.add(match.group(1))
    return names


def parse_subscriber(text: str, enum_name: str) -> tuple[set[str], set[str]]:
    """Подписки фронта: значения enum событий и ключи customParsers (в значениях enum)."""
    start = text.find(f"enum {enum_name} {{")
    if start < 0:
        raise ContractError(f"на фронте не найден enum {enum_name} — подписки разобрать нечем")
    end = text.index("}", start)
    block = text[start:end]

    by_key = {key: value for key, value in ENUM_MEMBER.findall(block)}

    # Ключи ищем только в customParsers: рядом лежит карта типов SocketPayload с теми же
    # ключами, и по ней «разборщик есть» читалось бы для каждого события.
    parsers_at = text.find("customParsers")
    if parsers_at < 0:
        raise ContractError("на фронте не найден customParsers — разборщики аргументов разобрать нечем")
    parsers = {
        by_key[key]
        for holder, key in CUSTOM_PARSER_KEY.findall(text[parsers_at:])
        if holder == enum_name and key in by_key
    }
    return set(by_key.values()), parsers


#-----------------------------------------------------------------------------------------
# Сбор фактов и проверка
#-----------------------------------------------------------------------------------------

def read_file(path: str) -> str:
    file = q.ROOT / path
    if not file.exists():
        raise ContractError(f"файл из контракта не найден: {path}")
    return file.read_text(encoding="utf-8")


def scan_files(root: str, suffixes: tuple[str, ...], skip: tuple[str, ...] = ()) -> list[str]:
    found = []
    for file in sorted((q.ROOT / root).rglob("*")):
        if not file.is_file() or file.suffix not in suffixes:
            continue
        path = q.rel(file)
        if any(part in f"/{path}" for part in skip):
            continue
        found.append(path)
    return found


def collect(hub: dict, read) -> CodeFacts:
    facts = CodeFacts()
    facts.interface_methods = parse_interface(read(hub["publisherInterface"]))
    facts.published = parse_publisher(read(hub["publisher"]))
    facts.hub_methods = parse_hub(read(hub["server"]))
    facts.subscriptions, facts.custom_parsers = parse_subscriber(read(hub["subscriber"]), hub["subscriberEnum"])
    return facts


def check_hub(hub: dict, read, invocations: set[str]) -> list[str]:
    problems: list[str] = []
    facts = collect(hub, read)
    facts.invocations = invocations
    events = {event["name"]: event for event in hub.get("events") or []}
    methods = {method["name"] for method in hub.get("methods") or []}

    def report(kind: str, names, hint: str) -> None:
        for name in sorted(names):
            problems.append(f"{kind}: {name} — {hint}")

    # 1. Контракт против обработчика.
    report("не публикуется", set(events) - set(facts.published),
           f"событие описано в {CONTRACT}, но ни один SendAsync его не шлёт")
    report("мимо контракта", set(facts.published) - set(events),
           f"событие уходит в группу из {hub['publisher']}, но не описано в {CONTRACT}")

    # 2. Контракт против подписок фронта.
    report("никто не слушает", set(events) - facts.subscriptions,
           f"событие описано, но {hub['subscriber']} на него не подписан")
    report("подписка в пустоту", facts.subscriptions - set(events),
           f"фронт подписан на событие, которого нет в {CONTRACT}")

    # 3. Контракт против объявления в интерфейсе.
    declared_publishers = {event.get("publisher") for event in events.values()}
    report("нет объявления", declared_publishers - set(facts.interface_methods),
           f"publisher из контракта не объявлен в {hub['publisherInterface']}")
    report("объявление без события", set(facts.interface_methods) - declared_publishers,
           f"метод публикации объявлен, но ни одно событие {CONTRACT} на него не ссылается")

    # 4. Аргументы: контракт против сигнатуры и против фактического вызова.
    for name, event in sorted(events.items()):
        args = event.get("args") or []
        signature = facts.interface_methods.get(event.get("publisher"))
        if signature is not None:
            expected = [(arg["type"], arg["name"]) for arg in args]
            if expected != signature:
                problems.append(
                    f"аргументы разошлись: {name} — в контракте {_show(expected)}, "
                    f"в {event['publisher']} {_show(signature)}"
                )
        for call in facts.published.get(name, []):
            if call.arity != len(args):
                problems.append(
                    f"аргументы разошлись: {name} — в контракте {len(args)}, "
                    f"а {call.method} шлёт {call.arity}"
                )
            if call.method != event.get("publisher"):
                problems.append(
                    f"publisher разошёлся: {name} — в контракте {event.get('publisher')}, "
                    f"шлёт {call.method}"
                )
        # Дефолтное правило фронта берёт первый аргумент; всё, что дальше, без
        # разборщика теряется молча.
        needs_parser = len(args) > 1
        has_parser = name in facts.custom_parsers
        if needs_parser and not has_parser:
            problems.append(
                f"нет разборщика: {name} — аргументов {len(args)}, "
                f"а в customParsers ({hub['subscriber']}) записи нет: фронт возьмёт только первый"
            )
        if has_parser and not needs_parser:
            problems.append(
                f"лишний разборщик: {name} — аргумент один, разборщик в customParsers не нужен"
            )

    # 5. Методы хаба: контракт, сам хаб и conn.invoke на фронте.
    report("нет в хабе", methods - facts.hub_methods,
           f"метод описан в {CONTRACT}, но в {hub['server']} его нет")
    report("метод мимо контракта", facts.hub_methods - methods,
           f"публичный метод хаба не описан в {CONTRACT}")
    report("не вызывается", methods - facts.invocations,
           f"метод описан, но ни один conn.invoke на фронте его не зовёт")
    report("вызов в пустоту", facts.invocations - methods,
           f"фронт зовёт метод, которого нет в {CONTRACT}")

    return problems


def _show(args: list[tuple[str, str]]) -> str:
    return "(" + ", ".join(f"{type_name} {name}" for type_name, name in args) + ")"


def check(read) -> list[str]:
    contract = parse_yaml(read(CONTRACT), CONTRACT)
    hubs = contract.get("hubs") or []
    if not hubs:
        raise ContractError(f"{CONTRACT}: не описан ни один хаб")

    # Вызовы клиента ищем по всему фронту, а не только в файле подписок: conn.invoke
    # живёт в модели сокета, а завтра может появиться где угодно.
    invocations: set[str] = set()
    for path in scan_files(FRONTEND_SCAN, (".ts", ".tsx")):
        invocations |= set(INVOKE_LITERAL.findall(read(path)))

    problems: list[str] = []
    for hub in hubs:
        problems.extend(check_hub(hub, read, invocations))

    # Публикация мимо объявленных обработчиков: новый хаб-клиент в другом файле контракт
    # обойдёт молча, поэтому ищем литеральные SendAsync по всему бекенду.
    known = {hub["publisher"] for hub in hubs}
    for path in scan_files(BACKEND_SCAN, (".cs",), BACKEND_SKIP):
        if path in known:
            continue
        for name in sorted(set(STRAY_PUBLISH.findall(read(path)))):
            problems.append(
                f"публикация вне контракта: {name} — уходит из {path}, "
                f"а обработчиками контракта объявлены только {', '.join(sorted(known))}"
            )

    return problems


#-----------------------------------------------------------------------------------------
# Самопроверка: гейт, который не краснеет там, где обязан, хуже отсутствующего
#-----------------------------------------------------------------------------------------

def self_test() -> int:
    contract = parse_yaml(read_file(CONTRACT), CONTRACT)
    hub = contract["hubs"][0]

    def mutation(path: str, before: str, after: str):
        def read(target: str) -> str:
            text = read_file(target)
            if target != path:
                return text
            if before not in text:
                raise ContractError(f"самопроверка: в {path} не найдено {before!r}")
            return text.replace(before, after, 1)
        return read

    scenarios = [
        ("согласованное состояние", read_file, False),
        (
            "событие удалено из контракта",
            mutation(CONTRACT, "      - name: ReceiveBugStepDelete\n", "      - name: ReceiveBugStepDeleteX\n"),
            True,
        ),
        (
            "бекенд шлёт событие мимо контракта",
            mutation(hub["publisher"], '.SendAsync("ReceiveBugCreate"', '.SendAsync("ReceiveBugCreated"'),
            True,
        ),
        (
            "фронт подписан на несуществующее событие",
            mutation(hub["subscriber"], "  BugCreate = \"ReceiveBugCreate\",", "  BugCreate = \"ReceiveBugCreated\","),
            True,
        ),
        (
            "фронт потерял подписку",
            mutation(hub["subscriber"], "  CommentUpdate = \"ReceiveCommentUpdate\",\n", ""),
            True,
        ),
        (
            "у события изменилось число аргументов",
            mutation(CONTRACT, "          - name: commentId\n            type: int\n", ""),
            True,
        ),
        (
            "у события изменился тип аргумента",
            mutation(CONTRACT, "          - name: linkId\n            type: int\n", "          - name: linkId\n            type: long\n"),
            True,
        ),
        (
            "метод хаба выпал из контракта",
            mutation(CONTRACT, "      - name: LeaveReportGroupAsync\n", "      - name: LeaveReportGroupAsyncX\n"),
            True,
        ),
        (
            "многоаргументное событие осталось без разборщика",
            mutation(
                hub["subscriber"],
                "[SocketEvent.CommentDelete]: (...args: unknown[]) => {",
                "[SocketEvent.CommentDeleteX]: (...args: unknown[]) => {",
            ),
            True,
        ),
    ]

    failed = 0
    for title, read, must_be_red in scenarios:
        try:
            problems = check(read)
        except SystemExit as exc:
            problems = [str(exc)]
        is_red = bool(problems)
        if is_red == must_be_red:
            print(f"  ok   {title}: {'красный' if is_red else 'зелёный'}")
        else:
            failed += 1
            print(f"  ФЕЙЛ {title}: ожидался {'красный' if must_be_red else 'зелёный'}, вышло наоборот")
            for problem in problems[:5]:
                print(f"         {problem}")

    print()
    if failed:
        print(f"Самопроверка не прошла: сценариев с неверным результатом — {failed}.")
        return 1
    print(f"Самопроверка прошла: {len(scenarios)} сценариев, гейт краснеет ровно там, где обязан.")
    return 0


#-----------------------------------------------------------------------------------------

def show_list() -> int:
    contract = parse_yaml(read_file(CONTRACT), CONTRACT)
    for hub in contract["hubs"]:
        facts = collect(hub, read_file)
        print(f"{hub['name']}  {hub['path']}")
        print(f"  методы (клиент → сервер): {', '.join(sorted(m['name'] for m in hub['methods']))}")
        print(f"  события (сервер → клиент): {len(hub['events'])} в контракте, "
              f"{len(facts.published)} публикуется, {len(facts.subscriptions)} слушает фронт")
        print()
        for event in hub["events"]:
            args = event.get("args") or []
            print(f"  {event['name']}")
            print(f"      publisher: {event['publisher']}  args: {_show([(a['type'], a['name']) for a in args])}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=f"Гейт покрытия realtime-контракта ({CONTRACT}).")
    parser.add_argument("--list", action="store_true", help="показать разобранный контракт")
    parser.add_argument("--self-test", action="store_true", help="проверить, что гейт краснеет там, где обязан")
    args = parser.parse_args()

    if args.list:
        return show_list()
    if args.self_test:
        print("Самопроверка гейта realtime-contract\n")
        return self_test()

    problems = check(read_file)
    if not problems:
        print(f"Realtime-контракт сходится: {CONTRACT} совпадает с объявлением, обработчиком и подписками фронта.")
        return 0

    print(f"Расхождение realtime-контракта ({len(problems)}):")
    for problem in problems:
        print(f"  ✘ {problem}")
    print()
    print(f"Событие добавляют сначала в {CONTRACT}, потом в код. Форму сообщений менять нельзя:")
    print("фронт стоит в проде у заказчика (ADR-0007).")
    return 1


if __name__ == "__main__":
    sys.exit(main())
