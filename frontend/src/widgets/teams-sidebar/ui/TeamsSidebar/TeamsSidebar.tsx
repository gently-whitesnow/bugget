import { useParams } from "react-router";
import { useUnit } from "effector-react";
import { useEffect } from "react";
import { $workspaces } from "@/shared/model";
import {
  setTeamContext,
  clearTeamContext,
  clearCopiedState,
  watchCopyToClipboard,
} from "../../model";
import { TeamInfo } from "./TeamInfo";
import { TeamMembersList } from "./TeamMembersList";
import { TeamJoinSection } from "./TeamJoinSection";
import { TeamInviteSection } from "./TeamInviteSection";
import { LeaveTeamButton } from "./LeaveTeamButton";
import { copyToClipboard as copyToClipboardUtil } from "@/shared/lib/clipboard";
import { SidebarContainer } from "@/shared/ui";

export const TeamsSidebar = () => {
  const { teamId } = useParams<{
    workspaceId: string;
    teamId: string;
  }>();
  const workspaces = useUnit($workspaces);
  const [setTeamContextUnit, clearTeamContextUnit, clearCopiedStateUnit] =
    useUnit([setTeamContext, clearTeamContext, clearCopiedState]);

  const resolvedTeamId = teamId ?? null;
  const currentWorkspace =
    workspaces.find((w) =>
      w.teams?.some((team) => String(team.id) === resolvedTeamId)
    ) ?? workspaces[0];
  const resolvedWorkspaceId = currentWorkspace?.id ?? null;
  const currentTeam = currentWorkspace?.teams?.find(
    (t) => String(t.id) === resolvedTeamId
  );

  // Set team context on mount and when params change
  useEffect(() => {
    if (resolvedWorkspaceId && resolvedTeamId) {
      setTeamContextUnit({
        workspaceId: resolvedWorkspaceId,
        teamId: resolvedTeamId,
      });
    }

    return () => {
      clearTeamContextUnit();
    };
  }, [
    resolvedWorkspaceId,
    resolvedTeamId,
    setTeamContextUnit,
    clearTeamContextUnit,
  ]);

  // Handle clipboard copy
  useEffect(() => {
    const unsubscribe = watchCopyToClipboard(async (text) => {
      try {
        await copyToClipboardUtil(text);
        // Clear copied state after 2 seconds
        setTimeout(() => clearCopiedStateUnit(), 2000);
      } catch (err) {
        console.error("Failed to copy to clipboard:", err);
        alert("Не удалось скопировать ссылку");
      }
    });

    return unsubscribe;
  }, [clearCopiedStateUnit]);

  return (
    <SidebarContainer>
      <div className="flex flex-col h-full">
        <div className="flex flex-col gap-4">
          {/* Team info */}
          <TeamInfo teamName={currentTeam?.name || "Команда"} />

          {/* Team members */}
          <div>
            <div className="text-xs font-semibold text-base-content/70 mb-2">
              Участники команды
            </div>
            <TeamMembersList />
            <TeamJoinSection />
          </div>

          {/* Invite section */}
          <div>
            <TeamInviteSection />
          </div>
        </div>

        {/* Leave team button */}
      </div>
      <LeaveTeamButton />
    </SidebarContainer>
  );
};
