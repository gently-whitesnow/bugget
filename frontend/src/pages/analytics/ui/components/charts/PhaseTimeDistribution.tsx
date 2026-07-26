import type { PhaseTimeDistribution as PhaseDist } from "@/shared/api";

type Props = {
  data: PhaseDist | null;
};

const formatPct = (v: number): string => `${Math.round(v * 100)}%`;

const PhaseTimeDistribution = ({ data }: Props) => {
  const testPct = data?.test_pct ?? 0;
  const fixPct = data?.fix_pct ?? 0;
  const total = testPct + fixPct;
  const hasData = total > 0;

  const testWidth = hasData ? (testPct / total) * 100 : 0;
  const fixWidth = hasData ? (fixPct / total) * 100 : 0;

  return (
    <div className="rounded-md border border-base-300 bg-base-100 p-4">
      <div className="text-xs uppercase tracking-wide text-base-content/60">
        Распределение времени по фазам
      </div>

      {hasData ? (
        <>
          <div className="mt-3 flex h-3 w-full overflow-hidden rounded-full bg-base-200">
            <div
              className="bg-info"
              style={{ width: `${testWidth}%` }}
              aria-label="Test"
            />
            <div
              className="bg-warning"
              style={{ width: `${fixWidth}%` }}
              aria-label="Fix"
            />
          </div>
          <div className="mt-2 flex justify-between text-xs">
            <span className="flex items-center gap-1">
              <span className="inline-block h-2 w-2 rounded-sm bg-info" />
              Test {formatPct(testPct)}
            </span>
            <span className="flex items-center gap-1">
              <span className="inline-block h-2 w-2 rounded-sm bg-warning" />
              Fix {formatPct(fixPct)}
            </span>
          </div>
        </>
      ) : (
        <div className="mt-3 text-sm text-base-content/50">
          Недостаточно данных
        </div>
      )}
    </div>
  );
};

export default PhaseTimeDistribution;
