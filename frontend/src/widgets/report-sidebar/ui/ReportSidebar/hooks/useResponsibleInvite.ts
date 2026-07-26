import { useEffect, useMemo, useRef, useState } from "react";
import { useUnit } from "effector-react";
import { useParams, useSearchParams } from "react-router-dom";

import {
  $participantsUserIdsStore,
  $reportIdStore,
  $responsibleUserIdStore,
  addParticipantSocketEvent,
  changeResponsibleUserIdEvent,
  fetchUsersFx,
} from "@/entities/report";
import { $authUserStore } from "@/entities/user";
import { basePath } from "@/shared/config";
import { copyToClipboard } from "@/shared/lib";

const CopiedStateTimeoutMs = 2000;

type ResponsibleInviteState = {
  isVisible: boolean;
  isCopied: boolean;
  copyLink: () => Promise<void>;
};

export const useResponsibleInvite = (): ResponsibleInviteState => {
  const { teamId: teamIdFromPath, reportId: reportIdFromPath } = useParams<{
    workspaceId?: string;
    teamId?: string;
    reportId?: string;
  }>();
  const [searchParams, setSearchParams] = useSearchParams();
  const [participantsUserIds, loadedReportId, authUser, responsibleUserId] =
    useUnit([
      $participantsUserIdsStore,
      $reportIdStore,
      $authUserStore,
      $responsibleUserIdStore,
    ]);
  const [addParticipant, fetchUsers, changeResponsibleUser] = useUnit([
    addParticipantSocketEvent,
    fetchUsersFx,
    changeResponsibleUserIdEvent,
  ]);
  const [isCopied, setIsCopied] = useState(false);
  const copiedTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const handledAutoAssignRef = useRef<Set<string>>(new Set());

  const queryReportId = searchParams.get("reportId");
  // Workspace в self-hosted всегда один.
  const resolvedWorkspaceId = 1;
  const resolvedTeamId = Number(teamIdFromPath);
  const currentReportId = reportIdFromPath ?? loadedReportId;

  const isVisible =
    participantsUserIds.length === 1 &&
    Boolean(currentReportId) &&
    Number.isFinite(resolvedWorkspaceId) &&
    Number.isFinite(resolvedTeamId);

  const responsibleInviteLink = useMemo(() => {
    if (!isVisible || !currentReportId) return null;

    const normalizedBasePath = basePath ? basePath.replace(/\/$/, "") : "";
    const encodedReportId = encodeURIComponent(currentReportId);
    return `${window.location.origin}${normalizedBasePath}/?workspaceId=${resolvedWorkspaceId}&teamId=${resolvedTeamId}&reportId=${encodedReportId}`;
  }, [currentReportId, isVisible, resolvedTeamId, resolvedWorkspaceId]);

  useEffect(() => {
    return () => {
      if (copiedTimeoutRef.current) {
        clearTimeout(copiedTimeoutRef.current);
      }
    };
  }, []);

  useEffect(() => {
    if (!queryReportId || !loadedReportId) return;
    if (queryReportId !== loadedReportId) return;
    if (reportIdFromPath && reportIdFromPath !== queryReportId) return;
    if (!authUser.id) return;

    const assignKey = `${queryReportId}:${authUser.id}`;
    if (handledAutoAssignRef.current.has(assignKey)) return;
    handledAutoAssignRef.current.add(assignKey);

    // Fallback for invite flow: if socket "participant joined" event is missed
    // during connection bootstrap, keep sidebar state consistent without refresh.
    if (!participantsUserIds.includes(authUser.id)) {
      addParticipant(authUser.id);
    }

    void fetchUsers([authUser.id]).catch((error) => {
      console.error("Failed to fetch invite assignee user:", error);
    });

    if (responsibleUserId !== authUser.id) {
      changeResponsibleUser(authUser.id);
    }

    const nextSearchParams = new URLSearchParams(searchParams);
    nextSearchParams.delete("reportId");
    setSearchParams(nextSearchParams, { replace: true });
  }, [
    authUser.id,
    addParticipant,
    changeResponsibleUser,
    loadedReportId,
    participantsUserIds,
    queryReportId,
    reportIdFromPath,
    fetchUsers,
    responsibleUserId,
    searchParams,
    setSearchParams,
  ]);

  const copyLink = async () => {
    if (!responsibleInviteLink) return;

    try {
      await copyToClipboard(responsibleInviteLink);
      setIsCopied(true);

      if (copiedTimeoutRef.current) {
        clearTimeout(copiedTimeoutRef.current);
      }

      copiedTimeoutRef.current = setTimeout(() => {
        setIsCopied(false);
      }, CopiedStateTimeoutMs);
    } catch (error) {
      console.error("Failed to copy responsible invite link:", error);
      alert("Не удалось скопировать ссылку");
    }
  };

  return {
    isVisible,
    isCopied,
    copyLink,
  };
};
