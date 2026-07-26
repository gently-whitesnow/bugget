import { useUnit } from "effector-react";
import { X } from "lucide-react";

import {
  $responsibleUserNameStore,
  $participantsWithNamesStore,
  changeResponsibleUserIdEvent,
  $responsibleUserIdStore,
  $reportIdStore,
  $isExcludedFromAnalyticsStore,
  updateIsExcludedFromAnalyticsEvent,
} from "@/entities/report";
import { SidebarContainer } from "@/shared/ui";
import { useResponsibleInvite } from "./hooks/useResponsibleInvite";
import { useReportSidebar } from "./useReportSidebar";
import Content from "./components/Content";

import "./ReportSidebar.css";

const ReportSidebar = () => {
  const { isMobileSidebarOpen, isMobileSidebarMounted, closeMobileSidebar } =
    useReportSidebar();
  const [
    responsibleUserName,
    responsibleUserId,
    participantsWithNames,
    reportId,
    isExcludedFromAnalytics,
  ] = useUnit([
    $responsibleUserNameStore,
    $responsibleUserIdStore,
    $participantsWithNamesStore,
    $reportIdStore,
    $isExcludedFromAnalyticsStore,
  ]);
  const changeResponsibleUser = useUnit(changeResponsibleUserIdEvent);
  const updateIsExcludedFromAnalytics = useUnit(
    updateIsExcludedFromAnalyticsEvent
  );
  const {
    isVisible: shouldShowResponsibleLinkButton,
    isCopied,
    copyLink: handleCopyResponsibleLink,
  } = useResponsibleInvite();

  const responsibleUserImageUrl = participantsWithNames?.find(
    (p) => p.id === responsibleUserId
  )?.imageUrl;

  const numericReportId = reportId === null ? null : Number(reportId);
  const excludeReportId =
    numericReportId !== null && Number.isFinite(numericReportId)
      ? numericReportId
      : null;

  const contentProps = {
    responsibleUserName,
    responsibleUserId,
    responsibleUserImageUrl,
    participantsWithNames,
    shouldShowResponsibleLinkButton,
    isCopied,
    onResponsibleUserChange: changeResponsibleUser,
    onCopyResponsibleLink: handleCopyResponsibleLink,
    excludeReportId,
    isExcludedFromAnalytics,
    onIsExcludedChange: updateIsExcludedFromAnalytics,
  };

  return (
    <>
      <div className="report-sidebar-desktop">
        <SidebarContainer>
          <Content {...contentProps} />
        </SidebarContainer>
      </div>

      <div className="report-sidebar-mobile">
        {isMobileSidebarMounted && (
          <>
            <button
              type="button"
              className={`report-sidebar-drawer-backdrop cursor-default ${
                isMobileSidebarOpen ? "is-open" : ""
              }`}
              aria-label="Закрыть параметры репорта"
              onClick={closeMobileSidebar}
            />
            <aside
              className={`report-sidebar-drawer ${
                isMobileSidebarOpen ? "is-open" : ""
              }`}
              role="dialog"
              aria-modal="true"
              aria-labelledby="report-sidebar-drawer-title"
            >
              <div className="report-sidebar-drawer-panel">
                <div className="flex items-center justify-between gap-3">
                  <h2
                    id="report-sidebar-drawer-title"
                    className="text-base font-semibold"
                  >
                    Параметры репорта
                  </h2>
                  <button
                    type="button"
                    className="btn btn-ghost btn-sm btn-square"
                    aria-label="Закрыть параметры репорта"
                    onClick={closeMobileSidebar}
                  >
                    <X className="h-4 w-4" />
                  </button>
                </div>

                <div className="flex min-h-0 flex-1 flex-col justify-between gap-4">
                  <Content {...contentProps} />
                </div>
              </div>
            </aside>
          </>
        )}
      </div>
    </>
  );
};

export default ReportSidebar;
