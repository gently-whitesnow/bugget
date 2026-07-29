#!/usr/bin/env python3
"""Гейт backend-contract-snapshots: снимок ответа обязан соответствовать схеме контракта.

Что сверяется: снимки публичного контракта
(`backend/Bugget.IntegrationTests/Contract/Snapshots/*.txt`) — то, что приложение
действительно отдаёт на провод, — со схемами ответов из `specs/contracts/**/openapi.yaml`
и `shared.yaml` — тем, что контракт про это обещает.

Зачем: до этого гейта инвариант «источник правды описывает то, что уходит на провод»
держался только на внимательности ревьюера. `openapi-check.sh` валидирует генерацию из
спецификации, а не форму ответа; снимок фиксирует форму ответа, но со схемой её никто не
сверял. В эту щель дважды уезжали дефекты одного класса (MAIN-67): поля, которых в
контракте нет, спокойно доезжали до фронта.

Правила:
  * каждое поле снимка описано в схеме соответствующего ответа;
  * каждое `required`-поле схемы присутствует в снимке;
  * тип поля в снимке допускается схемой (`nullable` учитывается);
  * статус и media type ответа объявлены в контракте.

Операция находится по строке `request:` снимка — методу и шаблону маршрута, которые
снимок пишет из таблицы маршрутов живого хоста. Угадывания по имени файла здесь нет.

Пути, которых в OpenAPI нет по существу, перечислены в EXCEPTIONS поимённо. Список
только сокращается: как только контракт начинает описывать путь из исключений, гейт
краснеет — иначе исключение переживёт свою причину.

Использование:
  python3 scripts/quality/contract-snapshots.py
  python3 scripts/quality/contract-snapshots.py --list        # что с чем сопоставилось
  python3 scripts/quality/contract-snapshots.py --self-test   # гейт краснеет там, где обязан
"""

from __future__ import annotations

import argparse
import pathlib
import sys

from quality_yaml import YamlError, parse_yaml

SNAPSHOTS = "backend/Bugget.IntegrationTests/Contract/Snapshots"
SHARED = "specs/contracts/shared.yaml"
MODULES = "specs/contracts"

ContractError = YamlError

# Снимки, которым в OpenAPI соответствовать нечему. Каждая строка — «метод путь: причина».
# Список только сокращается: описанный в контракте путь обязан исчезнуть отсюда.
EXCEPTIONS = {
    "POST /v1/report-page-hub/negotiate":
        "handshake SignalR, а не операция REST: форму задаёт протокол, "
        "сами сообщения описаны в specs/contracts/events.yaml",
}

# Тип схемы OpenAPI → типы формы снимка (JsonShape), которые он допускает.
# JsonShape не различает integer и number: и то и другое в JSON — число.
OBSERVED_TYPES = {
    "object": {"object"},
    "array": {"array"},
    "string": {"string"},
    "integer": {"number"},
    "number": {"number"},
    "boolean": {"bool"},
}

METHODS = ("get", "post", "put", "patch", "delete", "head", "options")


#-----------------------------------------------------------------------------------------
# Чтение репозитория
#-----------------------------------------------------------------------------------------

def read_file(path: str) -> str:
    file = pathlib.Path(path)
    if not file.is_file():
        raise ContractError(f"файл не найден: {path}")
    return file.read_text(encoding="utf-8")


def list_snapshots() -> list[str]:
    directory = pathlib.Path(SNAPSHOTS)
    if not directory.is_dir():
        raise ContractError(f"каталог снимков не найден: {SNAPSHOTS}")
    return sorted(f"{SNAPSHOTS}/{file.name}" for file in directory.glob("*.txt"))


def list_specs() -> list[str]:
    return sorted(str(path) for path in pathlib.Path(MODULES).glob("*/openapi.yaml"))


#-----------------------------------------------------------------------------------------
# Контракт
#-----------------------------------------------------------------------------------------

class Contract:
    """Схемы контракта с разрешением ссылок между документами."""

    def __init__(self, read, specs: list[str]):
        self.documents = {path: parse_yaml(read(path), path) for path in specs}
        self.documents[SHARED] = parse_yaml(read(SHARED), SHARED)

        self.operations: dict[str, tuple[str, dict]] = {}
        for path in specs:
            for route, methods in (self.documents[path].get("paths") or {}).items():
                for method, operation in (methods or {}).items():
                    if method not in METHODS:
                        continue
                    key = f"{method.upper()} {route}"
                    if key in self.operations:
                        raise ContractError(f"{path}: операция {key} описана дважды")
                    self.operations[key] = (path, operation)

    def deref(self, node, document: str):
        """Разворачивает `$ref` — локальный и на shared.yaml — до узла без ссылки."""
        seen = 0
        while isinstance(node, dict) and "$ref" in node:
            seen += 1
            if seen > 32:
                raise ContractError(f"{document}: цикл в $ref")
            file, _, pointer = node["$ref"].partition("#")
            if file:
                document = _normalize(f"{pathlib.PurePosixPath(document).parent}/{file}")
            if document not in self.documents:
                raise ContractError(f"$ref ведёт в неизвестный документ: {node['$ref']}")
            node = self._resolve_pointer(self.documents[document], pointer, document)
        return node, document

    def _resolve_pointer(self, root, pointer: str, document: str):
        node = root
        for token in pointer.strip("/").split("/"):
            if not isinstance(node, dict) or token not in node:
                raise ContractError(f"{document}: не найден узел контракта {pointer}")
            node = node[token]
        return node

    def merged(self, schema, document: str) -> tuple[dict, str]:
        """Схема с раскрытым `allOf`: свойства объединяются, `required` складываются."""
        schema, document = self.deref(schema, document)
        if not isinstance(schema, dict):
            raise ContractError(f"{document}: схема должна быть отображением, получено {schema!r}")
        if "oneOf" in schema or "anyOf" in schema:
            raise ContractError(
                f"{document}: oneOf/anyOf в схеме ответа гейт не разбирает — "
                "либо опишите форму одним типом, либо расширьте гейт осознанно")
        if "allOf" not in schema:
            return schema, document

        merged: dict = {"type": "object", "properties": {}, "required": []}
        for part in schema["allOf"]:
            resolved, part_document = self.merged(part, document)
            merged["properties"].update(resolved.get("properties") or {})
            merged["required"].extend(resolved.get("required") or [])
            if resolved.get("additionalProperties") is not None:
                merged["additionalProperties"] = resolved["additionalProperties"]
            document = part_document
        for key, value in schema.items():
            if key != "allOf":
                merged[key] = value
        return merged, document


def _normalize(path: str) -> str:
    """`specs/contracts/reports/../shared.yaml` → `specs/contracts/shared.yaml`."""
    parts: list[str] = []
    for part in pathlib.PurePosixPath(path).parts:
        if part == "..":
            if not parts:
                raise ContractError(f"$ref выходит за пределы репозитория: {path}")
            parts.pop()
        elif part != ".":
            parts.append(part)
    return "/".join(parts)


#-----------------------------------------------------------------------------------------
# Снимок
#-----------------------------------------------------------------------------------------

class Snapshot:
    """Разобранный снимок: запрос, статус, media type и форма тела."""

    def __init__(self, path: str, text: str):
        self.path = path
        self.name = pathlib.PurePosixPath(path).name.removesuffix(".txt")
        self.shape: dict[str, set[str]] = {}

        header: dict[str, str] = {}
        lines = text.replace("\r\n", "\n").split("\n")
        for index, line in enumerate(lines):
            if not line.strip():
                continue
            key, _, value = line.partition(":")
            if key in ("request", "status", "content-type"):
                header[key] = value.strip()
                continue
            if key == "body":
                # Значение строки — «empty» либо «non-json»; форма, если она есть, идёт ниже.
                self._parse_shape(lines[index + 1:])
                break
            raise ContractError(f"{path}: неожиданная строка снимка {line!r}")

        for key in ("request", "status", "content-type"):
            if key not in header:
                raise ContractError(
                    f"{path}: в снимке нет строки {key!r}. Снимки пересобираются командой "
                    "UPDATE_CONTRACT_SNAPSHOTS=1 dotnet test backend/Bugget.IntegrationTests")

        self.request = header["request"]
        self.status = header["status"]
        self.media_type = header["content-type"]

    def _parse_shape(self, lines: list[str]) -> None:
        for line in lines:
            if not line.strip():
                continue
            path, _, types = line.partition(": ")
            if not path.startswith("$") or not types:
                raise ContractError(f"{self.path}: неожиданная строка формы тела {line!r}")
            self.shape[path.strip()] = set(types.strip().split("|"))


def tokenize(json_path: str) -> list[str]:
    """`$.bugs[].id` → ['bugs', '[]', 'id']. `[]` — элемент массива, остальное — ключ.

    Индекс в имени ключа (`scopes[0].key` в `errors` у Problem Details) остаётся частью
    ключа: `[0]` — не элемент массива, а wire-имя поля запроса.
    """
    tokens: list[str] = []
    rest = json_path[1:]
    while rest:
        if rest.startswith("[]"):
            tokens.append("[]")
            rest = rest[2:]
            continue
        if not rest.startswith("."):
            raise ContractError(f"путь формы тела разобрать не удалось: {json_path!r}")

        rest = rest[1:]
        name: list[str] = []
        while rest and not rest.startswith((".", "[]")):
            name.append(rest[0])
            rest = rest[1:]
        if not name:
            raise ContractError(f"путь формы тела разобрать не удалось: {json_path!r}")
        tokens.append("".join(name))
    return tokens


#-----------------------------------------------------------------------------------------
# Сверка
#-----------------------------------------------------------------------------------------

def check(read=read_file, snapshots=None, specs=None) -> list[str]:
    contract = Contract(read, specs if specs is not None else list_specs())
    problems: list[str] = []

    for path in snapshots if snapshots is not None else list_snapshots():
        snapshot = Snapshot(path, read(path))
        problems.extend(f"{snapshot.name}: {problem}" for problem in _check_one(contract, snapshot))

    for request, reason in sorted(EXCEPTIONS.items()):
        if request in contract.operations:
            problems.append(
                f"исключение «{request}» устарело: контракт эту операцию описывает. "
                f"Уберите строку из EXCEPTIONS (причина была: {reason})")

    return problems


def _check_one(contract: Contract, snapshot: Snapshot) -> list[str]:
    if snapshot.request in EXCEPTIONS:
        return []

    if snapshot.request not in contract.operations:
        return [f"операция «{snapshot.request}» не описана в specs/contracts/**/openapi.yaml. "
                "Опишите её в контракте либо внесите в EXCEPTIONS с причиной"]

    document, operation = contract.operations[snapshot.request]
    responses = operation.get("responses") or {}
    if snapshot.status not in responses:
        return [f"{snapshot.request} отвечает {snapshot.status}, а контракт этот статус не "
                f"объявляет (есть: {', '.join(sorted(responses)) or 'ни одного'})"]

    response, document = contract.deref(responses[snapshot.status], document)
    content = response.get("content") or {}

    if snapshot.media_type == "-":
        return ([f"{snapshot.request} {snapshot.status}: тело пустое, а контракт объявляет "
                 f"content ({', '.join(sorted(content))})"] if content else [])

    if snapshot.media_type not in content:
        return [f"{snapshot.request} {snapshot.status}: media type {snapshot.media_type} не объявлен "
                f"в контракте (есть: {', '.join(sorted(content)) or 'ни одного'})"]

    if not snapshot.shape:
        # Тело есть, но формы у него нет: не-JSON поток (файл, превью).
        return []

    return _check_shape(contract, snapshot, content[snapshot.media_type].get("schema"), document)


def _check_shape(contract: Contract, snapshot: Snapshot, schema, document: str) -> list[str]:
    if schema is None:
        return [f"{snapshot.request} {snapshot.status}: у media type нет схемы, а снимок "
                f"описывает {len(snapshot.shape)} полей"]

    problems: list[str] = []
    resolved: dict[str, tuple[dict, str] | None] = {}

    for json_path in sorted(snapshot.shape):
        node = _walk(contract, schema, document, tokenize(json_path))
        if node is None:
            problems.append(f"поле {json_path} уходит на провод, но в схеме ответа его нет. "
                            "Либо опишите поле в контракте, либо перестаньте его отдавать")
            resolved[json_path] = None
            continue

        resolved[json_path] = node
        problems.extend(_check_types(json_path, snapshot.shape[json_path], node))

    problems.extend(_check_required(snapshot, resolved))
    return problems


def _walk(contract: Contract, schema, document: str, tokens: list[str]):
    """Находит схему поля по пути формы. None — поля в схеме нет."""
    node, node_document = contract.merged(schema, document)
    rest = list(tokens)

    while rest:
        token = rest.pop(0)
        node, node_document = contract.merged(node, node_document)
        if token == "[]":
            items = node.get("items")
            if items is None:
                return None
            node, node_document = contract.merged(items, node_document)
            continue

        properties = node.get("properties") or {}
        if token in properties:
            node, node_document = contract.merged(properties[token], node_document)
            continue

        extra = node.get("additionalProperties")
        if extra is None or extra is False:
            return None
        if extra is True:
            # Схема разрешает любые ключи и ничего про них не обещает — проверять нечего.
            return ({}, node_document)

        # Ключ здесь динамический, и точка в нём — часть имени, а не спуск на уровень
        # ниже: в `errors` у Problem Details лежат wire-пути полей запроса
        # (`scopes[0].key`). Поэтому следующие имена относятся к тому же ключу, и
        # спускаться по ним нельзя — в отличие от `[]`, который остаётся элементом массива.
        while rest and rest[0] != "[]":
            rest.pop(0)
        node, node_document = contract.merged(extra, node_document)

    return (node, node_document)


def _check_types(json_path: str, observed: set[str], node) -> list[str]:
    schema, _ = node
    declared = schema.get("type")
    if declared is None:
        return []
    if declared not in OBSERVED_TYPES:
        raise ContractError(f"неизвестный тип схемы {declared!r} у {json_path}")

    allowed = set(OBSERVED_TYPES[declared])
    if schema.get("nullable") is True:
        allowed.add("null")

    unexpected = {kind for kind in observed if kind not in allowed and kind != "undefined"}
    if "null" in unexpected and schema.get("nullable") is not True:
        return [f"поле {json_path} уходит на провод как null, а в схеме не nullable"]
    if unexpected:
        return [f"поле {json_path} уходит на провод как {'|'.join(sorted(observed))}, "
                f"а схема объявляет {declared}"]
    return []


def _check_required(snapshot: Snapshot, resolved) -> list[str]:
    problems: list[str] = []
    for json_path, node in sorted(resolved.items()):
        if node is None:
            continue
        schema, _ = node
        for field in schema.get("required") or []:
            if f"{json_path}.{field}" not in snapshot.shape:
                problems.append(
                    f"схема требует {json_path}.{field}, но в снимке этого поля нет. "
                    "Либо поле не уходит на провод, либо сценарий снимка его не показывает")
    return problems


#-----------------------------------------------------------------------------------------
# Режимы
#-----------------------------------------------------------------------------------------

def show_list() -> int:
    contract = Contract(read_file, list_specs())
    for path in list_snapshots():
        snapshot = Snapshot(path, read_file(path))
        target = "исключение" if snapshot.request in EXCEPTIONS else (
            contract.operations[snapshot.request][0]
            if snapshot.request in contract.operations else "НЕ НАЙДЕНО")
        print(f"{snapshot.name:<48} {snapshot.request} → {snapshot.status} {target}")
    return 0


def self_test() -> int:
    """Проверяет, что гейт краснеет там, где обязан: подменяет чтение файлов на лету."""
    snapshots = list_snapshots()
    specs = list_specs()
    reports = "specs/contracts/reports/openapi.yaml"
    report_snapshot = f"{SNAPSHOTS}/v2.reports.post.txt"
    invalid_snapshot = f"{SNAPSHOTS}/v2.reports.post.invalid.txt"

    def mutation(target: str, before: str, after: str):
        def read(path: str) -> str:
            text = read_file(path)
            if path != target:
                return text
            if before not in text:
                raise ContractError(f"самопроверка: в {path} не найдено {before!r}")
            return text.replace(before, after, 1)
        return read

    def extra(name: str, text: str):
        virtual = f"{SNAPSHOTS}/{name}.txt"

        def read(path: str) -> str:
            return text if path == virtual else read_file(path)
        return read, snapshots + [virtual]

    unknown_read, unknown_snapshots = extra(
        "self-test.unknown-path",
        "request: GET /v2/self-test/unknown\nstatus: 200\ncontent-type: application/json\nbody:\n$: object\n")

    scenarios: list[tuple[str, object, list[str], bool]] = [
        ("согласованное состояние", read_file, snapshots, False),
        (
            "в снимке поле, которого нет в схеме",
            mutation(report_snapshot, "$.status: number", "$.status: number\n$.limit: number"),
            snapshots,
            True,
        ),
        (
            "обязательное поле схемы пропало из снимка",
            mutation(report_snapshot, "$.title: string\n", ""),
            snapshots,
            True,
        ),
        (
            "тип поля разошёлся со схемой",
            mutation(report_snapshot, "$.title: string", "$.title: number"),
            snapshots,
            True,
        ),
        (
            "поле уходит null, а в схеме не nullable",
            mutation(report_snapshot, "$.title: string", "$.title: null"),
            snapshots,
            True,
        ),
        (
            "поле пропало из схемы, но осталось на проводе",
            mutation(reports, "        title:\n          type: string\n          description: Заголовок репорта.\n", ""),
            snapshots,
            True,
        ),
        (
            "статус ответа не объявлен в контракте",
            mutation(report_snapshot, "status: 200", "status: 201"),
            snapshots,
            True,
        ),
        (
            "media type мимо контракта",
            mutation(report_snapshot, "content-type: application/json", "content-type: application/xml"),
            snapshots,
            True,
        ),
        (
            "снимок без строки request",
            mutation(report_snapshot, "request: POST /v2/reports\n", ""),
            snapshots,
            True,
        ),
        (
            "вложенное поле в errors описано дополнительными свойствами",
            mutation(invalid_snapshot, "$.errors.title[]: string", "$.errors.title[]: number"),
            snapshots,
            True,
        ),
        ("снимок пути, которого нет в контракте", unknown_read, unknown_snapshots, True),
        (
            "исключение переживает свою причину",
            mutation(
                reports,
                "  /v2/reports:\n",
                "  /v1/report-page-hub/negotiate:\n"
                "    post:\n"
                "      operationId: Reports_SelfTestNegotiate\n"
                "      responses:\n"
                "        '200':\n"
                "          description: Самопроверка.\n"
                "  /v2/reports:\n"),
            snapshots,
            True,
        ),
    ]

    failed = 0
    for title, read, used, must_be_red in scenarios:
        try:
            problems = check(read, used, specs)
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


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Гейт соответствия снимков контрактных тестов схемам specs/contracts/**.")
    parser.add_argument("--list", action="store_true", help="показать, что с чем сопоставилось")
    parser.add_argument("--self-test", action="store_true", help="проверить, что гейт краснеет там, где обязан")
    args = parser.parse_args()

    if args.list:
        return show_list()
    if args.self_test:
        print("Самопроверка гейта backend-contract-snapshots\n")
        return self_test()

    problems = check()
    if not problems:
        print(f"Снимки контракта совпадают со схемами: {len(list_snapshots())} снимков, "
              f"{len(EXCEPTIONS)} исключение(й).")
        return 0

    print(f"Снимки контракта разошлись со схемами ({len(problems)}):")
    for problem in problems:
        print(f"  ✘ {problem}")
    print()
    print("Источник правды — specs/contracts/**/openapi.yaml: на провод не уходит то, чего")
    print("контракт не описывает. Снимки пересобираются командой")
    print("UPDATE_CONTRACT_SNAPSHOTS=1 dotnet test backend/Bugget.IntegrationTests.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
