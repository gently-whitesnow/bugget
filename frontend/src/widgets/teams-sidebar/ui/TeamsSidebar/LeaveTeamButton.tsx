import type { FC } from "react";
import { useUnit } from "effector-react";
import { useNavigate } from "react-router-dom";
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
      className="btn btn-sm btn-ghost text-xs opacity-40 hover:opacity-100 justify-start mt-auto"
      onClick={handleLeaveTeam}
      disabled={isLeaving}
    >
      {isLeaving ? (
        <>
          <span className="loading loading-spinner loading-xs"></span>
          Выход...
        </>
      ) : (
        <>Покинуть команду</>
      )}
    </button>
  );
};
