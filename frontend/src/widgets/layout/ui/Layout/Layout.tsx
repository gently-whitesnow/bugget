import React from "react";

import { useHeaderVisibility, LayoutContext } from "@/shared/lib";
import { headerHeight } from "@/shared/config";
import SelfHostedHeader from "../SelfHostedHeader/SelfHostedHeader";

type Props = {
  children: React.ReactNode;
  rightSidebar?: React.ReactNode;
  leftSidebar?: React.ReactNode;
  header?: React.ReactNode;
};

const Layout = ({ children, rightSidebar, leftSidebar, header }: Props) => {
  const { isHidden: isHeaderHidden, scrollRef } = useHeaderVisibility();

  const getGridClass = () => {
    if (leftSidebar && rightSidebar) return "app-layout-grid--left-right";
    if (leftSidebar) return "app-layout-grid--left";
    if (rightSidebar) return "app-layout-grid--right";
    return "app-layout-grid--main";
  };

  return (
    <LayoutContext.Provider
      value={{ isHeaderHidden, scrollContainerRef: scrollRef }}
    >
      <div className="flex flex-col h-full">
        {header ?? <SelfHostedHeader />}

        <div
          ref={scrollRef}
          className="app-layout-scroll flex-1 overflow-y-auto transition-[margin-top] duration-200"
          style={{ marginTop: isHeaderHidden ? `-${headerHeight}` : "0" }}
        >
          <div className={`app-layout-grid ${getGridClass()}`}>
            {leftSidebar && (
              <div className="app-layout-left">{leftSidebar}</div>
            )}
            <main className="app-layout-main app-main">{children}</main>
            {rightSidebar && (
              <div className="app-layout-right">{rightSidebar}</div>
            )}
          </div>
        </div>
      </div>
    </LayoutContext.Provider>
  );
};

export default Layout;
