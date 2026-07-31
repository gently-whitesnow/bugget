import { request } from "./client";
import type { Result } from "./client";

const TEAM_MEMBERS = "/v1/workspaces/{workspaceId}/teams/{teamId}/members";
const TEAM_MEMBER =
  "/v1/workspaces/{workspaceId}/teams/{teamId}/members/{userId}";
const TEAM_MEMBERS_JOIN =
  "/v1/workspaces/{workspaceId}/teams/{teamId}/members/join";

export type TeamMembersResult = Result<typeof TEAM_MEMBERS, "get">;

// Рабочее пространство в адресах участников контракт объявляет игнорируемым
// сегментом (команда берётся из identity), команда и пользователь — числами.
// Исключение — `userId` в удалении участника, см. комментарий ниже.
export const listTeamMembers = (
  workspaceId: string | number,
  teamId: string | number
) =>
  request(TEAM_MEMBERS, "get", {
    path: { workspaceId: String(workspaceId), teamId: Number(teamId) },
  });

// Контракт объявляет `userId` числом (`int64`), а users-ручки отдают его строкой —
// и приводить её нельзя: значения за `Number.MAX_SAFE_INTEGER` округляются, и
// удаление уезжает на соседнего участника. Сегмент уходит в путь дословно; провод
// от этого не меняется, `buildOperationPath` всё равно подставляет `String(value)`.
// Расходится только тип path-параметра, поэтому сужение точечное и снимается,
// как только контракт объявит `userId` строкой.
export const deleteTeamMember = (
  workspaceId: string | number,
  teamId: string | number,
  userId: string | number
) =>
  request(TEAM_MEMBER, "delete", {
    path: {
      workspaceId: String(workspaceId),
      teamId: Number(teamId),
      userId: String(userId) as unknown as number,
    },
  });

export const leaveTeam = (
  workspaceId: string | number,
  teamId: string | number
) =>
  request(TEAM_MEMBERS, "delete", {
    path: { workspaceId: String(workspaceId), teamId: Number(teamId) },
  });

export const joinTeam = (
  workspaceId: string | number,
  teamId: string | number
) =>
  request(TEAM_MEMBERS_JOIN, "post", {
    path: { workspaceId: String(workspaceId), teamId: Number(teamId) },
  });
