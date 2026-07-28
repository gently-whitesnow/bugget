/**
 * Контракты Users API
 * Используются как в SaaS, так и в Self-Hosted режимах
 */

export type Team = {
  name: string | null;
  id: string | null;
};

export type TeamsAutocompleteResponse = {
  teams: Team[];
  total: number;
};

export type UserResponse = {
  id: string;
  name: string;
  imageUrl?: string | null;
};

export type TeamMemberResponse = {
  teamId: string;
  userId: string;
  createdAt: string;
};

export type WorkspaceMemberResponse = {
  workspaceId: string;
  userId: string;
  role: string;
  createdAt: string;
};

export type TeamMembersResponse = {
  members: TeamMemberResponse[];
  sizeLimit: number;
};

export type CreateTeamRequest = {
  name: string;
};

export type TeamResponse = {
  id: string;
  workspaceId: string;
  name: string;
  createdAt: string;
  updatedAt: string;
  members?: TeamMemberResponse[];
};

export type WorkspaceResponse = {
  id: string;
  name: string;
  createdAt: string;
  updatedAt: string;
  teams?: TeamResponse[];
};

export type WorkspacesContextResponse = {
  workspaces: WorkspaceResponse[];
  teamsMember?: TeamMemberResponse[];
  workspacesMember?: WorkspaceMemberResponse[];
};

export type CreateWorkspaceRequest = {
  name: string;
};
