import type { AnalyticsReportBugsByStatus } from "@/shared/api";

type Props = {
  data: AnalyticsReportBugsByStatus;
};

const items: {
  key: keyof AnalyticsReportBugsByStatus;
  title: string;
  tone: string;
}[] = [
  { key: "open", title: "Open", tone: "text-info" },
  { key: "fixed", title: "Fixed", tone: "text-warning" },
  { key: "verified", title: "Verified", tone: "text-success" },
  { key: "rejected", title: "Rejected", tone: "text-base-content/60" },
];

const BugsByStatusGrid = ({ data }: Props) => {
  return (
    <div className="rounded-md border border-base-300 bg-base-100 p-4">
      <div className="mb-3 text-sm font-medium">Баги по статусам</div>
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {items.map((it) => (
          <div
            key={it.key}
            className="rounded-md border border-base-300 bg-base-100 p-3"
          >
            <div className="text-xs uppercase tracking-wide text-base-content/60">
              {it.title}
            </div>
            <div
              className={`mt-1 text-2xl font-semibold tabular-nums ${it.tone}`}
            >
              {data[it.key]}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};

export default BugsByStatusGrid;
