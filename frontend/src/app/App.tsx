import { useEffect } from "react";
import {
  BrowserRouter as Router,
  Outlet,
  Routes,
  Route,
  useRoutes,
  type RouteObject,
} from "react-router";
import { useUnit } from "effector-react";
import { Layout, SelfHostedHeader } from "@/widgets/layout";
import { ReportPage as Report } from "@/pages/Report";
import { SearchPage as Search } from "@/pages/Search";
import { SettingsPage as Settings } from "@/pages/Settings";
import { AnalyticsPage as Analytics } from "@/pages/analytics";
import { LoginPage as Login } from "@/pages/Login";
import { LegacyReportRedirectPage as LegacyReportRedirect } from "@/pages/LegacyReportRedirect";
import { DevNotificationsPage as DevNotifications } from "@/pages/DevNotifications";
import {
  ReportSidebar,
  ReportSidebarHeaderTrigger,
  ReportSidebarProvider,
} from "@/widgets/report-sidebar";
import "@/shared/styles/tailwind.css";
import "./relations"; // Связи между слоями
import { BootstrapStatus } from "@/shared/config";
import { basePath } from "@/shared/config";
import ApiBaseBoot from "./ApiBaseBoot";

import { TeamsPage as Teams } from "@/pages/Teams";
import { CustomLeftSidebar } from "@/widgets/custom-left-sidebar";
import { TeamsSidebar } from "@/widgets/teams-sidebar";
import { AppLayout } from "./layouts/RouteLayouts";

// Bootstrap imports
import { WorkspaceJoinPage as WorkspaceJoin } from "@/pages/SelfHostedWorkspaceJoin";
import {
  TeamSelectPage as TeamSelect,
  SelfHostedEntry,
} from "@/pages/SelfHostedTeamSelect";
import {
  fetchBootstrapFx,
  $bootstrapState,
  startSocketLifecycle,
} from "@/shared/model";
import { RequiredUserSettingsPage as RequiredUserSettings } from "@/pages/SelfHostedRequiredUserSettings";
import { $authUserStore, fetchCurrentUserFx } from "@/entities/user";
import { userNameRequired, mattermostUserIdRequired } from "@/shared/config";
import { NotificationProvider } from "@/app/providers";

// Layout без сайдбара
const LayoutWrapper = () => (
  <Layout>
    <Outlet />
  </Layout>
);

// Layout с сайдбаром для страницы репорта
const LayoutWithSidebarWrapper = () => (
  <ReportSidebarProvider>
    <Layout
      rightSidebar={<ReportSidebar />}
      header={
        <SelfHostedHeader sidebarAction={<ReportSidebarHeaderTrigger />} />
      }
    >
      <Outlet />
    </Layout>
  </ReportSidebarProvider>
);

// Маршруты приложения с bootstrap логикой
const AppRoutesRenderer = () => {
  const [bootstrapState, bootstrapPending, user, userPending] = useUnit([
    $bootstrapState,
    fetchBootstrapFx.pending,
    $authUserStore,
    fetchCurrentUserFx.pending,
  ]);

  const requiresUserSettings = userNameRequired || mattermostUserIdRequired;

  // Ждём загрузки данных
  if (bootstrapPending) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="loading loading-spinner loading-lg"></div>
      </div>
    );
  }

  // Шаг 1: Нет workspace - страница присоединения
  if (bootstrapState.status === BootstrapStatus.NO_WORKSPACE) {
    return <WorkspaceJoin />;
  }

  // Шаг 2: Есть workspace, но нет команды - страница выбора команды
  if (bootstrapState.status === BootstrapStatus.NO_TEAM) {
    return <TeamSelect />;
  }

  // Шаг 3: Проверка обязательных настроек пользователя
  if (requiresUserSettings) {
    if (!user?.id || userPending) {
      return (
        <div className="min-h-screen flex items-center justify-center">
          <div className="loading loading-spinner loading-lg"></div>
        </div>
      );
    }

    const needsName =
      userNameRequired &&
      (!user.name?.trim() || user.name?.startsWith("пользователь"));
    const needsMattermostUserId =
      mattermostUserIdRequired && !user.mattermostUserId;

    if (needsName || needsMattermostUserId) {
      return <RequiredUserSettings />;
    }
  }

  // Шаг 4: Есть команда - роуты с /teams/:teamId/
  const defaultTeamId = bootstrapState.defaultTeamId;

  const routes: RouteObject[] = [
    // Редирект с корня на команду пользователя
    {
      path: "/",
      element: <SelfHostedEntry defaultTeamId={defaultTeamId} />,
    },
    // Legacy ссылки вида /reports/:id
    {
      path: "/reports/:legacyId",
      element: <LegacyReportRedirect />,
    },
    // Основные маршруты внутри команды
    {
      path: "/teams/:teamId",
      element: (
        <AppLayout
          leftSidebar={<CustomLeftSidebar />}
          rightSidebar={<TeamsSidebar />}
        />
      ),
      children: [{ index: true, element: <Teams /> }],
    },
    {
      path: "/teams/:teamId/reports",
      element: <LayoutWithSidebarWrapper />,
      children: [
        { index: true, element: <Report /> },
        { path: ":reportId", element: <Report /> },
      ],
    },
    {
      path: "/teams/:teamId/search",
      element: <LayoutWrapper />,
      children: [{ index: true, element: <Search /> }],
    },
    {
      path: "/teams/:teamId/settings",
      element: <LayoutWrapper />,
      children: [{ index: true, element: <Settings /> }],
    },
    {
      path: "/teams/:teamId/analytics",
      element: <LayoutWrapper />,
      children: [{ index: true, element: <Analytics /> }],
    },
  ];

  return <AppRoutes routes={routes} />;
};

function AppRoutes({ routes }: { routes: RouteObject[] }) {
  const element = useRoutes(routes);
  return element;
}

// Обёртка для основных роутов с bootstrap логикой
const MainRoutes = () => {
  const runFetchBootstrap = useUnit(fetchBootstrapFx);

  useEffect(() => {
    // Загружаем workspaces для bootstrap; после него подтянется текущий
    // пользователь (см. relations.ts).
    runFetchBootstrap();
  }, [runFetchBootstrap]);

  return (
    <>
      <ApiBaseBoot />
      <AppRoutesRenderer />
    </>
  );
};

const App = () => {
  // Возврат на вкладку и восстановление сети поднимают упавший сокет
  useEffect(() => startSocketLifecycle(), []);

  return (
    <NotificationProvider>
      <Router basename={basePath}>
        <Routes>
          {/* Страница логина - доступна без авторизации */}
          <Route path="/login" element={<Login />} />
          <Route path="/dev/notifications" element={<DevNotifications />} />
          {/* Все остальные маршруты */}
          <Route path="/*" element={<MainRoutes />} />
        </Routes>
      </Router>
    </NotificationProvider>
  );
};

export default App;
