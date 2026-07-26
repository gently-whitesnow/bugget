import type { FC } from "react";
import { useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { useSelfHostedAutoJoin } from "@/shared/lib/selfHostedAutoJoin";

type Props = {
  defaultTeamId: string | number;
};

export const SelfHostedEntry: FC<Props> = ({ defaultTeamId }) => {
  const navigate = useNavigate();
  const { autoJoinParams, isAutoJoining } = useSelfHostedAutoJoin();

  useEffect(() => {
    if (autoJoinParams || !defaultTeamId) return;
    navigate(`/teams/${defaultTeamId}`, { replace: true });
  }, [autoJoinParams, defaultTeamId, navigate]);

  if (autoJoinParams || isAutoJoining) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="loading loading-spinner loading-lg"></div>
      </div>
    );
  }

  return null;
};
