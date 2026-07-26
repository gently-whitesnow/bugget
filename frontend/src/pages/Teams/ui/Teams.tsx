import { useEffect, useMemo } from "react";
import { useUnit } from "effector-react";
import {
  $reportsStore,
  $reportsUsersStore,
  loadReportsFx,
} from "@/entities/report-list";
import {
  $isDashboardVisible,
  showDashboard,
  hideDashboard,
} from "@/entities/dashboard";
import { ReportCard } from "@/entities/report";
import { DashboardContent } from "@/widgets/dashboard";
import { $workspaces, fetchWorkspacesFx } from "@/entities/saas/workspace";
import type { ReportListItem } from "@/entities/report-list";
import { useLocation, useParams } from "react-router-dom";

type TeamsView = "dashboard" | "team";

const resolveView = (hash: string): TeamsView => {
  if (hash === "#team") return "team";
  return "dashboard";
};

export const Teams = () => {
  const { teamId } = useParams<{ teamId: string }>();
  const location = useLocation();
  const isDashboardVisible = useUnit($isDashboardVisible);
  const [
    hideDashboardUnit,
    showDashboardUnit,
    runFetchWorkspaces,
    runLoadReports,
  ] = useUnit([hideDashboard, showDashboard, fetchWorkspacesFx, loadReportsFx]);

  const view = useMemo(() => resolveView(location.hash), [location.hash]);

  // Поддерживаем legacy $isDashboardVisible (используется сайдбаром / др. виджетами).
  useEffect(() => {
    if (view === "dashboard") {
      showDashboardUnit();
    } else {
      hideDashboardUnit();
    }
  }, [view, hideDashboardUnit, showDashboardUnit]);

  const teamReports = useUnit($reportsStore);
  const usersStore = useUnit($reportsUsersStore);
  const workspaces = useUnit($workspaces);

  // Загружаем workspaces если их нет (для сайдбара)
  useEffect(() => {
    if (workspaces.length === 0) {
      void runFetchWorkspaces();
    }
  }, [workspaces.length, runFetchWorkspaces]);

  // Загружаем репорты команды только при view=team.
  useEffect(() => {
    if (view === "team" && teamId) {
      void runLoadReports({ teamId });
    }
  }, [view, teamId, runLoadReports]);

  if (view === "dashboard" && isDashboardVisible) {
    return (
      <div className="flex flex-col gap-4">
        <DashboardContent />
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <section className="flex flex-col gap-2">
        <div className="text-lg text-base-content">Репорты команды</div>
        <div className="flex flex-col gap-1">
          {teamReports.reports && teamReports.reports.length > 0 ? (
            teamReports.reports.map((report: ReportListItem) => (
              <ReportCard
                key={report.id}
                report={report}
                usersStore={usersStore}
              />
            ))
          ) : (
            <p className="text-sm text-base-content/50">Репортов нет</p>
          )}
        </div>
      </section>
    </div>
  );
};
