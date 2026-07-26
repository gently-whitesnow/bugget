import {
  allPeriods,
  periodLabel,
  type AnalyticsPeriod,
} from "@/shared/lib/time";

type Props = {
  value: AnalyticsPeriod;
  onChange: (next: AnalyticsPeriod) => void;
};

const PeriodFilter = ({ value, onChange }: Props) => {
  return (
    <label className="flex items-center gap-2 text-sm">
      <span className="text-base-content/60">Период:</span>
      <select
        className="select select-sm select-bordered"
        value={value}
        onChange={(e) => onChange(e.target.value as AnalyticsPeriod)}
      >
        {allPeriods.map((p) => (
          <option key={p} value={p}>
            {periodLabel(p)}
          </option>
        ))}
      </select>
    </label>
  );
};

export default PeriodFilter;
