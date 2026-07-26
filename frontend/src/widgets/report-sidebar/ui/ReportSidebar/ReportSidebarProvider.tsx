import { ReactNode, useCallback, useEffect, useState } from "react";

import { ReportSidebarContext } from "./ReportSidebarContext";

type Props = {
  children: ReactNode;
};

const mobileSidebarAnimationMs = 180;

const ReportSidebarProvider = ({ children }: Props) => {
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false);
  const [isMobileSidebarMounted, setIsMobileSidebarMounted] = useState(false);

  const openMobileSidebar = useCallback(() => {
    if (isMobileSidebarMounted) {
      setIsMobileSidebarOpen(true);
      return;
    }

    setIsMobileSidebarMounted(true);
  }, [isMobileSidebarMounted]);

  const closeMobileSidebar = useCallback(() => {
    setIsMobileSidebarOpen(false);
  }, []);

  useEffect(() => {
    if (!isMobileSidebarMounted) return;

    const initialBodyOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        closeMobileSidebar();
      }
    };

    document.addEventListener("keydown", handleEscape);
    return () => {
      document.body.style.overflow = initialBodyOverflow;
      document.removeEventListener("keydown", handleEscape);
    };
  }, [closeMobileSidebar, isMobileSidebarMounted]);

  useEffect(() => {
    if (!isMobileSidebarMounted) return;

    const animationFrameId = window.requestAnimationFrame(() => {
      setIsMobileSidebarOpen(true);
    });

    return () => window.cancelAnimationFrame(animationFrameId);
  }, [isMobileSidebarMounted]);

  useEffect(() => {
    if (isMobileSidebarOpen || !isMobileSidebarMounted) return;

    const timeoutId = window.setTimeout(() => {
      setIsMobileSidebarMounted(false);
    }, mobileSidebarAnimationMs);

    return () => window.clearTimeout(timeoutId);
  }, [isMobileSidebarOpen, isMobileSidebarMounted]);

  return (
    <ReportSidebarContext.Provider
      value={{
        isMobileSidebarOpen,
        isMobileSidebarMounted,
        openMobileSidebar,
        closeMobileSidebar,
      }}
    >
      {children}
    </ReportSidebarContext.Provider>
  );
};

export default ReportSidebarProvider;
