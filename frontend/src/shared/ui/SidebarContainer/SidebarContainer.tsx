import React from "react";

import { useLayout } from "@/shared/lib";
import { headerHeight } from "@/shared/config";

type Props = {
  children: React.ReactNode;
  side?: "left" | "right";
};

/**
 * Базовый контейнер для сайдбара.
 */
const SidebarContainer = ({ children, side = "right" }: Props) => {
  const { isHeaderHidden } = useLayout();

  const height = isHeaderHidden ? "100vh" : `calc(100vh - ${headerHeight})`;
  const borderClass =
    side === "left" ? "border-r rounded-r-sm" : "border-l rounded-l-sm";

  return (
    <div
      className={`sidebar-container flex min-h-full w-full flex-1 self-stretch ${borderClass} border-base-content/30`}
    >
      <div
        className="sidebar-container-inner sticky top-0 flex w-full flex-col justify-between gap-4 px-6 py-8 transition-[height] duration-200"
        style={{ height }}
      >
        {children}
      </div>
    </div>
  );
};
export default SidebarContainer;
