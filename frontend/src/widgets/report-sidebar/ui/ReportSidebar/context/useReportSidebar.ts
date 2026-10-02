import { useContext } from "react";

import { ReportSidebarContext } from "./ReportSidebarContext";

export const useReportSidebar = () => {
  const context = useContext(ReportSidebarContext);

  if (!context) {
    throw new Error(
      "useReportSidebar must be used within ReportSidebarProvider"
    );
  }

  return context;
};
