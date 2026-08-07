import { useCallback } from "react";
import {
  isTokenExpired,
  isTokenOutOfCurrentTeam,
} from "../../../lib/personalAccessTokens";
import { usePersonalAccessTokens } from "../../hooks/usePersonalAccessTokens";
import { CreatedTokenDialog } from "./components/CreatedTokenDialog";
import { TokenListItem } from "./components/TokenListItem";

export const PersonalAccessTokensSection = () => {
  const {
    tokens,
    isTokensLoading,
    tokensError,
    newTokenLabel,
    isTokenCreating,
    revokingTokenIds,
    createdTokenValue,
    currentTeamId,
    setNewTokenLabel,
    handleTokensRetry,
    handleTokenCreate,
    handleTokenRevoke,
    dismissCreatedToken,
  } = usePersonalAccessTokens();

  const confirmRevoke = useCallback(
    (tokenId: string) => {
      const confirmed = confirm(
        "Отозвать токен? Отзыв необратим, доступ пропадёт сразу."
      );
      if (confirmed) handleTokenRevoke(tokenId);
    },
    [handleTokenRevoke]
  );

  const now = Date.now();

  return (
    <div className="bg-base-100 rounded-xl border border-base-300/50 overflow-hidden w-full">
      <div className="px-5 py-4 bg-base-200/50 border-b border-base-300/50">
        <h3 className="font-semibold text-base-content">Токены доступа</h3>
        <p className="mt-1 text-sm text-base-content/60">
          Для доступа к API без браузера. Токен наследует ваши права в команде,
          на которую выпущен.
        </p>
      </div>

      <div className="px-5 py-4 flex flex-col sm:flex-row gap-2">
        <input
          type="text"
          className="input input-bordered input-sm flex-1"
          placeholder="Название, например mcp"
          value={newTokenLabel}
          maxLength={128}
          onChange={(event) => setNewTokenLabel(event.target.value)}
        />
        <button
          className="btn btn-primary btn-sm"
          onClick={handleTokenCreate}
          disabled={isTokenCreating || newTokenLabel.trim() === ""}
        >
          {isTokenCreating ? (
            <span className="loading loading-spinner loading-xs" />
          ) : (
            "Выпустить токен"
          )}
        </button>
      </div>

      {tokensError && (
        <div className="alert alert-error mx-5 mb-4 py-2">
          <span className="text-sm">{tokensError}</span>
          <button className="btn btn-ghost btn-xs" onClick={handleTokensRetry}>
            Попробовать снова
          </button>
        </div>
      )}

      {isTokensLoading && (
        <div className="px-5 py-6 text-center">
          <span className="loading loading-spinner loading-sm text-primary" />
        </div>
      )}

      {!isTokensLoading && tokensError === null && tokens.length === 0 && (
        <div className="px-5 py-6 text-center text-sm text-base-content/60">
          Токенов пока нет
        </div>
      )}

      {tokens.map((token) => (
        <TokenListItem
          key={token.id}
          token={token}
          isExpired={isTokenExpired(token, now)}
          isOutOfCurrentTeam={isTokenOutOfCurrentTeam(token, currentTeamId)}
          isRevoking={revokingTokenIds.has(token.id)}
          onRevoke={confirmRevoke}
        />
      ))}

      {createdTokenValue !== null && (
        <CreatedTokenDialog
          token={createdTokenValue}
          onClose={dismissCreatedToken}
        />
      )}
    </div>
  );
};
