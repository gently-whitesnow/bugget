import { useState } from "react";
import type { PhaseTrendWeekly } from "@/shared/api";
import { formatIsoWeekLabel, parseIsoWeek } from "@/shared/lib/time";

type Props = {
  trends: PhaseTrendWeekly[];
};

const formatWeek = (iso: string): string => {
  try {
    return formatIsoWeekLabel(parseIsoWeek(iso));
  } catch {
    return iso;
  }
};

const chartWidth = 720;
const chartHeight = 220;
const paddingLeft = 36;
const paddingRight = 12;
const paddingTop = 12;
const paddingBottom = 40;

const PhaseTrendsChart = ({ trends }: Props) => {
  const [expanded, setExpanded] = useState(false);

  if (trends.length === 0) {
    return (
      <div className="rounded-md border border-base-300 bg-base-100 p-4">
        <div className="text-sm font-medium">
          Динамика по фазам (по неделям)
        </div>
        <div className="py-6 text-center text-sm text-base-content/50">
          Недостаточно данных
        </div>
      </div>
    );
  }

  const innerWidth = chartWidth - paddingLeft - paddingRight;
  const innerHeight = chartHeight - paddingTop - paddingBottom;
  const maxValue = Math.max(
    1,
    ...trends.flatMap((t) => [t.test_days, t.fix_days])
  );

  const slotWidth = innerWidth / trends.length;
  const barGroupPadding = slotWidth * 0.2;
  const groupWidth = slotWidth - barGroupPadding * 2;
  const barWidth = groupWidth / 2;

  const yScale = (v: number) => (v / maxValue) * innerHeight;

  // Несколько горизонтальных gridlines (0, 0.5, 1.0 от max).
  const gridSteps = [0, 0.5, 1];

  return (
    <div className="rounded-md border border-base-300 bg-base-100 p-4">
      <div className="mb-3 flex items-center justify-between">
        <div className="text-sm font-medium">
          Динамика по фазам (по неделям)
        </div>
        <button
          type="button"
          onClick={() => setExpanded((v) => !v)}
          className="text-xs underline text-base-content/70 hover:text-base-content"
        >
          {expanded ? "Скрыть динамику" : "Показать динамику"}
        </button>
      </div>

      {expanded && (
        <>
          <div className="flex items-center gap-4 text-xs text-base-content/70 mb-2">
            <span className="flex items-center gap-1">
              <span className="inline-block h-2 w-2 rounded-sm bg-info" />
              Test (дн)
            </span>
            <span className="flex items-center gap-1">
              <span className="inline-block h-2 w-2 rounded-sm bg-warning" />
              Fix (дн)
            </span>
          </div>

          <div className="w-full overflow-x-auto">
            <svg
              viewBox={`0 0 ${chartWidth} ${chartHeight}`}
              width="100%"
              role="img"
              aria-label="Динамика по фазам по неделям"
              className="min-w-[560px]"
            >
              {/* Gridlines + Y-axis labels */}
              {gridSteps.map((step) => {
                const value = maxValue * step;
                const y = paddingTop + innerHeight - yScale(value);
                return (
                  <g key={step}>
                    <line
                      x1={paddingLeft}
                      x2={chartWidth - paddingRight}
                      y1={y}
                      y2={y}
                      stroke="currentColor"
                      strokeOpacity="0.1"
                      strokeWidth="1"
                    />
                    <text
                      x={paddingLeft - 6}
                      y={y}
                      fontSize="10"
                      textAnchor="end"
                      dominantBaseline="middle"
                      fill="currentColor"
                      opacity="0.5"
                    >
                      {value.toFixed(1)}
                    </text>
                  </g>
                );
              })}

              {/* Bars */}
              {trends.map((t, i) => {
                const slotX = paddingLeft + slotWidth * i + barGroupPadding;
                const testHeight = yScale(t.test_days);
                const fixHeight = yScale(t.fix_days);
                const baseY = paddingTop + innerHeight;
                const label = formatWeek(t.iso_week);
                const labelX = slotX + groupWidth / 2;
                return (
                  <g key={t.iso_week}>
                    <rect
                      x={slotX}
                      y={baseY - testHeight}
                      width={barWidth}
                      height={testHeight}
                      className="fill-info"
                    />
                    <rect
                      x={slotX + barWidth}
                      y={baseY - fixHeight}
                      width={barWidth}
                      height={fixHeight}
                      className="fill-warning"
                    />
                    <text
                      x={labelX}
                      y={baseY + 14}
                      fontSize="10"
                      textAnchor="middle"
                      fill="currentColor"
                      opacity="0.6"
                    >
                      {label}
                    </text>
                  </g>
                );
              })}
            </svg>
          </div>
        </>
      )}
    </div>
  );
};

export default PhaseTrendsChart;
