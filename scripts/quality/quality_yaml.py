"""Разбор того подмножества YAML, которым написаны контракты Bugget.

Свой разборщик, а не PyYAML: гейты обязаны одинаково работать на машине разработчика и
на раннере CI, где ставится только dotnet и node. Поддерживается ровно то, что реально
встречается в `specs/contracts/**` — вложенные отображения, списки, однострочные скаляры,
блочные скаляры (`|`, `>` с индикаторами обрезки) и пустые потоковые коллекции (`{}`, `[]`).
Всё остальное (якоря, псевдонимы, теги, непустой поток) — явная ошибка, а не молчаливый
пропуск: гейт, который «как-то» разобрал контракт, хуже отсутствующего.

Модуль общий для гейтов realtime-contract.py и contract-snapshots.py.
"""

from __future__ import annotations

import re

__all__ = ["YamlError", "parse_yaml"]


class YamlError(SystemExit):
    """Контракт разобрать не удалось: синтаксис вне поддерживаемого подмножества."""


# Ключ отображения заканчивается двоеточием перед пробелом либо в конце строки. Именно
# так, а не «первым двоеточием»: в путях контракта двоеточие бывает частью ключа —
# `/v2/reports/counts:batch`.
_KEY = re.compile(r"^(?P<key>.*?):(?:\s+(?P<value>.*))?$")

_BLOCK_SCALAR = re.compile(r"^(?P<style>[|>])(?P<chomp>[-+]?)$")


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


def _unquote(raw: str) -> str:
    raw = raw.strip()
    if len(raw) >= 2 and raw[0] == raw[-1] and raw[0] in "\"'":
        return raw[1:-1]
    return raw


def _scalar(raw: str, where: str):
    raw = raw.strip()
    if len(raw) >= 2 and raw[0] == raw[-1] and raw[0] in "\"'":
        return raw[1:-1]
    if raw in ("true", "false"):
        return raw == "true"
    if raw in ("null", "~"):
        return None
    if re.fullmatch(r"-?\d+", raw):
        return int(raw)
    if raw == "{}":
        return {}
    if raw == "[]":
        return []
    if raw.startswith(("&", "*", "!", "{", "[")):
        raise YamlError(f"{where}: неподдерживаемый синтаксис YAML — {raw!r}")
    return raw


def _fold(lines: list[str], style: str, chomp: str) -> str:
    """Складывает блочный скаляр. Отступ снимается по первой непустой строке."""
    indent = next((len(line) - len(line.lstrip()) for line in lines if line.strip()), 0)
    body = [line[indent:] if len(line) > indent else line.strip() for line in lines]

    text = "\n".join(body) if style == "|" else " ".join(part for part in body if part)
    if chomp == "-":
        return text.rstrip("\n")
    if chomp == "+":
        return text
    return text.rstrip("\n") + "\n"


def _tokenize(text: str, source: str) -> list[tuple[int, str, int, object]]:
    """Значимые строки: (отступ, содержимое, номер, готовое значение блочного скаляра)."""
    raw_lines = text.splitlines()
    tokens: list[tuple[int, str, int, object]] = []
    index = 0

    while index < len(raw_lines):
        raw = raw_lines[index]
        number = index + 1
        index += 1

        lead = raw[: len(raw) - len(raw.lstrip())]
        if "\t" in lead:
            raise YamlError(f"{source}:{number}: отступ табуляцией не поддерживается")

        content = _strip_comment(raw)
        if not content.strip():
            continue

        indent = len(content) - len(content.lstrip())
        content = content.strip()

        # Блочный скаляр: тело — все последующие строки правее ключа, комментарии в нём
        # текст, а не комментарии, поэтому берём их из исходных строк.
        block = None
        match = _KEY.match(content)
        style = _BLOCK_SCALAR.match((match.group("value") or "").strip()) if match else None
        if style:
            body: list[str] = []
            while index < len(raw_lines):
                candidate = raw_lines[index]
                if candidate.strip() and len(candidate) - len(candidate.lstrip()) <= indent:
                    break
                body.append(candidate)
                index += 1
            content = f"{match.group('key')}:"
            block = _fold(body, style.group("style"), style.group("chomp"))

        tokens.append((indent, content, number, block))

    return tokens


def parse_yaml(text: str, source: str):
    """Подмножество YAML: вложенные отображения, списки, скаляры, блочные скаляры."""
    tokens = _tokenize(text, source)
    if not tokens:
        return {}

    value, pos = _parse_block(tokens, 0, tokens[0][0], source)
    if pos != len(tokens):
        raise YamlError(f"{source}:{tokens[pos][2]}: неожиданный отступ")
    return value


def _parse_block(tokens, pos: int, indent: int, source: str):
    if tokens[pos][1].startswith("- "):
        return _parse_sequence(tokens, pos, indent, source)
    return _parse_mapping(tokens, pos, indent, source)


def _parse_sequence(tokens, pos: int, indent: int, source: str):
    result = []
    while pos < len(tokens) and tokens[pos][0] == indent and tokens[pos][1].startswith("- "):
        item_indent, content, number, block = tokens[pos]
        body = content[2:].strip()
        pos += 1

        nested = [(item_indent + 2, body, number, block)]
        while pos < len(tokens) and tokens[pos][0] > item_indent:
            nested.append(tokens[pos])
            pos += 1

        if len(nested) == 1 and not _KEY.match(body):
            result.append(_scalar(body, f"{source}:{number}"))
            continue

        # «- key: value» — блок, начинающийся правее дефиса: подменяем строку и отдаём
        # вместе со всем, что вложено под ней.
        value, consumed = _parse_block(nested, 0, nested[0][0], source)
        if consumed != len(nested):
            raise YamlError(f"{source}:{nested[consumed][2]}: неожиданный отступ")
        result.append(value)

    return result, pos


def _parse_mapping(tokens, pos: int, indent: int, source: str):
    result: dict = {}
    while pos < len(tokens) and tokens[pos][0] == indent:
        _, content, number, block = tokens[pos]
        if content.startswith("- "):
            break

        match = _KEY.match(content)
        if not match:
            raise YamlError(f"{source}:{number}: ожидалось «ключ: значение», получено {content!r}")

        key = _unquote(match.group("key"))
        if key in result:
            raise YamlError(f"{source}:{number}: ключ {key!r} повторяется")
        pos += 1

        if block is not None:
            result[key] = block
            continue

        rest = match.group("value") or ""
        if rest.strip():
            result[key] = _scalar(rest, f"{source}:{number}")
            continue

        if pos < len(tokens) and tokens[pos][0] > indent:
            result[key], pos = _parse_block(tokens, pos, tokens[pos][0], source)
        else:
            result[key] = None

    return result, pos
