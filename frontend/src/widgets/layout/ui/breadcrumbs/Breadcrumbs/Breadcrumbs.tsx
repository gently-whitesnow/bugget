import { Link } from "react-router";
import { Fragment, type ReactNode } from "react";
import { ChevronRight, MoreHorizontal } from "lucide-react";
import type { Breadcrumb } from "@/shared/lib/types";

import "./Breadcrumbs.css";

type Props = {
  breadcrumbs: Breadcrumb[];
  icon?: ReactNode;
};

const Breadcrumbs = ({ breadcrumbs, icon }: Props) => {
  if (breadcrumbs.length === 0) return null;

  return (
    <nav className="app-breadcrumbs mx-1 text-sm" aria-label="Хлебные крошки">
      <ol className="app-breadcrumbs-list">
        {breadcrumbs.map((breadcrumb, index) => {
          const isFirst = index === 0;
          const isLast = index === breadcrumbs.length - 1;
          const isMiddle = !isFirst && !isLast;
          const hasHash = breadcrumb.path.includes("#");
          const itemClassName = [
            "app-breadcrumbs-item",
            isFirst ? "app-breadcrumbs-item--first" : "",
            isMiddle ? "app-breadcrumbs-item--middle" : "",
            isLast ? "app-breadcrumbs-item--current" : "",
          ]
            .filter(Boolean)
            .join(" ");

          return (
            <Fragment key={breadcrumb.path}>
              {index === 1 && breadcrumbs.length > 2 && (
                <li className="app-breadcrumbs-item app-breadcrumbs-collapsed">
                  <ChevronRight
                    className="app-breadcrumbs-separator h-4 w-4"
                    aria-hidden="true"
                  />
                  <span
                    className="app-breadcrumbs-current px-1"
                    aria-hidden="true"
                  >
                    <MoreHorizontal className="h-4 w-4" />
                  </span>
                </li>
              )}

              <li className={itemClassName}>
                {index > 0 && (
                  <ChevronRight
                    className="app-breadcrumbs-separator h-4 w-4"
                    aria-hidden="true"
                  />
                )}

                {isLast && !hasHash ? (
                  <span
                    className="app-breadcrumbs-current"
                    aria-current="page"
                    title={breadcrumb.label}
                  >
                    {isFirst && icon}
                    <span className="app-breadcrumbs-label">
                      {breadcrumb.label}
                    </span>
                  </span>
                ) : (
                  <Link
                    to={breadcrumb.path}
                    className={`app-breadcrumbs-link ${isLast ? "font-semibold" : ""}`}
                    title={breadcrumb.label}
                  >
                    {isFirst && icon}
                    <span className="app-breadcrumbs-label">
                      {breadcrumb.label}
                    </span>
                  </Link>
                )}
              </li>
            </Fragment>
          );
        })}
      </ol>
    </nav>
  );
};

export default Breadcrumbs;
