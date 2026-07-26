// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import {
  Link,
  MemoryRouter,
  Outlet,
  Route,
  Routes,
  useParams,
  useRoutes,
  useSearchParams,
  type RouteObject,
} from "react-router";

const Layout = () => (
  <div>
    <span>layout</span>
    <Outlet />
  </div>
);

const TeamPage = () => <span>team:{useParams().teamId}</span>;
const ReportPage = () => <span>report:{useParams().reportId ?? "index"}</span>;
const SearchPage = () => <span>search:{useSearchParams()[0].get("q")}</span>;

const routes: RouteObject[] = [
  { path: "/", element: <Link to="/teams/7">go</Link> },
  {
    path: "/teams/:teamId",
    element: <Layout />,
    children: [{ index: true, element: <TeamPage /> }],
  },
  {
    path: "/teams/:teamId/reports",
    element: <Layout />,
    children: [
      { index: true, element: <ReportPage /> },
      { path: ":reportId", element: <ReportPage /> },
    ],
  },
  { path: "/teams/:teamId/search", element: <SearchPage /> },
];

const Inner = () => useRoutes(routes);

const App = () => (
  <Routes>
    <Route path="/login" element={<span>login</span>} />
    <Route path="/*" element={<Inner />} />
  </Routes>
);

const renderAt = (path: string) =>
  render(
    <MemoryRouter basename="/app" initialEntries={[`/app${path}`]}>
      <App />
    </MemoryRouter>
  );

describe("роутинг на react-router 8", () => {
  it("отдаёт логин по /login", () => {
    renderAt("/login");
    expect(screen.getByText("login")).toBeDefined();
  });

  it("матчит вложенный layout и index-роут команды", () => {
    renderAt("/teams/7");
    expect(screen.getByText("layout")).toBeDefined();
    expect(screen.getByText(/team:7/)).toBeDefined();
  });

  it("матчит deep link на репорт", () => {
    renderAt("/teams/7/reports/42");
    expect(screen.getByText(/report:42/)).toBeDefined();
  });

  it("читает query через useSearchParams", () => {
    renderAt("/teams/7/search?q=падение");
    expect(screen.getByText(/search:падение/)).toBeDefined();
  });
});
