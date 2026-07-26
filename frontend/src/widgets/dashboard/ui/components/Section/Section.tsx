import { useUnit } from "effector-react";

import { $reportsUsersStore } from "@/entities/report-list";
import { ReportCard } from "@/entities/report";
import type { ReportListItem } from "@/entities/report-list";

type Props = {
  title: string;
  reports: ReportListItem[];
  className?: string;
};

const Section = ({ title, reports, className }: Props) => {
  const usersStore = useUnit($reportsUsersStore);

  return (
    <section className="flex flex-col gap-2">
      <div className="text-lg text-base-content">{title}</div>
      <div className="flex flex-col gap-1">
        {!!reports.length &&
          reports.map((report) => (
            <ReportCard
              key={report.id}
              report={report}
              usersStore={usersStore}
              className={className}
            />
          ))}
      </div>
    </section>
  );
};

export default Section;
