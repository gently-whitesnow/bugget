#!/usr/bin/env python3
"""Audit per-file analyzer suppressions across the bugget-api solution.

Tracks two surfaces:
  * `#pragma warning disable` directives in any `.cs` source.
  * `dotnet_diagnostic.<RULE>.severity = none|suggestion` entries inside
    non-glob (per-file) sections of any `.editorconfig`.

Each suppression must carry a justification: a `//` (pragma) or `#`
(editorconfig) prefixed comment block within the preceding 15 lines that
mentions `intent:<id>` (id is the hex string of an Intent in throne).

Two failure modes:
  1. unjustified  — entry without `intent:` reference within preceding window.
  2. unbaselined  — entry present in code but absent from baseline snapshot.
                    Ratchet: count can only fall, growth is refused.

Subcommands:
  check           — fail if any new entry exists vs. baseline OR any new
                    entry lacks an `intent:` reference. Default.
  write-baseline  — overwrite `.quality/suppressions-baseline.json`. Refuses
                    to grow the count vs. an existing baseline.
  list            — print the current entries and exit 0.

Counterpart of throne's `scripts/quality/suppression_audit.py`; same shape,
adapted for the flat bugget-api project layout and CS/CA rule families.
"""

from __future__ import annotations

import argparse
import json
import pathlib
import re
import sys
from dataclasses import asdict, dataclass
from typing import Iterable


REPO_ROOT = pathlib.Path(__file__).resolve().parent.parent.parent
BASELINE = REPO_ROOT / ".quality" / "suppressions-baseline.json"

JUSTIFICATION_RE = re.compile(r"intent:[0-9a-fA-F]{16,}")
LOOKBACK = 15
PRAGMA_RE = re.compile(r"#pragma\s+warning\s+disable\s+([A-Z]+\d+(?:\s*,\s*[A-Z]+\d+)*)")
SEVERITY_RE = re.compile(
    r"^dotnet_diagnostic\.([A-Z]+\d{3,5})\.severity\s*=\s*(none|suggestion)\s*$"
)
SECTION_RE = re.compile(r"^\[(.+)\]\s*$")
EXCLUDED_PATH_PARTS = ("/bin/", "/obj/", "/Generated/", "/Migrations/", "/TestResults/")


@dataclass(frozen=True, order=True)
class Entry:
    kind: str
    location: str
    rule: str

    def to_dict(self) -> dict:
        return asdict(self)


def is_excluded(path: pathlib.Path) -> bool:
    rel = "/" + path.relative_to(REPO_ROOT).as_posix()
    if rel.endswith(".g.cs"):
        return True
    return any(part in rel for part in EXCLUDED_PATH_PARTS)


def iter_cs_files() -> Iterable[pathlib.Path]:
    for cs_file in REPO_ROOT.rglob("*.cs"):
        if is_excluded(cs_file):
            continue
        yield cs_file


def iter_editorconfigs() -> Iterable[pathlib.Path]:
    for ec in REPO_ROOT.rglob(".editorconfig"):
        if is_excluded(ec):
            continue
        yield ec


def scan_comment_block(
    lines: list[str], anchor_line: int, comment_prefix: str
) -> tuple[str, bool]:
    """Walk LOOKBACK lines above `anchor_line` looking for a comment block
    that mentions `intent:`. Stops at the first non-comment, non-blank line
    after at least one comment line was collected.
    """
    start = max(0, anchor_line - 1 - LOOKBACK)
    end = anchor_line - 1
    window = lines[start:end]
    block: list[str] = []
    for raw in reversed(window):
        stripped = raw.strip()
        if not stripped:
            if block:
                break
            continue
        if stripped.startswith(comment_prefix):
            block.append(stripped.lstrip(comment_prefix).strip())
            continue
        break
    block.reverse()
    snippet = " | ".join(block)
    ok = bool(JUSTIFICATION_RE.search(snippet))
    return snippet, ok


def parse_editorconfig(ec_path: pathlib.Path) -> list[tuple[Entry, bool, str]]:
    lines = ec_path.read_text(encoding="utf-8").splitlines()
    rel_ec = ec_path.relative_to(REPO_ROOT).as_posix()
    results: list[tuple[Entry, bool, str]] = []

    current_section: str | None = None
    current_section_line = 0

    for idx, raw in enumerate(lines, start=1):
        line = raw.strip()
        section_match = SECTION_RE.match(line)
        if section_match:
            current_section = section_match.group(1)
            current_section_line = idx
            continue
        sev = SEVERITY_RE.match(line)
        if not sev or not current_section:
            continue
        # only per-file sections matter — glob sections like
        # `[**/Bugget.Tests/**/*.cs]` are systemic policies, not pin-point
        # suppressions of a single file. Skip them.
        if "*" in current_section:
            continue
        rule = sev.group(1)
        location = f"{rel_ec}:{current_section}"
        entry = Entry(kind="editorconfig", location=location, rule=rule)
        snippet, ok = scan_comment_block(lines, current_section_line, "#")
        results.append((entry, ok, snippet))

    return results


def parse_pragmas(cs_path: pathlib.Path) -> list[tuple[Entry, bool, str]]:
    try:
        lines = cs_path.read_text(encoding="utf-8").splitlines()
    except UnicodeDecodeError:
        return []
    rel = cs_path.relative_to(REPO_ROOT).as_posix()
    results: list[tuple[Entry, bool, str]] = []
    for idx, raw in enumerate(lines, start=1):
        match = PRAGMA_RE.search(raw)
        if not match:
            continue
        rules = [r.strip() for r in match.group(1).split(",") if r.strip()]
        snippet, ok = scan_comment_block(lines, idx, "//")
        for rule in rules:
            entry = Entry(kind="pragma", location=f"{rel}:{idx}", rule=rule)
            results.append((entry, ok, snippet))
    return results


def collect_all() -> list[tuple[Entry, bool, str]]:
    results: list[tuple[Entry, bool, str]] = []
    for ec in iter_editorconfigs():
        results.extend(parse_editorconfig(ec))
    for cs_file in iter_cs_files():
        results.extend(parse_pragmas(cs_file))
    return results


def load_baseline() -> set[Entry]:
    if not BASELINE.exists():
        return set()
    raw = json.loads(BASELINE.read_text(encoding="utf-8"))
    return {Entry(**item) for item in raw.get("entries", [])}


def write_baseline_file(entries: Iterable[Entry]) -> None:
    payload = {
        "$schema": "https://example.invalid/bugget-suppress-baseline.schema.json",
        "description": "Per-file analyzer suppression baseline. Ratcheted: count can only decrease.",
        "entries": [e.to_dict() for e in sorted(set(entries))],
    }
    BASELINE.parent.mkdir(parents=True, exist_ok=True)
    BASELINE.write_text(
        json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )


def cmd_list() -> int:
    all_entries = collect_all()
    print(f"Found {len(all_entries)} per-file analyzer suppressions.\n")
    for entry, ok, snippet in sorted(
        all_entries, key=lambda e: (e[0].kind, e[0].location, e[0].rule)
    ):
        flag = "OK " if ok else "?? "
        print(f"  {flag} [{entry.kind}] {entry.location} :: {entry.rule}")
        if snippet:
            print(f"       L {snippet[:140]}")
    return 0


def cmd_write_baseline() -> int:
    entries = [e for e, _, _ in collect_all()]
    new_set = set(entries)
    existing = load_baseline()
    if existing and len(new_set) > len(existing):
        print(
            f"Ratchet REFUSED: suppress baseline вырос с {len(existing)} до {len(new_set)} записей.\n"
            f"Baseline only-down. Удали suppression в коде вместо re-baseline.\n"
            f"Если рост оправдан — приложи интент и попроси оператора подтвердить через\n"
            f"  set_intent_status(intent_id=<current>, status='needs_help', reason='нужен рост suppress baseline: …').",
            file=sys.stderr,
        )
        return 1
    write_baseline_file(entries)
    rel = BASELINE.relative_to(REPO_ROOT)
    print(f"Wrote {len(new_set)} entries to {rel}.")
    return 0


def cmd_check() -> int:
    all_entries = collect_all()
    baseline = load_baseline()

    current_set = {e for e, _, _ in all_entries}
    justified_map = {e: (ok, snippet) for e, ok, snippet in all_entries}
    new_entries = current_set - baseline
    removed = baseline - current_set

    new_unjustified = [
        (e, justified_map[e][1]) for e in new_entries if not justified_map[e][0]
    ]

    print(f"Total suppressions: {len(current_set)} (baseline: {len(baseline)})")
    if removed:
        print(
            f"  removed {len(removed)} entries vs baseline "
            f"(ratchet down — re-run write-baseline to lock in)"
        )
        for entry in sorted(removed):
            print(f"      - [{entry.kind}] {entry.location} :: {entry.rule}")

    fail = False

    if new_unjustified:
        print(
            f"\nFAIL: {len(new_unjustified)} NEW suppression(s) without intent: "
            f"reference in preceding {LOOKBACK} lines."
        )
        print("   New suppressions MUST cite an intent id in a comment block")
        print("   immediately above. Fix the root cause OR add a justification.")
        for entry, snippet in new_unjustified:
            print(f"    [{entry.kind}] {entry.location} :: {entry.rule}")
            print(f"        preceding comment: {snippet or '<none>'}")
        fail = True

    other_new = [e for e in new_entries if justified_map[e][0]]
    if other_new:
        print(
            f"\nFAIL: {len(other_new)} NEW suppression(s) (justified, but not in baseline)."
        )
        print("   Ratchet policy: any new suppression — even with intent: — requires")
        print("   re-baselining with explicit rationale in the commit message.")
        for entry in sorted(other_new):
            print(f"    [{entry.kind}] {entry.location} :: {entry.rule}")
        fail = True

    if not fail:
        print("\nOK: no new suppressions vs baseline.")
    return 1 if fail else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "command",
        choices=("check", "write-baseline", "list"),
        nargs="?",
        default="check",
    )
    args = parser.parse_args()
    if args.command == "check":
        return cmd_check()
    if args.command == "write-baseline":
        return cmd_write_baseline()
    if args.command == "list":
        return cmd_list()
    return 64


if __name__ == "__main__":
    sys.exit(main())
