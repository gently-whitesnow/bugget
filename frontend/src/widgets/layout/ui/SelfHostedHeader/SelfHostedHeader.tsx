import { useEffect, useMemo, useRef } from "react";
import type { ReactNode } from "react";
import { useLocation, useNavigate, useParams } from "react-router";
import { useUnit } from "effector-react";
import { BarChart3, Keyboard, Search, Settings } from "lucide-react";

import { $breadcrumbs, setBreadcrumbs } from "../../model";
import { $workspaces } from "@/shared/model";
import { useLayout } from "@/shared/lib";
import {
  appHotkeys,
  ariaHotkey,
  formatHotkey,
  useHotkey,
} from "@/shared/lib/keyboard";
import { Avatar, HeaderContainer } from "@/shared/ui";
import { CreateReportButton } from "@/features/create-report";
import { Breadcrumbs } from "../breadcrumbs";
import HeaderActionsMenu from "../components/HeaderActionsMenu";
import HotkeysDialog from "../components/HotkeysDialog";

type Props = {
  sidebarAction?: ReactNode;
};

const SelfHostedHeader = ({ sidebarAction }: Props) => {
  const { isHeaderHidden } = useLayout();
  const breadcrumbs = useUnit($breadcrumbs);
  const workspaces = useUnit($workspaces);
  const updateBreadcrumbs = useUnit(setBreadcrumbs);
  const { reportId, teamId } = useParams();
  const navigate = useNavigate();
  const location = useLocation();

  // Базовый путь для текущей команды
  const teamBasePath = teamId ? `/teams/${teamId}` : "";

  const teamName = useMemo(() => {
    if (!teamId) return null;
    for (const ws of workspaces) {
      const team = ws.teams?.find((t) => String(t.id) === teamId);
      if (team) return team.name;
    }
    return null;
  }, [workspaces, teamId]);

  useEffect(() => {
    const basePath = teamBasePath || "/";
    const crumbs = [
      {
        label: "Дашборд",
        path: basePath,
      },
      {
        label: teamName || "Команда",
        path: `${basePath}#team`,
      },
    ];

    if (location.pathname.includes("/reports")) {
      if (!reportId) {
        crumbs.push({
          label: "Новый репорт",
          path: `${teamBasePath}/reports`,
        });
      } else {
        crumbs.push({
          label: `Репорт #${reportId}`,
          path: `${teamBasePath}/reports/${reportId}`,
        });
      }
    }

    if (location.pathname.includes("/search")) {
      crumbs.push({
        label: "Поиск",
        path: `${teamBasePath}/search`,
      });
    }

    if (location.pathname.includes("/settings")) {
      crumbs.push({
        label: "Настройки",
        path: `${teamBasePath}/settings`,
      });
    }

    updateBreadcrumbs(crumbs);
  }, [reportId, location.pathname, teamBasePath, teamName, updateBreadcrumbs]);

  // Проверяем, находимся ли на странице репортов или поиска
  const isOnReportsPage = location.pathname.includes("/reports");
  const isOnSearchPage = location.pathname.includes("/search");
  const isOnAnalyticsPage = location.pathname.endsWith("/analytics");
  const isOnHomePage =
    location.pathname === teamBasePath ||
    location.pathname === `${teamBasePath}/`;
  const showAnalytics = Boolean(teamId) && !isOnAnalyticsPage;
  const hotkeysDialogRef = useRef<HTMLDialogElement>(null);
  const openHotkeys = () => {
    const dialog = hotkeysDialogRef.current;
    if (dialog && !dialog.open) dialog.showModal();
  };
  const openSearch = () => navigate(`${teamBasePath}/search`);

  useHotkey([appHotkeys.search, appHotkeys.searchSlash], openSearch, {
    // На странице поиска те же клавиши ставят фокус в строку — это делает сама страница.
    enabled: Boolean(teamId) && !isOnSearchPage,
  });
  useHotkey(appHotkeys.help, openHotkeys, {
    inEditable: true,
  });
  useHotkey(appHotkeys.helpQuestion, openHotkeys);

  return (
    <HeaderContainer hidden={isHeaderHidden}>
      <div className="flex min-w-0 flex-1 items-center">
        <Avatar />
        <Breadcrumbs breadcrumbs={breadcrumbs} />
      </div>
      {sidebarAction && (
        <div className="header-sidebar-action">{sidebarAction}</div>
      )}
      <div className="header-actions-inline">
        {isOnHomePage && (
          <button
            className="btn bg-base-100 mr-2"
            onClick={() => navigate(`${teamBasePath}/settings?tab=team`)}
          >
            <Settings className="w-4 h-4" />
          </button>
        )}

        {showAnalytics && (
          <button
            className="btn bg-base-100 mr-2"
            onClick={() => navigate(`${teamBasePath}/analytics`)}
            aria-label="Аналитика"
          >
            <BarChart3 className="w-4 h-4" />
          </button>
        )}

        {!isOnSearchPage && (
          <div
            className="tooltip tooltip-bottom mr-2"
            data-tip={`Поиск ${formatHotkey(appHotkeys.search)}`}
          >
            <button
              className="btn bg-base-100"
              onClick={openSearch}
              aria-label="Поиск"
              aria-keyshortcuts={`${ariaHotkey(appHotkeys.search)} /`}
            >
              <Search className="w-4 h-4" />
            </button>
          </div>
        )}

        <div
          className="tooltip tooltip-bottom mr-2"
          data-tip={`Горячие клавиши ${formatHotkey(appHotkeys.help)}`}
        >
          <button
            className="btn bg-base-100"
            onClick={openHotkeys}
            aria-label="Горячие клавиши"
            aria-keyshortcuts={ariaHotkey(appHotkeys.help)}
          >
            <Keyboard className="w-4 h-4" />
          </button>
        </div>

        {(!isOnReportsPage || reportId) && (
          <CreateReportButton className="btn btn-primary" />
        )}
      </div>

      <HeaderActionsMenu>
        {isOnHomePage && (
          <button
            type="button"
            className="btn w-full justify-start bg-base-100"
            onClick={() => navigate(`${teamBasePath}/settings?tab=team`)}
          >
            <Settings className="h-4 w-4" />
            Настройки
          </button>
        )}

        {showAnalytics && (
          <button
            type="button"
            className="btn w-full justify-start bg-base-100"
            onClick={() => navigate(`${teamBasePath}/analytics`)}
          >
            <BarChart3 className="h-4 w-4" />
            Аналитика
          </button>
        )}

        {!isOnSearchPage && (
          <button
            type="button"
            className="btn w-full justify-start bg-base-100"
            onClick={openSearch}
          >
            <Search className="h-4 w-4" />
            Поиск
          </button>
        )}

        {(!isOnReportsPage || reportId) && (
          <CreateReportButton className="btn btn-primary w-full justify-start" />
        )}
      </HeaderActionsMenu>

      <HotkeysDialog ref={hotkeysDialogRef} />
    </HeaderContainer>
  );
};

export default SelfHostedHeader;
