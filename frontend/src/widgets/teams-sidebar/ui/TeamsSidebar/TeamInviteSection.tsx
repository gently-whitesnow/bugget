import type { FC } from "react";
import { useUnit } from "effector-react";
import { basePath } from "@/shared/config";
import {
  $isCopied,
  $isCurrentUserMember,
  $teamContext,
  copyToClipboard,
} from "../../model";

/**
 * Приглашение в команду: ссылка с workspaceId/teamId, по которой пользователь
 * присоединяется сам. Одноразовые инвайты с TTL были частью SaaS и выпилены.
 */
export const TeamInviteSection: FC = () => {
  const copyToClipboardFn = useUnit(copyToClipboard);
  const teamContext = useUnit($teamContext);
  const isCopied = useUnit($isCopied);
  const isMember = useUnit($isCurrentUserMember);

  if (!teamContext || !isMember) return null;

  const normalizedBasePath = basePath ? basePath.replace(/\/$/, "") : "";
  const inviteLink = `${window.location.origin}${normalizedBasePath}/?workspaceId=${teamContext.workspaceId}&teamId=${teamContext.teamId}`;

  return (
    <button
      onClick={() => copyToClipboardFn(inviteLink)}
      className="btn btn-sm btn-primary w-full py-2"
    >
      {isCopied ? "✓ Скопировано" : "Ссылка приглашение"}
    </button>
  );
};
