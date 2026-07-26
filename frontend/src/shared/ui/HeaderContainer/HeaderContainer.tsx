import React from "react";
import { headerHeight } from "@/shared/config";

import "./HeaderContainer.css";

type Props = {
  children: React.ReactNode;
  hidden?: boolean;
};

/**
 * Базовый контейнер для хэдера
 */
const HeaderContainer = ({ children, hidden }: Props) => {
  const visibilityClass = hidden ? "-translate-y-full" : "translate-y-0";
  return (
    <header
      style={{ height: headerHeight }}
      className={`
        app-header relative z-[200] bg-base-200 shadow-sm px-4
        flex items-center justify-between gap-2
        transform-gpu transition-transform duration-200
        ${visibilityClass}
      `}
    >
      {children}
    </header>
  );
};

export default HeaderContainer;
