import { getAppContext } from "@/shared/api";
import { createEffect, createEvent, createStore } from "effector";
import {
  banParticipant,
  closeBetaTest,
  fetchBetaTest,
  fetchExternalUsers,
  fetchParticipants,
  openBetaTest,
  patchBetaTest,
  unbanParticipant,
  type BetaTestDto,
  type CloseBetaTestPayload,
  type ExternalUserDto,
  type OpenBetaTestPayload,
  type ParticipantDto,
  type PatchBetaTestPayload,
} from "./api";

export const resetBetaTest = createEvent();

export const fetchBetaTestFx = createEffect(
  async (workspaceId: string | number) => {
    return await fetchBetaTest(workspaceId);
  }
);

export const openBetaTestFx = createEffect(
  async (params: {
    workspaceId: string | number;
    payload?: OpenBetaTestPayload;
  }) => {
    return await openBetaTest(params.workspaceId, params.payload);
  }
);

export const closeBetaTestFx = createEffect(
  async (params: {
    workspaceId: string | number;
    payload?: CloseBetaTestPayload;
  }) => {
    return await closeBetaTest(params.workspaceId, params.payload);
  }
);

export const patchBetaTestFx = createEffect(
  async (params: {
    workspaceId: string | number;
    payload: PatchBetaTestPayload;
  }) => {
    return await patchBetaTest(params.workspaceId, params.payload);
  }
);

export const $betaTest = createStore<BetaTestDto | null>(null)
  .on(fetchBetaTestFx.doneData, (_, data) => data)
  .on(openBetaTestFx.doneData, (_, data) => data)
  .on(closeBetaTestFx.doneData, (_, data) => data)
  .on(patchBetaTestFx.doneData, (_, data) => data)
  .reset(resetBetaTest);

export const $betaTestLoading = fetchBetaTestFx.pending;

export const fetchParticipantsFx = createEffect(
  async (params: { workspaceId: string | number; withReports?: boolean }) => {
    return await fetchParticipants(params.workspaceId, {
      withReports: params.withReports,
    });
  }
);

export const banParticipantFx = createEffect(
  async (params: { workspaceId: string | number; participantId: string }) => {
    return await banParticipant(params.workspaceId, params.participantId);
  }
);

export const unbanParticipantFx = createEffect(
  async (params: { workspaceId: string | number; participantId: string }) => {
    return await unbanParticipant(params.workspaceId, params.participantId);
  }
);

const replaceParticipant = (
  list: ParticipantDto[],
  updated: ParticipantDto
): ParticipantDto[] =>
  list.map((item) => (item.id === updated.id ? updated : item));

export const $participants = createStore<ParticipantDto[]>([])
  .on(fetchParticipantsFx.doneData, (_, data) => data)
  .on(banParticipantFx.doneData, replaceParticipant)
  .on(unbanParticipantFx.doneData, replaceParticipant)
  .reset(resetBetaTest);

export const $betaTestError = createStore<string | null>(null)
  .on(
    fetchBetaTestFx.fail,
    (_, { error }) => error.message || "Ошибка загрузки"
  )
  .on(fetchBetaTestFx, () => null)
  .reset(resetBetaTest);

export const clearExternalUsersEvent = createEvent<void>();

export const fetchExternalUsersFx = createEffect<string[], ExternalUserDto[]>(
  async (ids) => {
    if (ids.length === 0) return [];
    const { workspaceId } = getAppContext();
    if (!workspaceId) return [];
    return await fetchExternalUsers(workspaceId, ids);
  }
);

export const $externalUsersStore = createStore<Record<string, ExternalUserDto>>(
  {}
)
  .on(fetchExternalUsersFx.doneData, (state, users) => {
    if (users.length === 0) return state;
    const next = { ...state };
    for (const user of users) {
      next[user.id] = user;
    }
    return next;
  })
  .reset(clearExternalUsersEvent);
