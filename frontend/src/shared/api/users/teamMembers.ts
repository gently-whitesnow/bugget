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
export const listTeamMembers = (
  workspaceId: string | number,
  teamId: string | number
) =>
  request(TEAM_MEMBERS, "get", {
    path: { workspaceId: String(workspaceId), teamId: Number(teamId) },
  });

export const deleteTeamMember = (
  workspaceId: string | number,
  teamId: string | number,
  userId: string | number
) =>
  request(TEAM_MEMBER, "delete", {
    path: {
      workspaceId: String(workspaceId),
      teamId: Number(teamId),
      userId: Number(userId),
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
