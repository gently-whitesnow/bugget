import { useUnit } from "effector-react";
import { useEffect } from "react";

import { fetchExternalUsersFx, $externalUsersStore } from "../model";
import type { ExternalUserDto } from "../api";

const unknownTester = "Тестер";

export const useExternalUser = (
  workspaceId: string | number | null | undefined,
  participantId: string | null | undefined
): ExternalUserDto | null => {
  const externalUsers = useUnit($externalUsersStore);
  const fetchExternalUsers = useUnit(fetchExternalUsersFx);

  useEffect(() => {
    if (!workspaceId || !participantId) return;
    if (externalUsers[participantId]) return;
    fetchExternalUsers([participantId]);
  }, [workspaceId, participantId, externalUsers, fetchExternalUsers]);

  if (!participantId) return null;
  return externalUsers[participantId] ?? null;
};

export const useExternalUserDisplayName = (
  workspaceId: string | number | null | undefined,
  participantId: string | null | undefined
): string =>
  useExternalUser(workspaceId, participantId)?.displayName ?? unknownTester;
