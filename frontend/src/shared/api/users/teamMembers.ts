import { request } from "./client";
import type { Result } from "./client";

const TEAM_MEMBERS = "/v1/workspaces/{workspaceId}/teams/{teamId}/members";
const TEAM_MEMBER =
  "/v1/workspaces/{workspaceId}/teams/{teamId}/members/{userId}";
const TEAM_MEMBERS_JOIN =
  "/v1/workspaces/{workspaceId}/teams/{teamId}/members/join";

export type TeamMembersResult = Result<typeof TEAM_MEMBERS, "get">;

// Рабочее пространство в адресах участников контракт объявляет игнорируемым
// сегментом (команда берётся из identity), команда — числом, пользователь —
// строкой канона `Int64String`.
export const listTeamMembers = (
  workspaceId: string | number,
  teamId: string | number
) =>
  request(TEAM_MEMBERS, "get", {
    path: { workspaceId: String(workspaceId), teamId: Number(teamId) },
  });

// `userId` уходит в адрес дословно: контракт объявляет его строкой канона
// `Int64String`, и приводить сегмент к числу нельзя — значения за
// `Number.MAX_SAFE_INTEGER` округляются, и удаление уезжает на соседнего участника.
export const deleteTeamMember = (
  workspaceId: string | number,
  teamId: string | number,
  userId: string | number
) =>
  request(TEAM_MEMBER, "delete", {
    path: {
      workspaceId: String(workspaceId),
      teamId: Number(teamId),
      userId: String(userId),
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
