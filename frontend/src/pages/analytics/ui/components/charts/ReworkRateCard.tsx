type Props = {
  reworkRate: number | null;
  avgRegressionCyclesWhenPresent: number | null | undefined;
};

const formatPct = (v: number | null): string => {
  if (v === null || v === undefined) return "—";
  return `${Math.round(v * 100)}%`;
};

const formatCycles = (v: number | null | undefined): string =>
  typeof v === "number" ? v.toFixed(1) : "—";

const ReworkRateCard = ({
  reworkRate,
  avgRegressionCyclesWhenPresent,
}: Props) => {
  return (
    <div className="rounded-md border border-base-300 bg-base-100 p-4">
      <div className="text-xs uppercase tracking-wide text-base-content/60">
        Доля регрессий (rework rate)
      </div>
      <div className="mt-2 text-2xl font-semibold tabular-nums">
        {formatPct(reworkRate)}
      </div>
      <div className="mt-1 text-[11px] text-base-content/50">
        Среднее число регрессий: {formatCycles(avgRegressionCyclesWhenPresent)}
      </div>
    </div>
  );
};

export default ReworkRateCard;
