import { createEffect, sample, combine } from "effector";
import { selfHostedApi } from "@/shared/api";
import { BootstrapStatus } from "@/shared/config";
import {
  $workspaces as $workspacesStore,
  $teamsMember,
  $workspacesMember,
  fetchWorkspacesFx,
} from "./workspaces";

/**
 * Bootstrap model
 * Универсальная модель для определения состояния пользователя
 * Используется для Self-hosted и SaaS режимов
 */

// Эффект загрузки workspaces
export const fetchBootstrapFx = fetchWorkspacesFx;

// Эффект присоединения к workspace
export const joinWorkspaceFx = createEffect(
  async (workspaceId: string | number) => {
    await selfHostedApi.joinWorkspace(workspaceId);
    return workspaceId;
  }
);

// Эффект присоединения к команде
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

// Эффект создания команды
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

// Эффект переименования команды
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

// Эффект удаления команды
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

// Вычисляемые значения
export const $bootstrapState = combine(
  $workspacesStore,
  $teamsMember,
  $workspacesMember,
  (workspaces, teamsMember, workspacesMember) => {
    // Пустой массив - пользователь не в workspace
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
      // Не состоит ни в одной команде
      return {
        status: BootstrapStatus.NO_TEAM as const,
        workspace,
        availableTeams: workspace.teams || [],
        workspacesMember: workspaceMembers,
      };
    }

    // Состоит в команде(ах)
    return {
      status: BootstrapStatus.READY as const,
      workspace,
      memberTeams,
      defaultTeamId: memberTeams[0].teamId,
      workspacesMember: workspaceMembers,
    };
  }
);

// После успешного join workspace - перезагружаем данные
sample({
  clock: joinWorkspaceFx.done,
  target: fetchBootstrapFx,
});

// После успешного join/create team - перезагружаем данные
sample({
  clock: [joinTeamFx.done, createTeamFx.done],
  target: fetchBootstrapFx,
});

// После успешного rename/delete team - перезагружаем данные
sample({
  clock: [renameTeamFx.done, deleteTeamFx.done],
  target: fetchBootstrapFx,
});
