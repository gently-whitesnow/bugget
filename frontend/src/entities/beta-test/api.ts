import type { AxiosError } from "axios";
import { botApi, isExternalUrl, withCacheKey } from "@/shared/api";

export type BetaTestState = "open" | "closed";

export type BetaTestDto = {
  workspaceId: string;
  state: BetaTestState;
  hash: string | null;
  wishes: string | null;
  requestSteps: boolean;
  name: string | null;
};

export type PatchBetaTestPayload = {
  wishes: string | null;
  requestSteps: boolean;
  name?: string | null;
};

export type CloseBetaTestPayload = {
  workspaceName?: string;
};

export type OpenBetaTestPayload = {
  workspaceName?: string;
};

export const fetchBetaTest = async (
  workspaceId: string | number
): Promise<BetaTestDto | null> => {
  try {
    const response = await botApi.get<BetaTestDto>(
      `/workspaces/${workspaceId}/beta-test`
    );
    return response.data;
  } catch (err) {
    const axiosError = err as AxiosError;
    if (axiosError.response?.status === 404) return null;
    throw err;
  }
};

export const openBetaTest = async (
  workspaceId: string | number,
  payload: OpenBetaTestPayload = {}
): Promise<BetaTestDto> => {
  const response = await botApi.post<BetaTestDto>(
    `/workspaces/${workspaceId}/beta-test/open`,
    payload
  );
  return response.data;
};

export const closeBetaTest = async (
  workspaceId: string | number,
  payload: CloseBetaTestPayload = {}
): Promise<BetaTestDto> => {
  const response = await botApi.post<BetaTestDto>(
    `/workspaces/${workspaceId}/beta-test/close`,
    payload
  );
  return response.data;
};

export const patchBetaTest = async (
  workspaceId: string | number,
  payload: PatchBetaTestPayload
): Promise<BetaTestDto> => {
  const response = await botApi.patch<BetaTestDto>(
    `/workspaces/${workspaceId}/beta-test`,
    payload
  );
  return response.data;
};

export type ParticipantDto = {
  id: string;
  displayName: string;
  joinedAt: string;
  reportsCount: number;
  isBanned: boolean;
  imageUrl: string | null;
};

const resolveExternalAvatarUrl = (
  workspaceId: string | number,
  participantId: string,
  imageUrl: string | null | undefined
): string | null => {
  if (!imageUrl) return null;
  if (isExternalUrl(imageUrl)) return imageUrl;

  return withCacheKey(
    `/api/bot/v1/bot-api/workspaces/${workspaceId}/external-users/${participantId}/avatar/content`,
    imageUrl
  );
};

const resolveParticipantDto = (
  workspaceId: string | number,
  participant: ParticipantDto
): ParticipantDto => ({
  ...participant,
  imageUrl: resolveExternalAvatarUrl(
    workspaceId,
    participant.id,
    participant.imageUrl
  ),
});

export const fetchParticipants = async (
  workspaceId: string | number,
  options: { withReports?: boolean } = {}
): Promise<ParticipantDto[]> => {
  const response = await botApi.get<ParticipantDto[]>(
    `/workspaces/${workspaceId}/participants`,
    { params: options.withReports ? { withReports: true } : undefined }
  );
  return response.data.map((participant) =>
    resolveParticipantDto(workspaceId, participant)
  );
};

export const banParticipant = async (
  workspaceId: string | number,
  participantId: string
): Promise<ParticipantDto> => {
  const response = await botApi.post<ParticipantDto>(
    `/workspaces/${workspaceId}/participants/${participantId}/ban`
  );
  return resolveParticipantDto(workspaceId, response.data);
};

export const unbanParticipant = async (
  workspaceId: string | number,
  participantId: string
): Promise<ParticipantDto> => {
  const response = await botApi.post<ParticipantDto>(
    `/workspaces/${workspaceId}/participants/${participantId}/unban`
  );
  return resolveParticipantDto(workspaceId, response.data);
};

export type ExternalUserDto = {
  id: string;
  displayName: string;
  tgUsername?: string;
  imageUrl: string | null;
};

export const fetchExternalUsers = async (
  workspaceId: string | number,
  ids: string[]
): Promise<ExternalUserDto[]> => {
  if (ids.length === 0) return [];
  const response = await botApi.get<ExternalUserDto[]>(
    `/workspaces/${workspaceId}/external-users`,
    { params: { ids: ids.join(",") } }
  );
  return response.data.map((user) => ({
    ...user,
    imageUrl: resolveExternalAvatarUrl(workspaceId, user.id, user.imageUrl),
  }));
};
