import {
  createEffect,
  createEvent,
  createStore,
  sample,
  combine,
} from "effector";
import { usersApi } from "@/shared/api";
import { getUsersByIds } from "@/entities/user";
import { $teamsMember } from "@/shared/model";
import type { TeamMembersResponse } from "@/shared/api";
import type { CurrentUserResponse } from "@/entities/user";

/**
 * Types
 */
type TeamContext = {
  workspaceId: string | number;
  teamId: string | number;
};

/**
 * Effects
 */
export const fetchTeamMembersFx = createEffect<
  TeamContext,
  TeamMembersResponse
>(async ({ workspaceId, teamId }) => {
  return await usersApi.listTeamMembers(workspaceId, teamId);
});

export const fetchMemberDetailsFx = createEffect<
  TeamContext & { userIds: (string | number)[] },
  CurrentUserResponse[]
>(async ({ workspaceId, teamId, userIds }) => {
  if (userIds.length === 0) return [];
  // Форма ответа теперь выведена из контракта целиком: перекладывать её поле за
  // полем незачем — рукописная копия как раз и теряла `mattermostUserId`.
  return await getUsersByIds(workspaceId, teamId, userIds);
});

export const deleteTeamMemberFx = createEffect<
  TeamContext & { userId: string | number },
  void
>(async ({ workspaceId, teamId, userId }) => {
  await usersApi.deleteTeamMember(workspaceId, teamId, userId);
});

export const leaveTeamFx = createEffect<TeamContext, void>(
  async ({ workspaceId, teamId }) => {
    await usersApi.leaveTeam(workspaceId, teamId);
  }
);

/**
 * Events
 */
export const setTeamContext = createEvent<TeamContext>();
export const clearTeamContext = createEvent<void>();
export const deleteMember = createEvent<{
  userId: string | number;
  userName: string;
}>();
export const leaveTeamEvent = createEvent<void>();
export const copyToClipboard = createEvent<string>();
export const clearCopiedState = createEvent<void>();
export const watchCopyToClipboard = (
  listener: (text: string) => void | Promise<void>
) => {
  return copyToClipboard.watch(listener);
};

/**
 * Stores
 */
export const $teamContext = createStore<TeamContext | null>(null)
  .on(setTeamContext, (_, context) => context)
  .reset(clearTeamContext);

export const $teamMembers = createStore<TeamMembersResponse>({
  members: [],
  sizeLimit: 0,
})
  .on(fetchTeamMembersFx.doneData, (_, members) => members)
  .reset(clearTeamContext);

export const $memberDetails = createStore<CurrentUserResponse[]>([])
  .on(fetchMemberDetailsFx.doneData, (_, details) => details)
  .reset(clearTeamContext);

export const $isCopied = createStore<boolean>(false)
  .on(copyToClipboard, () => true)
  .on(clearCopiedState, () => false)
  .reset(clearTeamContext);

export const $isLoading = combine(
  fetchTeamMembersFx.pending,
  fetchMemberDetailsFx.pending,
  (membersLoading, detailsLoading) => membersLoading || detailsLoading
);

export const $deletingUserId = createStore<string | number | null>(null)
  .on(deleteTeamMemberFx, (_, { userId }) => userId)
  .on(deleteTeamMemberFx.finally, () => null);

export const $isLeavingTeam = leaveTeamFx.pending;

// Computed stores
export const $membersCount = $teamMembers.map((data) => data.members.length);
export const $teamSizeLimit = $teamMembers.map((data) => data.sizeLimit);
export const $availableSlots = combine(
  $teamSizeLimit,
  $membersCount,
  (limit, count) => limit - count
);
export const $isCurrentUserMember = combine(
  $teamsMember,
  $teamContext,
  (teamsMember, ctx) => {
    if (!ctx) return false;
    return teamsMember.some((m) => String(m.teamId) === String(ctx.teamId));
  }
);

/**
 * Samples
 */

// Load team data when context is set
sample({
  clock: setTeamContext,
  target: fetchTeamMembersFx,
});

// Load member details after fetching members
sample({
  clock: fetchTeamMembersFx.doneData,
  source: $teamContext,
  filter: (context, members): context is TeamContext =>
    context !== null && members.members.length > 0,
  fn: (context: TeamContext, members: TeamMembersResponse) => ({
    workspaceId: context.workspaceId,
    teamId: context.teamId,
    userIds: members.members.map((m: { userId: string | number }) => m.userId),
  }),
  target: fetchMemberDetailsFx,
});

// Delete member
sample({
  clock: deleteMember,
  source: $teamContext,
  filter: (context): context is TeamContext => context !== null,
  fn: (context: TeamContext, { userId }) => ({
    workspaceId: context.workspaceId,
    teamId: context.teamId,
    userId,
  }),
  target: deleteTeamMemberFx,
});

// Reload members after deletion
sample({
  clock: deleteTeamMemberFx.done,
  source: $teamContext,
  filter: (context): context is TeamContext => context !== null,
  target: fetchTeamMembersFx,
});

// Leave team
sample({
  clock: leaveTeamEvent,
  source: $teamContext,
  filter: (context): context is TeamContext => context !== null,
  target: leaveTeamFx,
});
