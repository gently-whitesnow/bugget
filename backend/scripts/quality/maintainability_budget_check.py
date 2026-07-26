#!/usr/bin/env python3
"""Starter maintainability budget checker for C# repositories.

The checker intentionally uses lightweight lexical heuristics instead of a full
C# compiler. It is good enough for first-day budgets: file LOC, type LOC, method
LOC, and constructor parameter counts. Keep semantic rules in .NET analyzers.
"""

from __future__ import annotations

import argparse
import fnmatch
import json
import os
import pathlib
import re
import sys
from dataclasses import dataclass
from typing import Iterable


DEFAULT_CONFIG_NAMES = (
    "maintainability-budget.json",
    "config/maintainability-budget.json",
)

DEFAULT_EXCLUDE = (
    "**/bin/**",
    "**/obj/**",
    "**/.git/**",
    "**/Generated/**",
    "**/generated/**",
    "**/*.g.cs",
    "**/*.generated.cs",
    "**/*.designer.cs",
    "**/Migrations/**",
)

TYPE_KEYWORDS = {"class", "struct", "record", "interface", "enum"}
NESTED_TYPE_KEYWORDS = {"class", "struct", "record", "interface", "enum", "delegate"}
CONTROL_KEYWORDS = {
    "if",
    "for",
    "foreach",
    "while",
    "switch",
    "catch",
    "using",
    "lock",
    "fixed",
    "when",
}
CC_KEYWORDS = {"if", "for", "foreach", "while", "case", "catch", "when"}
CC_PUNCTS = {"&&", "||", "??"}
TWO_CHAR_PUNCTS = ("=>", "&&", "||", "??", "?.", "?[")
USING_DIRECTIVE = re.compile(
    r'^\s*(?:global\s+)?using(?:\s+(?:static|unsafe))?\s+'
    r'(?:[A-Za-z_][A-Za-z0-9_]*\s*=\s*)?'
    r'([A-Za-z_][A-Za-z0-9_.]*)\s*;',
    re.MULTILINE,
)


@dataclass(frozen=True)
class Token:
    value: str
    line: int
    kind: str


@dataclass(frozen=True)
class TypeSpan:
    name: str
    kind: str
    line: int
    open_index: int
    close_index: int
    primary_constructor_params: int | None


@dataclass(frozen=True)
class Violation:
    code: str
    path: str
    line: int
    subject: str
    actual: int
    limit: int


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Check C# maintainability budgets from maintainability-budget.json."
    )
    parser.add_argument(
        "roots",
        nargs="*",
        help="Scan roots. Defaults to config scanRoots, then src/tests, then current directory.",
    )
    parser.add_argument(
        "--config",
        default=os.environ.get("MAINTAINABILITY_BUDGET_CONFIG"),
        help="Path to maintainability-budget.json.",
    )
    parser.add_argument(
        "--profile",
        default=os.environ.get("MAINTAINABILITY_PROFILE"),
        help="Profile name, for example strict or legacy.",
    )
    parser.add_argument(
        "--mode",
        choices=("advisory", "blocking"),
        default=os.environ.get("MAINTAINABILITY_GATE_MODE"),
        help="Override profile gateMode.",
    )
    parser.add_argument(
        "--max-violations",
        type=int,
        default=int(os.environ.get("MAINTAINABILITY_MAX_VIOLATIONS", "50")),
        help="Maximum violations to print before truncating output.",
    )
    parser.add_argument(
        "--baseline-snapshot",
        default=os.environ.get("MAINTAINABILITY_BASELINE_SNAPSHOT"),
        help="Path to baseline snapshot. Any new violation vs snapshot fails the run (ratchet).",
    )
    parser.add_argument(
        "--write-baseline-snapshot",
        default=None,
        help="Write current violations to this path as new baseline snapshot, then exit 0.",
    )
    return parser.parse_args()


def load_config(path: str | None) -> tuple[dict, pathlib.Path | None]:
    if path:
        config_path = pathlib.Path(path)
        if not config_path.exists():
            raise SystemExit(f"Config не найден: {path}")
        return json.loads(config_path.read_text(encoding="utf-8")), config_path

    for candidate in DEFAULT_CONFIG_NAMES:
        config_path = pathlib.Path(candidate)
        if config_path.exists():
            return json.loads(config_path.read_text(encoding="utf-8")), config_path

    raise SystemExit(
        "Config maintainability-budget.json не найден. "
        "Передайте --config или задайте MAINTAINABILITY_BUDGET_CONFIG."
    )


def profile_from_config(config: dict, profile_name: str | None) -> tuple[str, dict]:
    profiles = config.get("profiles") or {}
    selected_name = profile_name or config.get("defaultProfile") or "legacy"
    profile = profiles.get(selected_name)
    if not isinstance(profile, dict):
        available = ", ".join(sorted(profiles)) or "<none>"
        raise SystemExit(f"Profile '{selected_name}' не найден. Доступно: {available}")
    return selected_name, profile


def resolve_roots(args_roots: list[str], config: dict) -> list[pathlib.Path]:
    if args_roots:
        candidates = args_roots
    else:
        candidates = config.get("scanRoots") or ["src", "tests"]

    roots = [pathlib.Path(root) for root in candidates if pathlib.Path(root).exists()]
    if roots:
        return roots

    return [pathlib.Path(".")]


def matches_any(path: str, patterns: Iterable[str]) -> bool:
    for pattern in patterns:
        normalized = pattern.replace("\\", "/")
        if fnmatch.fnmatch(path, normalized):
            return True
        if normalized.startswith("**/") and fnmatch.fnmatch(path, normalized[3:]):
            return True
    return False


def find_csharp_files(roots: Iterable[pathlib.Path], exclude: Iterable[str]) -> list[pathlib.Path]:
    files: list[pathlib.Path] = []
    seen: set[pathlib.Path] = set()
    for root in roots:
        for path in root.rglob("*.cs") if root.is_dir() else [root]:
            if not path.is_file() or path.suffix.lower() != ".cs":
                continue
            normalized = path.as_posix()
            if matches_any(normalized, exclude):
                continue
            resolved = path.resolve()
            if resolved in seen:
                continue
            seen.add(resolved)
            files.append(path)
    return sorted(files)


def line_has_code(line: str, state: dict[str, bool]) -> bool:
    i = 0
    has_code = False
    in_block = state.get("in_block", False)
    in_string = False
    in_char = False
    verbatim = False

    while i < len(line):
        ch = line[i]
        nxt = line[i + 1] if i + 1 < len(line) else ""

        if in_block:
            if ch == "*" and nxt == "/":
                in_block = False
                i += 2
                continue
            i += 1
            continue

        if in_string:
            has_code = True
            if verbatim and ch == '"' and nxt == '"':
                i += 2
                continue
            if ch == '"' and (verbatim or not is_escaped(line, i)):
                in_string = False
                verbatim = False
            i += 1
            continue

        if in_char:
            has_code = True
            if ch == "'" and not is_escaped(line, i):
                in_char = False
            i += 1
            continue

        if ch == "/" and nxt == "/":
            break
        if ch == "/" and nxt == "*":
            in_block = True
            i += 2
            continue
        if ch == "@" and nxt == '"':
            has_code = True
            in_string = True
            verbatim = True
            i += 2
            continue
        if ch == '"':
            has_code = True
            in_string = True
            i += 1
            continue
        if ch == "'":
            has_code = True
            in_char = True
            i += 1
            continue
        if not ch.isspace():
            has_code = True
        i += 1

    state["in_block"] = in_block
    return has_code


def count_loc(lines: list[str], start_line: int = 1, end_line: int | None = None) -> int:
    end = end_line if end_line is not None else len(lines)
    state: dict[str, bool] = {"in_block": False}
    total = 0
    for line in lines[start_line - 1 : end]:
        if line_has_code(line, state):
            total += 1
    return total


def is_escaped(text: str, index: int) -> bool:
    backslashes = 0
    i = index - 1
    while i >= 0 and text[i] == "\\":
        backslashes += 1
        i -= 1
    return backslashes % 2 == 1


def tokenize(text: str) -> list[Token]:
    tokens: list[Token] = []
    i = 0
    line = 1
    in_block = False
    in_line = False
    in_string = False
    in_char = False
    verbatim = False

    while i < len(text):
        ch = text[i]
        nxt = text[i + 1] if i + 1 < len(text) else ""

        if ch == "\n":
            line += 1
            in_line = False
            i += 1
            continue

        if in_line:
            i += 1
            continue

        if in_block:
            if ch == "*" and nxt == "/":
                in_block = False
                i += 2
                continue
            i += 1
            continue

        if in_string:
            if verbatim and ch == '"' and nxt == '"':
                i += 2
                continue
            if ch == '"' and (verbatim or not is_escaped(text, i)):
                in_string = False
                verbatim = False
            i += 1
            continue

        if in_char:
            if ch == "'" and not is_escaped(text, i):
                in_char = False
            i += 1
            continue

        if ch == "/" and nxt == "/":
            in_line = True
            i += 2
            continue
        if ch == "/" and nxt == "*":
            in_block = True
            i += 2
            continue
        if ch == "@" and nxt == '"':
            in_string = True
            verbatim = True
            i += 2
            continue
        if ch == '"':
            in_string = True
            i += 1
            continue
        if ch == "'":
            in_char = True
            i += 1
            continue

        if ch.isalpha() or ch == "_":
            start = i
            while i < len(text) and (text[i].isalnum() or text[i] == "_"):
                i += 1
            tokens.append(Token(text[start:i], line, "id"))
            continue

        if ch.isdigit():
            start = i
            while i < len(text) and (text[i].isalnum() or text[i] in "._"):
                i += 1
            tokens.append(Token(text[start:i], line, "number"))
            continue

        two = ch + nxt
        if two in TWO_CHAR_PUNCTS:
            tokens.append(Token(two, line, "punct"))
            i += 2
            continue

        if ch in "{}()[];,<>=:?":
            tokens.append(Token(ch, line, "punct"))
        i += 1

    return tokens


def build_brace_map(tokens: list[Token]) -> dict[int, int]:
    stack: list[int] = []
    mapping: dict[int, int] = {}
    for index, token in enumerate(tokens):
        if token.value == "{":
            stack.append(index)
        elif token.value == "}" and stack:
            open_index = stack.pop()
            mapping[open_index] = index
            mapping[index] = open_index
    return mapping


def find_matching_left(tokens: list[Token], right_index: int, left: str, right: str) -> int | None:
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


def find_matching_right(tokens: list[Token], left_index: int, left: str, right: str, stop: int) -> int | None:
    depth = 0
    for index in range(left_index, stop + 1):
        value = tokens[index].value
        if value == left:
            depth += 1
        elif value == right:
            depth -= 1
            if depth == 0:
                return index
    return None


def next_identifier(tokens: list[Token], start: int, stop: int) -> int | None:
    for index in range(start, stop):
        if tokens[index].kind == "id":
            return index
    return None


def count_parameters(tokens: list[Token], open_paren: int, close_paren: int) -> int:
    content = tokens[open_paren + 1 : close_paren]
    if not content:
        return 0

    useful = [token for token in content if token.value not in {",", "params"}]
    if not useful:
        return 0

    count = 1
    paren_depth = 0
    bracket_depth = 0
    angle_depth = 0
    for token in content:
        value = token.value
        if value == "(":
            paren_depth += 1
        elif value == ")":
            paren_depth = max(0, paren_depth - 1)
        elif value == "[":
            bracket_depth += 1
        elif value == "]":
            bracket_depth = max(0, bracket_depth - 1)
        elif value == "<":
            angle_depth += 1
        elif value == ">":
            angle_depth = max(0, angle_depth - 1)
        elif value == "," and paren_depth == 0 and bracket_depth == 0 and angle_depth == 0:
            count += 1
    return count


def find_type_spans(tokens: list[Token], brace_map: dict[int, int]) -> list[TypeSpan]:
    spans: list[TypeSpan] = []
    index = 0
    while index < len(tokens):
        token = tokens[index]
        if token.kind != "id" or token.value not in TYPE_KEYWORDS:
            index += 1
            continue

        kind = token.value
        name_start = index + 1
        if kind == "record" and name_start < len(tokens) and tokens[name_start].value in {"class", "struct"}:
            kind = f"record {tokens[name_start].value}"
            name_start += 1

        name_index = next_identifier(tokens, name_start, min(len(tokens), name_start + 6))
        if name_index is None:
            index += 1
            continue

        open_brace = None
        primary_params = None
        search_stop = min(len(tokens), name_index + 120)
        cursor = name_index + 1
        while cursor < search_stop:
            value = tokens[cursor].value
            if value == ";":
                break
            if value == "(" and primary_params is None:
                close_paren = find_matching_right(tokens, cursor, "(", ")", search_stop - 1)
                if close_paren is not None:
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

        close_brace = brace_map[open_brace]
        spans.append(
            TypeSpan(
                name=tokens[name_index].value,
                kind=kind,
                line=token.line,
                open_index=open_brace,
                close_index=close_brace,
                primary_constructor_params=primary_params,
            )
        )
        index += 1

    return spans


def previous_separator(tokens: list[Token], before: int, lower_bound: int) -> int:
    for index in range(before, lower_bound, -1):
        if tokens[index].value in {";", "}"}:
            return index
    return lower_bound


def has_lambda(tokens: list[Token], start: int, end: int) -> bool:
    return any(token.value == "=>" for token in tokens[start : end + 1])


def method_candidates(tokens: list[Token], brace_map: dict[int, int], type_span: TypeSpan) -> list[tuple[str, int, int, int, int]]:
    candidates: list[tuple[str, int, int, int, int]] = []
    index = type_span.open_index + 1
    while index < type_span.close_index:
        if tokens[index].value != "{" or index not in brace_map:
            index += 1
            continue

        if index == type_span.open_index:
            index += 1
            continue

        prev_index = index - 1
        if prev_index < 0 or tokens[prev_index].value != ")":
            index += 1
            continue

        open_paren = find_matching_left(tokens, prev_index, "(", ")")
        if open_paren is None or open_paren <= type_span.open_index:
            index += 1
            continue

        name_index = open_paren - 1
        if name_index <= type_span.open_index or tokens[name_index].kind != "id":
            index += 1
            continue

        name = tokens[name_index].value
        if name in CONTROL_KEYWORDS:
            index += 1
            continue

        separator = previous_separator(tokens, open_paren, type_span.open_index)
        signature_start = separator + 1
        if has_lambda(tokens, signature_start, index):
            index += 1
            continue

        close_index = brace_map[index]
        candidates.append((name, signature_start, index, close_index, count_parameters(tokens, open_paren, prev_index)))
        index = close_index + 1

    return candidates


def count_cyclomatic(tokens: list[Token], start_index: int, end_index: int) -> int:
    cc = 1
    for index in range(start_index, end_index + 1):
        token = tokens[index]
        if token.kind == "id" and token.value in CC_KEYWORDS:
            cc += 1
        elif token.kind == "punct" and token.value in CC_PUNCTS:
            cc += 1
    return cc


def count_public_members(tokens: list[Token], span: TypeSpan) -> int:
    count = 0
    depth = 0
    index = span.open_index + 1
    while index < span.close_index:
        value = tokens[index].value
        if value == "{":
            depth += 1
        elif value == "}":
            depth -= 1
        elif depth == 0 and tokens[index].kind == "id" and value == "public":
            cursor = index + 1
            while cursor < span.close_index and tokens[cursor].kind != "id":
                cursor += 1
            if cursor < span.close_index and tokens[cursor].value not in NESTED_TYPE_KEYWORDS:
                count += 1
        index += 1
    return count


def collect_fan_out(text: str, ignore_prefixes: list[str]) -> set[str]:
    seen: set[str] = set()
    for match in USING_DIRECTIVE.finditer(text):
        ns = match.group(1)
        if any(ns == prefix or ns.startswith(prefix + ".") for prefix in ignore_prefixes):
            continue
        seen.add(ns)
    return seen


def analyze_file(path: pathlib.Path, limits: dict, fan_out_ignore: list[str]) -> tuple[int, list[Violation]]:
    text = path.read_text(encoding="utf-8-sig")
    lines = text.splitlines()
    tokens = tokenize(text)
    brace_map = build_brace_map(tokens)
    type_spans = find_type_spans(tokens, brace_map)
    normalized = path.as_posix()
    violations: list[Violation] = []

    file_loc = count_loc(lines)
    file_limit = int(limits["fileMaxLoc"])
    if file_loc > file_limit:
        violations.append(Violation("FILE_LOC", normalized, 1, normalized, file_loc, file_limit))

    type_limit = int(limits["typeMaxLoc"])
    method_limit = int(limits["methodMaxLoc"])
    constructor_limit = int(limits["constructorMaxDependencies"])
    cc_limit = optional_int(limits, "methodMaxCyclomaticComplexity")
    public_limit = optional_int(limits, "typeMaxPublicMembers")
    fan_out_limit = optional_int(limits, "fileMaxFanOut")

    if fan_out_limit is not None:
        fan_out = collect_fan_out(text, fan_out_ignore)
        if len(fan_out) > fan_out_limit:
            violations.append(Violation("FILE_FANOUT", normalized, 1, normalized, len(fan_out), fan_out_limit))

    for span in type_spans:
        start_line = tokens[span.open_index].line
        end_line = tokens[span.close_index].line
        type_loc = count_loc(lines, start_line, end_line)
        if type_loc > type_limit:
            violations.append(Violation("TYPE_LOC", normalized, span.line, span.name, type_loc, type_limit))

        if public_limit is not None:
            public_count = count_public_members(tokens, span)
            if public_count > public_limit:
                violations.append(Violation("TYPE_PUB", normalized, span.line, span.name, public_count, public_limit))

        if span.primary_constructor_params is not None and span.primary_constructor_params > constructor_limit:
            subject = f"{span.name} primary constructor"
            violations.append(
                Violation(
                    "CTOR_DEPS",
                    normalized,
                    span.line,
                    subject,
                    span.primary_constructor_params,
                    constructor_limit,
                )
            )

        for name, signature_start, open_index, close_index, parameter_count in method_candidates(tokens, brace_map, span):
            start = tokens[signature_start].line if signature_start < len(tokens) else span.line
            end = tokens[close_index].line
            method_loc = count_loc(lines, start, end)
            subject = f"{span.name}.{name}"
            if method_loc > method_limit:
                violations.append(Violation("METHOD_LOC", normalized, start, subject, method_loc, method_limit))
            if name == span.name and parameter_count > constructor_limit:
                violations.append(Violation("CTOR_DEPS", normalized, start, subject, parameter_count, constructor_limit))
            if cc_limit is not None:
                cc = count_cyclomatic(tokens, open_index, close_index)
                if cc > cc_limit:
                    violations.append(Violation("METHOD_CC", normalized, start, subject, cc, cc_limit))

    return file_loc, violations


def optional_int(limits: dict, key: str) -> int | None:
    if key not in limits or limits[key] is None:
        return None
    return int(limits[key])


def print_report(
    profile_name: str,
    mode: str,
    files: list[pathlib.Path],
    violations: list[Violation],
    max_violations: int,
) -> None:
    print(f"==> Maintainability budget profile: {profile_name} ({mode})")
    print(f"Scanned C# files: {len(files)}")

    if not violations:
        print("No maintainability budget violations detected.")
        return

    print()
    print("Violations:")
    for violation in violations[:max_violations]:
        print(
            f"[{violation.code}] {violation.path}:{violation.line} "
            f"{violation.subject} = {violation.actual} > {violation.limit}"
        )

    remaining = len(violations) - max_violations
    if remaining > 0:
        print(f"... {remaining} more violation(s) not shown.")

    print()
    if mode == "advisory":
        print("Advisory mode: violations are reported, but exit code remains 0.")
    else:
        print("Blocking mode: fix violations or document repo-owned exceptions before merging.")


def main() -> int:
    args = parse_args()
    config, _config_path = load_config(args.config)
    profile_name, profile = profile_from_config(config, args.profile)

    mode = args.mode or profile.get("gateMode") or "advisory"
    if mode not in {"advisory", "blocking"}:
        raise SystemExit(f"Unsupported gate mode: {mode}")

    limits = profile.get("limits") or {}
    required_limits = {"fileMaxLoc", "typeMaxLoc", "methodMaxLoc", "constructorMaxDependencies"}
    missing = sorted(required_limits - set(limits))
    if missing:
        raise SystemExit(f"Profile '{profile_name}' missing limits: {', '.join(missing)}")

    exclude = list(DEFAULT_EXCLUDE)
    exclude.extend(config.get("generatedFilePatterns") or [])
    exclude.extend(profile.get("exclude") or [])

    fan_out_ignore = list(
        config.get("fanOutIgnorePrefixes")
        or profile.get("fanOutIgnorePrefixes")
        or ["System"]
    )

    roots = resolve_roots(args.roots, config)
    files = find_csharp_files(roots, exclude)

    violations: list[Violation] = []
    for path in files:
        try:
            _file_loc, file_violations = analyze_file(path, limits, fan_out_ignore)
        except UnicodeDecodeError as exc:
            print(f"Cannot read {path}: {exc}", file=sys.stderr)
            return 65
        violations.extend(file_violations)

    violations.sort(key=lambda item: (item.path, item.line, item.code, item.subject))
    print_report(profile_name, mode, files, violations, args.max_violations)

    if args.write_baseline_snapshot:
        existing = load_baseline_snapshot(args.write_baseline_snapshot)
        new_count = len({violation_identity(v) for v in violations})
        if existing and new_count > len(existing):
            print(
                f"Ratchet REFUSED: новый baseline вырос с {len(existing)} до {new_count} записей.\n"
                f"Baseline maintainability-budget — only-down. Чини root cause перед re-baseline.\n"
                f"Если нужна санкция на рост (deliberate intent), позови оператора через\n"
                f"  set_intent_status(intent_id=<current>, status='needs_help', reason='нужен рост maintainability baseline: …').",
                file=sys.stderr,
            )
            return 1
        write_baseline_snapshot(args.write_baseline_snapshot, profile_name, violations)
        print(f"Baseline snapshot written: {args.write_baseline_snapshot} ({len(violations)} violation(s))")
        return 0

    if args.baseline_snapshot:
        regressions = compute_regressions(args.baseline_snapshot, violations)
        report_regressions(args.baseline_snapshot, regressions, args.max_violations)
        return 1 if regressions else 0

    if violations and mode == "blocking":
        return 1
    return 0


def violation_identity(violation: Violation) -> tuple[str, str, str]:
    return (violation.code, violation.path, violation.subject)


def write_baseline_snapshot(path: str, profile_name: str, violations: list[Violation]) -> None:
    payload = {
        "version": 1,
        "profile": profile_name,
        "violations": [
            {"code": v.code, "path": v.path, "subject": v.subject}
            for v in violations
        ],
    }
    pathlib.Path(path).write_text(json.dumps(payload, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def load_baseline_snapshot(path: str) -> set[tuple[str, str, str]]:
    snapshot_path = pathlib.Path(path)
    if not snapshot_path.exists():
        print(f"Baseline snapshot не найден: {path}. Все violations считаются новыми.", file=sys.stderr)
        return set()
    payload = json.loads(snapshot_path.read_text(encoding="utf-8"))
    entries = payload.get("violations") or []
    return {(entry["code"], entry["path"], entry["subject"]) for entry in entries}


def compute_regressions(path: str, violations: list[Violation]) -> list[Violation]:
    baseline = load_baseline_snapshot(path)
    return [v for v in violations if violation_identity(v) not in baseline]


def report_regressions(path: str, regressions: list[Violation], max_violations: int) -> None:
    print()
    if not regressions:
        print(f"Ratchet OK: no new violations vs {path}.")
        return

    print(f"Ratchet FAIL: {len(regressions)} new violation(s) vs {path}:")
    for violation in regressions[:max_violations]:
        print(
            f"[{violation.code}] {violation.path}:{violation.line} "
            f"{violation.subject} = {violation.actual} > {violation.limit}"
        )
    remaining = len(regressions) - max_violations
    if remaining > 0:
        print(f"... {remaining} more regression(s) not shown.")
    print()
    print("Fix the new violations or, if intentional, regenerate the baseline:")
    print(f"  scripts/quality/maintainability-budget-check.sh --write-baseline-snapshot {path}")


if __name__ == "__main__":
    raise SystemExit(main())
