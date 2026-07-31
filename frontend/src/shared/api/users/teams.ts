import { getAppContext } from "@/shared/api/instances";
import { request } from "./client";
import type { Result } from "./client";

const TEAMS = "/v1/workspaces/{workspaceId}/teams";
const TEAM = "/v1/workspaces/{workspaceId}/teams/{teamId}";
const TEAMS_AUTOCOMPLETE = "/v1/workspaces/{workspaceId}/teams/autocomplete";

export type AutocompleteTeamsResult = Result<typeof TEAMS_AUTOCOMPLETE, "get">;

// Рабочее пространство в адресах команд контракт объявляет числом: сегмент здесь
// не игнорируемый, ручка по нему и работает. Call-site по-прежнему передаёт
// `string | number` — на проводе значение то же, приведение живёт на границе.
export const createTeam = (workspaceId: string | number, name: string) =>
  request(TEAMS, "post", {
    path: { workspaceId: Number(workspaceId) },
    body: { name },
  });

export const updateTeam = (
  workspaceId: string | number,
  teamId: string | number,
  name: string
) =>
  request(TEAM, "put", {
    path: { workspaceId: Number(workspaceId), teamId: Number(teamId) },
    body: { name },
  });

export const deleteTeam = (
  workspaceId: string | number,
  teamId: string | number
) =>
  request(TEAM, "delete", {
    path: { workspaceId: Number(workspaceId), teamId: Number(teamId) },
  });

/**
 * Подсказки по командам берут рабочее пространство из контекста приложения.
 * Пустой контекст здесь не ошибка, а «подсказывать нечего»: поле фильтра
 * показывается и до того, как контекст доехал.
 */
export const teamsAutocomplete = async (
  searchString: string,
  skip: number = 0,
  take: number = 10
): Promise<AutocompleteTeamsResult> => {
  try {
    const { workspaceId } = getAppContext();
    if (!workspaceId) {
      console.warn("Workspace context is not set for teams autocomplete");
      return { teams: [], total: 0 };
    }

    return await request(TEAMS_AUTOCOMPLETE, "get", {
      path: { workspaceId: Number(workspaceId) },
      query: { searchString, skip, take },
    });
  } catch (error) {
    console.error(error);
    return { teams: [], total: 0 };
  }
};
