// @vitest-environment jsdom
import { cleanup, render, waitFor } from "@testing-library/react";
import { fork } from "effector";
import { Provider } from "effector-react";
import { MemoryRouter, Route, Routes } from "react-router";
import { afterEach, beforeEach, expect, it } from "vitest";
import type { AxiosAdapter, InternalAxiosRequestConfig } from "axios";

import { appApi, setAppContext } from "@/shared/api";
import { LegacyReportRedirectPage } from "@/pages/LegacyReportRedirect";
import { $teamsMember, $workspaces, $workspacesMember } from "@/shared/model";
import ApiBaseBoot from "./ApiBaseBoot";

/**
 * `/reports/:legacyId` живёт вне `/teams/:teamId` и больше не задаёт контекст сам:
 * адрес запроса должен собираться из bootstrap-контекста `ApiBaseBoot`.
 */

const timestamp = "2026-08-14T00:00:00Z";
const workspace = {
  id: "1",
  name: "Workspace",
  createdAt: timestamp,
  updatedAt: timestamp,
  teams: [
    { id: "29", name: "Team", createdAt: timestamp, updatedAt: timestamp },
  ],
};
const teamMember = { teamId: "29", userId: "5", createdAt: timestamp };

let captured: InternalAxiosRequestConfig | null = null;
let originalAdapter: AxiosAdapter | undefined;

beforeEach(() => {
  captured = null;
  originalAdapter = appApi.defaults.adapter as AxiosAdapter | undefined;
  appApi.defaults.adapter = (async (config) => {
    captured = config;
    return {
      data: { teamId: "29", teamReportId: "team-42" },
      status: 200,
      statusText: "OK",
      headers: { "content-type": "application/json" },
      config,
    };
  }) as AxiosAdapter;
});

afterEach(() => {
  appApi.defaults.adapter = originalAdapter;
  cleanup();
  setAppContext(null, null);
});

it("legacy-редирект запрашивает репорт по контекстному адресу", async () => {
  const scope = fork({
    values: [
      [$workspaces, [workspace]],
      [$teamsMember, [teamMember]],
      [$workspacesMember, []],
    ],
  });

  render(
    <Provider value={scope}>
      <MemoryRouter initialEntries={["/reports/42"]}>
        <ApiBaseBoot />
        <Routes>
          <Route
            path="/reports/:legacyId"
            element={<LegacyReportRedirectPage />}
          />
        </Routes>
      </MemoryRouter>
    </Provider>
  );

  await waitFor(() => expect(captured).not.toBeNull());
  expect(captured?.url).toBe(
    "/api/app/workspaces/1/teams/29/v2/reports/legacy/42"
  );
});
