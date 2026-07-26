import {
  createEffect,
  createEvent,
  createStore,
  sample,
  combine,
} from "effector";
import {
  listTeamMembers,
  deleteTeamMember as deleteTeamMemberApi,
  leaveTeam as leaveTeamApi,
  getTeamInvite,
  createTeamInvite as createTeamInviteApi,
  regenerateTeamInvite as regenerateTeamInviteApi,
  deleteTeamInvite as deleteTeamInviteApi,
} from "@/shared/api";
import { $authUserStore, getUsersByIds, isAdmin } from "@/entities/user";
import { $teamsMember } from "@/shared/model";
import type {
  TeamCreateInviteRequest,
  TeamInviteResponse,
  TeamMembersResponse,
} from "@/shared/api";
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
  return await listTeamMembers(workspaceId, teamId);
});

export const fetchMemberDetailsFx = createEffect<
  TeamContext & { userIds: (string | number)[] },
  CurrentUserResponse[]
>(async ({ workspaceId, teamId, userIds }) => {
  if (userIds.length === 0) return [];
  const users = await getUsersByIds(workspaceId, teamId, userIds);
  return users.map((user) => ({
    id: String(user.id),
    name: user.name,
    imageUrl: user.imageUrl || null,
    workspaceRole: user.workspaceRole,
  }));
});

export const fetchTeamInviteFx = createEffect<
  TeamContext,
  TeamInviteResponse | null
>(async ({ workspaceId, teamId }) => {
  return await getTeamInvite(workspaceId, teamId);
});

export const createTeamInviteFx = createEffect<
  TeamContext,
  TeamCreateInviteRequest
>(async ({ workspaceId, teamId }) => {
  return await createTeamInviteApi(workspaceId, teamId);
});

export const regenerateTeamInviteFx = createEffect<
  TeamContext & { inviteId: string | number },
  TeamCreateInviteRequest
>(async ({ workspaceId, teamId, inviteId }) => {
  return await regenerateTeamInviteApi(workspaceId, teamId, inviteId);
});

export const deleteTeamInviteFx = createEffect<
  TeamContext & { inviteId: string | number },
  void
>(async ({ workspaceId, teamId, inviteId }) => {
  await deleteTeamInviteApi(workspaceId, teamId, inviteId);
});

export const deleteTeamMemberFx = createEffect<
  TeamContext & { userId: string | number },
  void
>(async ({ workspaceId, teamId, userId }) => {
  await deleteTeamMemberApi(workspaceId, teamId, userId);
});

export const leaveTeamFx = createEffect<TeamContext, void>(
  async ({ workspaceId, teamId }) => {
    await leaveTeamApi(workspaceId, teamId);
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
export const createInvite = createEvent<void>();
export const regenerateInvite = createEvent<string | number>();
export const deleteInvite = createEvent<string | number>();
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

export const $teamInvite = createStore<TeamInviteResponse | null>(null)
  .on(fetchTeamInviteFx.doneData, (_, invite) => invite)
  .on(createTeamInviteFx.doneData, (_, invite) => ({
    id: invite.id,
    createdAt: invite.createdAt,
    expiresAt: invite.expiresAt,
  }))
  .on(regenerateTeamInviteFx.doneData, (_, invite) => ({
    id: invite.id,
    createdAt: invite.createdAt,
    expiresAt: invite.expiresAt,
  }))
  .on(deleteTeamInviteFx.done, () => null)
  .reset(clearTeamContext);

export const $isCopied = createStore<boolean>(false)
  .on(copyToClipboard, () => true)
  .on(clearCopiedState, () => false)
  .reset(clearTeamContext);

export const $isLoading = combine(
  fetchTeamMembersFx.pending,
  fetchMemberDetailsFx.pending,
  fetchTeamInviteFx.pending,
  (membersLoading, detailsLoading, inviteLoading) =>
    membersLoading || detailsLoading || inviteLoading
);

export const $deletingUserId = createStore<string | number | null>(null)
  .on(deleteTeamMemberFx, (_, { userId }) => userId)
  .on(deleteTeamMemberFx.finally, () => null);

export const $isLeavingTeam = leaveTeamFx.pending;
export const $isCreatingInvite = createTeamInviteFx.pending;
export const $isRegeneratingInvite = regenerateTeamInviteFx.pending;

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

sample({
  clock: setTeamContext,
  source: $authUserStore,
  filter: (user) => isAdmin(user),
  fn: (_, ctx) => ctx,
  target: fetchTeamInviteFx,
});

// If user info loads after the team context is set (common on first load),
// fetch invite lazily once we know the user is an admin.
sample({
  clock: $authUserStore.updates,
  source: $teamContext,
  filter: (ctx, user) => ctx !== null && isAdmin(user),
  fn: (ctx) => ctx as TeamContext,
  target: fetchTeamInviteFx,
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

// Create invite
sample({
  clock: createInvite,
  source: $teamContext,
  filter: (context): context is TeamContext => context !== null,
  target: createTeamInviteFx,
});

// Copy invite link to clipboard after creation
sample({
  clock: createTeamInviteFx.doneData,
  fn: (invite) => invite.inviteLink,
  target: copyToClipboard,
});

// Regenerate invite
sample({
  clock: regenerateInvite,
  source: $teamContext,
  filter: (context, inviteId): context is TeamContext =>
    context !== null && Boolean(inviteId),
  fn: (context: TeamContext, inviteId) => ({
    workspaceId: context.workspaceId,
    teamId: context.teamId,
    inviteId,
  }),
  target: regenerateTeamInviteFx,
});

// Copy invite link to clipboard after regeneration
sample({
  clock: regenerateTeamInviteFx.doneData,
  fn: (invite) => invite.inviteLink,
  target: copyToClipboard,
});

// Delete invite
sample({
  clock: deleteInvite,
  source: $teamContext,
  filter: (context, inviteId): context is TeamContext =>
    context !== null && Boolean(inviteId),
  fn: (context: TeamContext, inviteId) => ({
    workspaceId: context.workspaceId,
    teamId: context.teamId,
    inviteId,
  }),
  target: deleteTeamInviteFx,
});
