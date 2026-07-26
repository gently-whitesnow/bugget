type Props = {
  avgFixPhaseDays: number | null | undefined;
};

const formatDays = (v: number | null | undefined): string =>
  typeof v === "number" ? `${v.toFixed(1)} дн` : "—";

const AvgFixCard = ({ avgFixPhaseDays }: Props) => {
  return (
    <div className="rounded-md border border-base-300 bg-base-100 p-4">
      <div className="text-xs uppercase tracking-wide text-base-content/60">
        Средняя длительность Fix-фазы
      </div>
      <div className="mt-2 text-2xl font-semibold tabular-nums">
        {formatDays(avgFixPhaseDays)}
      </div>
      <div className="mt-1 text-[11px] text-base-content/50">
        Среди fix-фаз с участием пользователя
      </div>
    </div>
  );
};

export default AvgFixCard;
