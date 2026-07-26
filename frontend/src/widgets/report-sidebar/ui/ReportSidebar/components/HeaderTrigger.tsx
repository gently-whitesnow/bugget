import { SlidersHorizontal } from "lucide-react";

import { useReportSidebar } from "../useReportSidebar";

const HeaderTrigger = () => {
  const { isMobileSidebarOpen, openMobileSidebar } = useReportSidebar();

  return (
    <button
      type="button"
      className="report-sidebar-header-trigger btn btn-primary btn-sm btn-square shadow-sm"
      aria-label="Открыть параметры репорта"
      aria-haspopup="dialog"
      aria-expanded={isMobileSidebarOpen}
      onClick={openMobileSidebar}
    >
      <SlidersHorizontal className="h-4 w-4" />
    </button>
  );
};

export default HeaderTrigger;
