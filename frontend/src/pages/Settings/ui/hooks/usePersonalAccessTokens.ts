import { useCallback, useEffect, useRef, useState } from "react";
import {
  createPersonalAccessToken,
  fetchPersonalAccessTokens,
  revokePersonalAccessToken,
  type PersonalAccessToken,
} from "@/entities/user";
import { getAppContext } from "@/shared/api";
import { notificationMessages, useNotifications } from "@/shared/model";

export const usePersonalAccessTokens = () => {
  const { notifyError } = useNotifications();

  const [tokens, setTokens] = useState<PersonalAccessToken[]>([]);
  const [isTokensLoading, setIsTokensLoading] = useState(true);
  const [tokensError, setTokensError] = useState<string | null>(null);
  const [newTokenLabel, setNewTokenLabel] = useState("");
  const [isTokenCreating, setIsTokenCreating] = useState(false);
  const [revokingTokenIds, setRevokingTokenIds] = useState<ReadonlySet<string>>(
    new Set()
  );
  const [createdTokenValue, setCreatedTokenValue] = useState<string | null>(
    null
  );

  const lastRefreshId = useRef(0);

  /**
   * Ответ не самого свежего запроса игнорируется: отзыв не блокирует кнопки
   * остальных строк, поэтому обновлений списка может лететь несколько сразу, и
   * запоздавший ответ вернул бы в список уже отозванный токен.
   */
  const refreshTokens = useCallback(async () => {
    const refreshId = ++lastRefreshId.current;

    try {
      const loadedTokens = await fetchPersonalAccessTokens();
      if (refreshId !== lastRefreshId.current) return;

      setTokens(loadedTokens);
      setTokensError(null);
    } catch (error) {
      if (refreshId !== lastRefreshId.current) return;

      console.error("Failed to load personal access tokens", error);
      setTokensError("Не удалось загрузить токены");
    } finally {
      if (refreshId === lastRefreshId.current) setIsTokensLoading(false);
    }
  }, []);

  useEffect(() => {
    refreshTokens();
  }, [refreshTokens]);

  const handleTokensRetry = useCallback(() => {
    setIsTokensLoading(true);
    setTokensError(null);
    refreshTokens();
  }, [refreshTokens]);

  const handleTokenCreate = useCallback(async () => {
    const label = newTokenLabel.trim();
    if (!label) return;

    setTokensError(null);
    setIsTokenCreating(true);
    try {
      const created = await createPersonalAccessToken({ label });
      setCreatedTokenValue(created.token);
      setNewTokenLabel("");
      await refreshTokens();
    } catch (error) {
      console.error("Failed to create personal access token", error);
      setTokensError("Не удалось выпустить токен");
      notifyError(
        "Не удалось выпустить токен",
        notificationMessages.errorRetry,
        {
          dedupeKey: "settings-pat-create-failed",
        }
      );
    } finally {
      setIsTokenCreating(false);
    }
  }, [newTokenLabel, notifyError, refreshTokens]);

  const handleTokenRevoke = useCallback(
    async (tokenId: string) => {
      setTokensError(null);
      setRevokingTokenIds((ids) => new Set(ids).add(tokenId));
      try {
        await revokePersonalAccessToken(tokenId);
        await refreshTokens();
      } catch (error) {
        console.error("Failed to revoke personal access token", error);
        setTokensError("Не удалось отозвать токен");
        notifyError(
          "Не удалось отозвать токен",
          notificationMessages.errorRetry,
          {
            dedupeKey: "settings-pat-revoke-failed",
          }
        );
      } finally {
        setRevokingTokenIds((ids) => {
          const rest = new Set(ids);
          rest.delete(tokenId);
          return rest;
        });
      }
    },
    [notifyError, refreshTokens]
  );

  const dismissCreatedToken = useCallback(() => setCreatedTokenValue(null), []);

  return {
    tokens,
    isTokensLoading,
    tokensError,
    newTokenLabel,
    isTokenCreating,
    revokingTokenIds,
    createdTokenValue,
    currentTeamId: getAppContext().teamId,
    setNewTokenLabel,
    handleTokensRetry,
    handleTokenCreate,
    handleTokenRevoke,
    dismissCreatedToken,
  };
};
