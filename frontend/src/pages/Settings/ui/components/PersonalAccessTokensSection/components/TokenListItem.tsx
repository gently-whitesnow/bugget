import type { PersonalAccessToken } from "@/entities/user";
import { formatTokenDate } from "../../../../lib/personalAccessTokens";

type Props = {
  token: PersonalAccessToken;
  isExpired: boolean;
  isOutOfCurrentTeam: boolean;
  isRevoking: boolean;
  onRevoke: (tokenId: string) => void;
};

export const TokenListItem = ({
  token,
  isExpired,
  isOutOfCurrentTeam,
  isRevoking,
  onRevoke,
}: Props) => {
  return (
    <div className="flex items-start justify-between gap-4 px-5 py-3 border-t border-base-300/50">
      <div className="min-w-0 flex flex-col gap-1">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="font-medium text-base-content truncate">
            {token.label}
          </span>
          <code className="font-mono text-xs text-base-content/60">
            {token.tokenPrefix}
          </code>
          {isExpired && <span className="badge badge-warning">Истёк</span>}
          {isOutOfCurrentTeam && (
            <span className="badge badge-ghost">Другая команда</span>
          )}
        </div>
        <div className="text-xs text-base-content/60">
          Выпущен {formatTokenDate(token.createdAt)}
          {token.expiresAt !== null &&
            ` · ${isExpired ? "истёк" : "истекает"} ${formatTokenDate(token.expiresAt)}`}
          {token.lastUsedAt === null
            ? " · не использовался"
            : ` · использован ${formatTokenDate(token.lastUsedAt)}`}
        </div>
      </div>
      <button
        className="btn btn-ghost btn-xs text-error shrink-0"
        onClick={() => onRevoke(token.id)}
        disabled={isRevoking}
      >
        {isRevoking ? (
          <span className="loading loading-spinner loading-xs" />
        ) : (
          "Отозвать"
        )}
      </button>
    </div>
  );
};
