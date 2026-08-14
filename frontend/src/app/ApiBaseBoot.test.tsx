// @vitest-environment jsdom
import { cleanup, render } from "@testing-library/react";
import { fork } from "effector";
import { Provider } from "effector-react";
import { MemoryRouter } from "react-router";
import { afterEach, describe, expect, it } from "vitest";

import { getAppContext, setAppContext } from "@/shared/api";
import { $teamsMember, $workspaces, $workspacesMember } from "@/shared/model";
import ApiBaseBoot from "./ApiBaseBoot";

const timestamp = "2026-08-14T00:00:00Z";
const workspace = {
  id: "7",
  name: "Workspace",
  createdAt: timestamp,
  updatedAt: timestamp,
  teams: [
    {
      id: "29",
      name: "Team",
      createdAt: timestamp,
      updatedAt: timestamp,
    },
  ],
};
const teamMember = {
  teamId: "29",
  userId: "5",
  createdAt: timestamp,
};

const renderBoot = (pathname: string, ready = true) => {
  const scope = fork({
    values: [
      [$workspaces, ready ? [workspace] : []],
      [$teamsMember, ready ? [teamMember] : []],
      [$workspacesMember, []],
    ],
  });

  render(
    <Provider value={scope}>
      <MemoryRouter initialEntries={[pathname]}>
        <ApiBaseBoot />
      </MemoryRouter>
    </Provider>
  );
};

afterEach(() => {
  cleanup();
  setAppContext(null, null);
});

describe("контекст API при старте приложения", () => {
  it("берёт workspace и команду из bootstrap вне командного URL", () => {
    renderBoot("/");

    expect(getAppContext()).toEqual({ workspaceId: "7", teamId: "29" });
  });

  it("сохраняет приоритет команды из URL", () => {
    renderBoot("/teams/47/settings");

    expect(getAppContext()).toEqual({ workspaceId: 1, teamId: 47 });
  });

  it("не задаёт контекст до готовности bootstrap", () => {
    renderBoot("/", false);

    expect(getAppContext()).toEqual({ workspaceId: null, teamId: null });
  });
});
