import type { FC } from "react";
import { useUnit } from "effector-react";
import { useLocation, useNavigate } from "react-router";

import { buildAuthRedirectUrl } from "@/shared/lib/auth";
import {
  joinTeamFx,
  useNotifications,
  notificationMessages,
} from "@/shared/model";
import {
  $isCurrentUserMember,
  $teamContext,
  $teamSizeLimit,
  $availableSlots,
  fetchTeamMembersFx,
} from "../../model";

const getErrorStatus = (error: unknown): number | null => {
  if (error && typeof error === "object" && "response" in error) {
    const response = (error as { response?: { status?: number } }).response;
    return response?.status ?? null;
  }
  return null;
};

export const TeamJoinSection: FC = () => {
  const location = useLocation();
  const navigate = useNavigate();
  const { notifyError } = useNotifications();

  const [teamContext, isMember, joinPending, teamSizeLimit, availableSlots] =
    useUnit([
      $teamContext,
      $isCurrentUserMember,
      joinTeamFx.pending,
      $teamSizeLimit,
      $availableSlots,
    ]);
  const [joinTeam, fetchTeamMembers] = useUnit([
    joinTeamFx,
    fetchTeamMembersFx,
  ]);

  if (!teamContext || isMember) return null;

  const isLimitKnown = teamSizeLimit > 0;
  const isTeamFull = isLimitKnown && availableSlots <= 0;

  const handleJoin = async () => {
    try {
      await joinTeam({
        workspaceId: teamContext.workspaceId,
        teamId: teamContext.teamId,
      });

      // Refresh sidebar state (members list, user chips, etc.).
      await fetchTeamMembers({
        workspaceId: teamContext.workspaceId,
        teamId: teamContext.teamId,
      });
    } catch (error) {
      const status = getErrorStatus(error);
      if (status === 401) {
        const next = `${location.pathname}${location.search}`;
        const redirectUrl =
          buildAuthRedirectUrl(next) ??
          `/login?next=${encodeURIComponent(next)}`;
        navigate(redirectUrl, { replace: true });
        return;
      }

      console.error("Failed to join team:", error);
      notifyError(
        "Не удалось присоединиться к команде",
        notificationMessages.errorRetry,
        {
          dedupeKey: "sidebar-join-team-failed",
        }
      );
    }
  };

  return (
    <div className="mt-3">
      <button
        onClick={handleJoin}
        disabled={joinPending || isTeamFull}
        className="btn btn-sm btn-primary w-full"
      >
        {joinPending ? (
          <>
            <span className="loading loading-spinner loading-sm"></span>
            Присоединяемся...
          </>
        ) : (
          "Вступить в команду"
        )}
      </button>

      {isTeamFull && (
        <div className="text-xs text-warning mt-2">Достигнут лимит команды</div>
      )}
    </div>
  );
};
