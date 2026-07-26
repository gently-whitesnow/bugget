import { createContext } from "react";

export type ReportSidebarContextValue = {
  isMobileSidebarOpen: boolean;
  isMobileSidebarMounted: boolean;
  openMobileSidebar: () => void;
  closeMobileSidebar: () => void;
};

export const ReportSidebarContext =
  createContext<ReportSidebarContextValue | null>(null);
