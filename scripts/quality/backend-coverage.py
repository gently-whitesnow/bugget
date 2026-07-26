#!/usr/bin/env python3
"""Покрытие кода бекенда тестами с ratchet-бейзлайном (гейт backend-coverage).

Гейт отвечает на один вопрос: стало ли покрытие хуже, чем было. Абсолютной планки нет —
она бессмысленна, пока реальная цифра низкая: любая честная планка либо не достижима,
либо не мешает. Вместо планки — снимок в .quality/backend-coverage.json: покрытие ниже
снимка (с допуском tolerance) валит гейт, выше — снимок можно подтянуть через --update.
Ratchet держится и по решению целиком, и по каждой сборке отдельно: иначе рост покрытия
в одном модуле маскирует провал в другом.

Как считается:
  1. прогоняются все тестовые проекты (IsTestProject) с coverlet.collector;
  2. отчёты сливаются ReportGenerator'ом в artifacts/coverage/report (HTML + Cobertura +
     сводка), HTML публикуется артефактом CI;
  3. слитый Cobertura разбирается построчно — отсюда и проценты, и список дыр.

Что не измеряется — backend/coverlet.runsettings (тесты, *.DbUp, сгенерированное из
OpenAPI). Сборки, до которых не дотянулся ни один тест, в проценты не попадают вовсе:
они перечисляются отдельным списком «нет данных» и считаются нулём при выборе, что
покрывать дальше.

  backend-coverage.py                проверить (прогон тестов + сверка со снимком)
  backend-coverage.py --skip-tests   сверить по уже собранным отчётам, не гоняя тесты
  backend-coverage.py --gaps         показать самые непокрытые файлы бизнес-логики
  backend-coverage.py --update       пересобрать снимок из текущего замера
"""

from __future__ import annotations

import argparse
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field

import quality_csharp as q

CONFIG = q.ROOT / ".quality" / "backend-coverage.json"
BACKEND = q.ROOT / "backend"
RUNSETTINGS = BACKEND / "coverlet.runsettings"

# Куда кладутся сырые отчёты тестовых прогонов и слитый отчёт. Каталог не в git:
# в CI он публикуется артефактом, локально просто перезаписывается.
ARTIFACTS = q.ROOT / "artifacts" / "coverage"
RAW = ARTIFACTS / "raw"
REPORT = ARTIFACTS / "report"
MERGED = REPORT / "Cobertura.xml"


@dataclass
class Counters:
    lines_covered: int = 0
    lines_total: int = 0
    branches_covered: int = 0
    branches_total: int = 0

    def add(self, other: "Counters") -> None:
        self.lines_covered += other.lines_covered
        self.lines_total += other.lines_total
        self.branches_covered += other.branches_covered
        self.branches_total += other.branches_total

    @property
    def line(self) -> float:
        return percent(self.lines_covered, self.lines_total)

    @property
    def branch(self) -> float:
        return percent(self.branches_covered, self.branches_total)


@dataclass
class Assembly:
    name: str
    totals: Counters = field(default_factory=Counters)
    files: dict[str, Counters] = field(default_factory=dict)


def percent(covered: int, total: int) -> float:
    """Процент с одним знаком. Нет строк — считаем полностью покрытым, а не нулём."""
    return 100.0 if total == 0 else round(covered * 100.0 / total, 1)


#-----------------------------------------------------------------------------------------
# Прогон тестов
#-----------------------------------------------------------------------------------------


def test_projects(config: dict) -> list[str]:
    """Тестовые проекты ищутся по IsTestProject, а не по списку, — как в dotnet-test.sh:
    новый тестовый проект не может молча выпасть из замера."""
    skip = tuple(config.get("skipTestProjects", []))
    found = [
        q.rel(path)
        for path in sorted(BACKEND.rglob("*.csproj"))
        if "<IsTestProject>true</IsTestProject>" in path.read_text(encoding="utf-8")
        and not q.matches_any(path.stem, skip)
    ]
    if not found:
        raise SystemExit("не нашлось ни одного тестового проекта в backend/")
    return found


def run(cmd: list[str], where=q.ROOT) -> int:
    print(f"$ ({q.rel(where)}) {' '.join(cmd)}", flush=True)
    return subprocess.call(cmd, cwd=where)


def collect_coverage(config: dict) -> None:
    if RAW.exists():
        shutil.rmtree(RAW)
    RAW.mkdir(parents=True)

    projects = test_projects(config)
    print(f"замер покрытия: {len(projects)} тестовый(х) проект(ов)")
    for project in projects:
        print(f"  {project}")

    failed = []
    for project in projects:
        print(f"\n== {project} ==", flush=True)
        code = run(
            [
                "dotnet", "test", project,
                "-c", "Release", "--no-restore",
                "--collect:XPlat Code Coverage",
                "--settings", q.rel(RUNSETTINGS),
                "--results-directory", q.rel(RAW),
            ]
        )
        if code != 0:
            failed.append(project)

    if failed:
        # Красные тесты делают цифру покрытия неинформативной: часть кода не выполнилась
        # не потому, что её не покрыли, а потому, что прогон упал раньше.
        print("\nтесты упали, замер покрытия недостоверен:", file=sys.stderr)
        for project in failed:
            print(f"  {project}", file=sys.stderr)
        raise SystemExit(1)


def merge_reports() -> None:
    reports = sorted(RAW.rglob("coverage.cobertura.xml"))
    if not reports:
        raise SystemExit(
            "не нашлось ни одного coverage.cobertura.xml — "
            "проверь, что тестовые проекты ссылаются на coverlet.collector"
        )
    print(f"\nотчётов от тестовых прогонов: {len(reports)}")

    if REPORT.exists():
        shutil.rmtree(REPORT)
    if run(["dotnet", "tool", "restore"]) != 0:
        raise SystemExit("не удалось восстановить локальные dotnet-инструменты")

    code = run(
        [
            "dotnet", "reportgenerator",
            f"-reports:{q.rel(RAW)}/**/coverage.cobertura.xml",
            f"-targetdir:{q.rel(REPORT)}",
            "-reporttypes:Html;Cobertura;TextSummary;MarkdownSummaryGithub",
            f"-sourcedirs:{q.rel(BACKEND)}",
            "-verbosity:Warning",
        ]
    )
    if code != 0 or not MERGED.exists():
        raise SystemExit("ReportGenerator не собрал сводный отчёт")


#-----------------------------------------------------------------------------------------
# Разбор слитого Cobertura
#-----------------------------------------------------------------------------------------


def parse_condition(text: str) -> tuple[int, int]:
    """'75% (3/4)' → (3, 4)."""
    inside = text[text.find("(") + 1 : text.find(")")]
    covered, _, total = inside.partition("/")
    return int(covered), int(total)


def read_measurement() -> dict[str, Assembly]:
    if not MERGED.exists():
        raise SystemExit(
            f"нет сводного отчёта {q.rel(MERGED)} — запусти без --skip-tests"
        )

    assemblies: dict[str, Assembly] = {}
    for package in ET.parse(MERGED).getroot().iter("package"):
        name = package.get("name") or "(без имени)"
        assembly = assemblies.setdefault(name, Assembly(name))

        for klass in package.iter("class"):
            filename = (klass.get("filename") or "(без файла)").replace("\\", "/")
            counters = assembly.files.setdefault(filename, Counters())
            for line in klass.iter("line"):
                counters.lines_total += 1
                if int(line.get("hits") or 0) > 0:
                    counters.lines_covered += 1
                if line.get("branch") == "true" and line.get("condition-coverage"):
                    covered, total = parse_condition(line.get("condition-coverage"))
                    counters.branches_covered += covered
                    counters.branches_total += total

        assembly.totals = Counters()
        for counters in assembly.files.values():
            assembly.totals.add(counters)

    if not assemblies:
        raise SystemExit(f"в {q.rel(MERGED)} нет ни одной сборки")
    return assemblies


def totals_of(assemblies: dict[str, Assembly]) -> Counters:
    total = Counters()
    for assembly in assemblies.values():
        total.add(assembly.totals)
    return total


def unreached(assemblies: dict[str, Assembly], config: dict) -> list[str]:
    """Продуктовые сборки, до которых не дотянулся ни один тест.

    В проценты они не попадают — их нет в отчёте вовсе, — поэтому перечисляются отдельно:
    без этого списка общая цифра выглядит лучше, чем есть."""
    skip = tuple(config.get("notMeasured", []))
    product = {
        path.stem
        for path in BACKEND.rglob("*.csproj")
        if "<IsTestProject>true</IsTestProject>" not in path.read_text(encoding="utf-8")
    }
    return sorted(
        name
        for name in product - set(assemblies)
        if not any(q.matches_any(name, [pattern]) for pattern in skip)
    )


#-----------------------------------------------------------------------------------------
# Вывод
#-----------------------------------------------------------------------------------------


def print_measurement(assemblies: dict[str, Assembly], config: dict) -> None:
    def row(title: str, counters: Counters) -> str:
        # Ветвлений нет вовсе — печатаем прочерк: «100%» здесь читалось бы как заслуга.
        branch = f"{counters.branch:>5}%" if counters.branches_total else "    —"
        return (
            f"  {title:<32} строки {counters.line:>5}%  "
            f"({counters.lines_covered}/{counters.lines_total})".ljust(66)
            + f"ветви {branch}"
        )

    print("\nпокрытие по сборкам:")
    for name in sorted(assemblies):
        print(row(name, assemblies[name].totals))
    print(row("ИТОГО", totals_of(assemblies)))

    missing = unreached(assemblies, config)
    if missing:
        print("\nсборки, до которых не дотянулся ни один тест (в проценты не входят):")
        for name in missing:
            print(f"  - {name}")


def print_gaps(assemblies: dict[str, Assembly], config: dict, limit: int) -> None:
    """Самые непокрытые файлы — вход для следующих задач по тестам."""
    focus = config.get("gapsFocus", [])
    rows = [
        (name, filename, counters)
        for name, assembly in assemblies.items()
        if not focus or q.matches_any(name, focus)
        for filename, counters in assembly.files.items()
        if counters.lines_total - counters.lines_covered > 0
    ]
    rows.sort(key=lambda row: (-(row[2].lines_total - row[2].lines_covered), row[1]))

    scope = ", ".join(focus) if focus else "все сборки"
    print(f"\nсамые непокрытые файлы ({scope}), топ {limit}:")
    for name, filename, counters in rows[:limit]:
        uncovered = counters.lines_total - counters.lines_covered
        short = filename.split("/backend/")[-1]
        print(f"  {uncovered:>5} непокрытых строк  {counters.line:>5}%  [{name}] {short}")


#-----------------------------------------------------------------------------------------
# Ratchet
#-----------------------------------------------------------------------------------------


def metrics(counters: Counters) -> dict:
    """null там, где мерить нечего: сборка без ветвлений не «покрыта на 100%», и первое
    же появившееся в ней ветвление не должно читаться как падение покрытия."""
    return {
        "line": counters.line if counters.lines_total else None,
        "branch": counters.branch if counters.branches_total else None,
    }


def snapshot(assemblies: dict[str, Assembly]) -> dict:
    return {
        "total": metrics(totals_of(assemblies)),
        "assemblies": {
            name: metrics(assemblies[name].totals) for name in sorted(assemblies)
        },
    }


def check(assemblies: dict[str, Assembly], config: dict) -> int:
    tolerance = float(config.get("tolerance", 0))
    baseline = config.get("baseline", {})
    base_total = baseline.get("total", {})
    base_assemblies = baseline.get("assemblies", {})
    measured = snapshot(assemblies)

    dropped: list[str] = []
    grown: list[str] = []
    fresh: list[str] = []

    def compare(title: str, was: dict, now: dict) -> None:
        for metric, label in (("line", "строки"), ("branch", "ветви")):
            before, after = was.get(metric), now.get(metric)
            if before is None or after is None:
                continue
            if after < before - tolerance:
                dropped.append(f"{title}: {label} {after}%, было {before}% (допуск {tolerance} п.п.)")
            elif after > before:
                grown.append(f"{title}: {label} {after}%, было {before}%")

    compare("ИТОГО", base_total, measured["total"])
    for name, now in measured["assemblies"].items():
        was = base_assemblies.get(name)
        if was is None:
            fresh.append(name)
            continue
        compare(name, was, now)

    stale = sorted(set(base_assemblies) - set(measured["assemblies"]))

    if grown or fresh or stale:
        print("\nснимок можно подтянуть (backend-coverage.py --update):")
        for item in grown:
            print(f"  - {item}")
        for name in fresh:
            print(f"  - {name}: сборки нет в снимке")
        for name in stale:
            print(f"  - {name}: сборки больше нет в замере")

    if not dropped:
        print("\nпокрытие не ниже снимка.")
        return 0

    print("\nпокрытие упало ниже снимка:")
    for item in dropped:
        print(f"  ✘ {item}")
    print(
        "\nЗакрой упавшее тестами. Если падение осознанное (удалены тесты, вырезан модуль) —"
        "\nпересобери снимок командой"
        "\n     python3 scripts/quality/backend-coverage.py --update --skip-tests"
        "\nотдельным коммитом с обоснованием в теле коммита (ADR-0002)."
    )
    return 1


#-----------------------------------------------------------------------------------------


def main() -> int:
    parser = argparse.ArgumentParser(
        description="покрытие бекенда тестами с ratchet-бейзлайном",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    parser.add_argument("--skip-tests", action="store_true",
                        help="не гонять тесты, пересобрать сводку по отчётам прошлого прогона")
    parser.add_argument("--update", action="store_true", help="пересобрать снимок")
    parser.add_argument("--gaps", nargs="?", type=int, const=25, default=0,
                        metavar="N", help="показать N самых непокрытых файлов (по умолчанию 25)")
    args = parser.parse_args()

    config = q.read_json(CONFIG)

    if not args.skip_tests:
        collect_coverage(config)
    merge_reports()

    assemblies = read_measurement()
    print_measurement(assemblies, config)
    if args.gaps:
        print_gaps(assemblies, config, args.gaps)

    print(f"\nотчёт: {q.rel(REPORT)}/index.html")

    if args.update:
        config["baseline"] = snapshot(assemblies)
        q.write_json(CONFIG, config)
        print(f"снимок пересобран: {q.rel(CONFIG)}")
        return 0

    return check(assemblies, config)


if __name__ == "__main__":
    sys.exit(main())
