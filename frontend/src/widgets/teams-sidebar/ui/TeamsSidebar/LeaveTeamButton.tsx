import type { FC } from "react";
import { LogOut } from "lucide-react";
import { useUnit } from "effector-react";
import { useNavigate } from "react-router";
import {
  $isCurrentUserMember,
  $isLeavingTeam,
  $teamContext,
  leaveTeamFx,
} from "../../model";
import {
  fetchBootstrapFx,
  useNotifications,
  notificationMessages,
} from "@/shared/model";

export const LeaveTeamButton: FC = () => {
  const navigate = useNavigate();
  const [teamContext, leaveTeam, fetchBootstrap] = useUnit([
    $teamContext,
    leaveTeamFx,
    fetchBootstrapFx,
  ]);
  const isUserMember = useUnit($isCurrentUserMember);
  const isLeaving = useUnit($isLeavingTeam);
  const { notifyError } = useNotifications();

  const handleLeaveTeam = async () => {
    const confirmed = confirm("Вы уверены, что хотите покинуть команду?");
    if (!confirmed) return;
    if (!teamContext) return;

    try {
      await leaveTeam(teamContext);
      await fetchBootstrap();
      navigate("/", { replace: true });
    } catch (err) {
      console.error("Failed to leave team:", err);
      notifyError(
        "Ошибка при выходе из команды",
        notificationMessages.errorRetry,
        {
          dedupeKey: "sidebar-leave-team-failed",
        }
      );
    }
  };

  if (!isUserMember) {
    return null;
  }

  return (
    <button
      className="btn btn-sm btn-ghost mt-auto justify-start text-error hover:bg-error/10"
      onClick={handleLeaveTeam}
      disabled={isLeaving}
    >
      {isLeaving ? (
        <>
          <span className="loading loading-spinner loading-xs"></span>
          Выход...
        </>
      ) : (
        <>
          <LogOut className="h-4 w-4" />
          Покинуть команду
        </>
      )}
    </button>
  );
};
