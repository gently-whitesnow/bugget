import { createEffect, sample, combine } from "effector";
import { selfHostedApi } from "@/shared/api";
import { BootstrapStatus } from "@/shared/config";
import {
  $workspaces as $workspacesStore,
  $teamsMember,
  $workspacesMember,
  fetchWorkspacesFx,
} from "./workspaces";

// Состояние пользователя для Self-hosted и SaaS режимов

export const fetchBootstrapFx = fetchWorkspacesFx;

export const joinWorkspaceFx = createEffect(
  async (workspaceId: string | number) => {
    await selfHostedApi.joinWorkspace(workspaceId);
    return workspaceId;
  }
);

export const joinTeamFx = createEffect(
  async ({
    workspaceId,
    teamId,
  }: {
    workspaceId: string | number;
    teamId: string | number;
  }) => {
    await selfHostedApi.joinTeam(workspaceId, teamId);
    return teamId;
  }
);

export const createTeamFx = createEffect(
  async ({
    workspaceId,
    name,
  }: {
    workspaceId: string | number;
    name: string;
  }) => {
    const team = await selfHostedApi.createTeam(workspaceId, name);
    // Автоматически вступаем в созданную команду
    await selfHostedApi.joinTeam(workspaceId, team.id);
    return team.id;
  }
);

export const renameTeamFx = createEffect(
  async ({
    workspaceId,
    teamId,
    name,
  }: {
    workspaceId: string | number;
    teamId: string | number;
    name: string;
  }) => {
    const team = await selfHostedApi.updateTeam(workspaceId, teamId, name);
    return team.id;
  }
);

export const deleteTeamFx = createEffect(
  async ({
    workspaceId,
    teamId,
  }: {
    workspaceId: string | number;
    teamId: string | number;
  }) => {
    await selfHostedApi.deleteTeam(workspaceId, teamId);
    return teamId;
  }
);

export const $bootstrapState = combine(
  $workspacesStore,
  $teamsMember,
  $workspacesMember,
  (workspaces, teamsMember, workspacesMember) => {
    if (workspaces.length === 0) {
      return { status: BootstrapStatus.NO_WORKSPACE as const };
    }

    const workspace = workspaces[0]; // Self-hosted = 1 workspace

    const teamIds = workspace.teams?.map((team) => String(team.id)) ?? [];
    const memberTeams = teamsMember.filter((member) =>
      teamIds.includes(String(member.teamId))
    );
    const workspaceMembers = workspacesMember.filter(
      (member) => String(member.workspaceId) === String(workspace.id)
    );

    if (memberTeams.length === 0) {
      return {
        status: BootstrapStatus.NO_TEAM as const,
        workspace,
        availableTeams: workspace.teams || [],
        workspacesMember: workspaceMembers,
      };
    }

    return {
      status: BootstrapStatus.READY as const,
      workspace,
      memberTeams,
      defaultTeamId: memberTeams[0].teamId,
      workspacesMember: workspaceMembers,
    };
  }
);

// После успешных мутаций перезагружаем данные
sample({
  clock: joinWorkspaceFx.done,
  target: fetchBootstrapFx,
});

sample({
  clock: [joinTeamFx.done, createTeamFx.done],
  target: fetchBootstrapFx,
});

sample({
  clock: [renameTeamFx.done, deleteTeamFx.done],
  target: fetchBootstrapFx,
});
