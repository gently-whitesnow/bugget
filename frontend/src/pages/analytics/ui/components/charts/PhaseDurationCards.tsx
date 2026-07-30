import type { AvgPhaseDurationDays } from "@/shared/api";

type Props = {
  data: AvgPhaseDurationDays | null;
};

const formatDays = (v: number | null | undefined): string =>
  typeof v === "number" ? `${v.toFixed(1)} дн` : "—";

const cards: { key: keyof AvgPhaseDurationDays; title: string }[] = [
  { key: "testInitial", title: "Test (первичный)" },
  { key: "testRetest", title: "Test (повторный)" },
  { key: "fix", title: "Fix" },
];

const PhaseDurationCards = ({ data }: Props) => {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
      {cards.map((c) => {
        const value = data ? data[c.key] : null;
        return (
          <div
            key={c.key}
            className="rounded-md border border-base-300 bg-base-100 p-4"
          >
            <div className="text-xs uppercase tracking-wide text-base-content/60">
              {c.title}
            </div>
            <div className="mt-2 text-2xl font-semibold tabular-nums">
              {formatDays(value as number | null | undefined)}
            </div>
            <div className="mt-1 text-[11px] text-base-content/50">
              Среднее время фазы
            </div>
          </div>
        );
      })}
    </div>
  );
};

export default PhaseDurationCards;
