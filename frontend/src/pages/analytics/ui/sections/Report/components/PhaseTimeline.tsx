import type { AnalyticsReportPhaseEntry } from "@/shared/api";

type Props = {
  entries: AnalyticsReportPhaseEntry[];
};

const formatDateTime = (iso: string): string => {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${pad(d.getDate())}.${pad(d.getMonth() + 1)}.${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

const formatDuration = (days: number | null | undefined): string => {
  if (typeof days !== "number") return "—";
  if (days < 1) {
    const hours = Math.max(1, Math.round(days * 24));
    return `${hours} ч`;
  }
  return `${days.toFixed(1)} д`;
};

const phaseLabel = (phase: AnalyticsReportPhaseEntry["phase"]): string =>
  phase === "Test" ? "Test" : "Fix";

const phaseColor = (
  phase: AnalyticsReportPhaseEntry["phase"],
  active: boolean
): string => {
  if (active) {
    return phase === "Test"
      ? "bg-info text-info-content"
      : "bg-warning text-warning-content";
  }
  return phase === "Test"
    ? "bg-info/15 text-info border border-info/30"
    : "bg-warning/15 text-warning border border-warning/30";
};

const PhaseTimeline = ({ entries }: Props) => {
  if (entries.length === 0) {
    return (
      <div className="rounded-md border border-base-300 bg-base-100 p-4 text-sm text-base-content/60">
        Нет данных о фазах для этого репорта.
      </div>
    );
  }

  // Рассчитываем относительную ширину для gantt-полосы. Активный интервал
  // получает «фантомную» длительность от entered_at до now.
  const now = Date.now();
  const widths = entries.map((e) => {
    if (typeof e.duration_days === "number" && e.duration_days > 0) {
      return e.duration_days;
    }
    if (!e.exited_at) {
      const start = new Date(e.entered_at).getTime();
      if (!Number.isNaN(start)) {
        return Math.max(0.01, (now - start) / (1000 * 60 * 60 * 24));
      }
    }
    return 0.01;
  });
  const totalWidth = widths.reduce((acc, w) => acc + w, 0) || 1;

  return (
    <div className="rounded-md border border-base-300 bg-base-100 p-4">
      <div className="mb-3 flex items-center justify-between">
        <div className="text-sm font-medium">Таймлайн фаз</div>
        <div className="text-xs text-base-content/50">{entries.length} шт.</div>
      </div>

      {/* Gantt-полоска */}
      <div className="mb-4 flex h-3 w-full overflow-hidden rounded-sm bg-base-200">
        {entries.map((e, idx) => {
          const active = !e.exited_at;
          const pct = (widths[idx] / totalWidth) * 100;
          return (
            <div
              key={`${e.regression_cycle_index}-${idx}`}
              title={`${phaseLabel(e.phase)} · цикл #${e.regression_cycle_index} · ${formatDuration(e.duration_days)}`}
              className={`h-full ${phaseColor(e.phase, active)} ${
                active ? "animate-pulse" : ""
              }`}
              style={{ width: `${pct}%` }}
            />
          );
        })}
      </div>

      {/* Список интервалов */}
      <ul className="flex flex-col gap-1">
        {entries.map((e, idx) => {
          const active = !e.exited_at;
          return (
            <li
              key={`${e.regression_cycle_index}-${idx}-row`}
              className={`flex flex-wrap items-center gap-2 rounded-sm px-2 py-1.5 ${
                active ? "bg-base-200" : "hover:bg-base-200/50"
              }`}
            >
              <span
                className={`inline-flex min-w-[42px] justify-center rounded-sm px-2 py-0.5 text-[11px] font-medium uppercase tracking-wide ${phaseColor(
                  e.phase,
                  active
                )}`}
              >
                {phaseLabel(e.phase)}
              </span>
              <span className="text-xs text-base-content/60">
                цикл #{e.regression_cycle_index}
              </span>
              <span className="text-xs tabular-nums">
                {formatDuration(e.duration_days)}
              </span>
              <span className="ml-auto text-[11px] tabular-nums text-base-content/60">
                {formatDateTime(e.entered_at)}
                {" → "}
                {e.exited_at ? (
                  formatDateTime(e.exited_at)
                ) : (
                  <span className="font-medium text-warning">Идёт сейчас</span>
                )}
              </span>
            </li>
          );
        })}
      </ul>
    </div>
  );
};

export default PhaseTimeline;
