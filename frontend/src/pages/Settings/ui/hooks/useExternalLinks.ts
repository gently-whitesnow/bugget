import { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "react-router";
import { useUnit } from "effector-react";
import {
  $externalLinksStore,
  fetchExternalLinksFx,
  mergeAccountsFx,
  unlinkProviderFx,
} from "@/entities/user";
import { showExternalProviders } from "@/shared/config";
import { notificationMessages, useNotifications } from "@/shared/model";
import { parseApiError } from "@/shared/api";

export const useExternalLinks = () => {
  const { notifyError, notifySuccess } = useNotifications();
  const [searchParams, setSearchParams] = useSearchParams();
  const [externalLinks, isExternalLinksLoading] = useUnit([
    $externalLinksStore,
    fetchExternalLinksFx.pending,
  ]);
  const { fetchExternalLinks, unlinkProvider, mergeAccounts } = useUnit({
    fetchExternalLinks: fetchExternalLinksFx,
    unlinkProvider: unlinkProviderFx,
    mergeAccounts: mergeAccountsFx,
  });
  const [unlinkingProvider, setUnlinkingProvider] = useState<string | null>(
    null
  );
  const [unlinkError, setUnlinkError] = useState<string | null>(null);

  // Merge dialog state
  const [mergeOwnerId, setMergeOwnerId] = useState<string | null>(null);
  const [isMerging, setIsMerging] = useState(false);

  useEffect(() => {
    if (!showExternalProviders) {
      return;
    }

    fetchExternalLinks();
  }, [fetchExternalLinks]);

  // Detect link_error query params after OAuth redirect
  useEffect(() => {
    if (!showExternalProviders) {
      return;
    }

    const linkError = searchParams.get("link_error");
    const ownerId = searchParams.get("owner_id");

    if (linkError === "external_id_taken" && ownerId) {
      setMergeOwnerId(ownerId);
      // Clean up URL params
      const newParams = new URLSearchParams(searchParams);
      newParams.delete("link_error");
      newParams.delete("owner_id");
      setSearchParams(newParams, { replace: true });
    }
  }, [searchParams, setSearchParams]);

  const handleProviderLink = useCallback((provider: string) => {
    if (!showExternalProviders) {
      return;
    }

    const currentPath = window.location.pathname + window.location.search;
    const encodedCurrentPath = encodeURIComponent(currentPath);
    window.location.href = `/api/authorization/v1/${provider}/login?mode=link&next=${encodedCurrentPath}`;
  }, []);

  const handleProviderUnlink = useCallback(
    async (provider: string) => {
      setUnlinkingProvider(provider);
      setUnlinkError(null);
      try {
        await unlinkProvider(provider);
      } catch (error) {
        console.error("Failed to unlink provider", error);
        setUnlinkError("Не удалось отвязать провайдер");
        notifyError(
          "Не удалось отвязать провайдер",
          notificationMessages.errorRetry,
          { dedupeKey: "settings-provider-unlink-failed" }
        );
      } finally {
        setUnlinkingProvider(null);
      }
    },
    [unlinkProvider, notifyError]
  );

  const handleMergeConfirm = useCallback(async () => {
    if (!mergeOwnerId) return;

    setIsMerging(true);
    try {
      await mergeAccounts(mergeOwnerId);
      notifySuccess("Аккаунты успешно объединены");
      setMergeOwnerId(null);
    } catch (error: unknown) {
      console.error("Failed to merge accounts", error);
      const { status, code } = parseApiError(error);
      if (status === 409 && code === "source_owns_workspaces") {
        notifyError(
          "Невозможно объединить: у второго аккаунта есть рабочая область",
          "Войдите во второй аккаунт и удалите рабочую область, затем попробуйте снова.",
          { dedupeKey: "merge-source-owns-workspaces" }
        );
      } else {
        notifyError(
          "Не удалось объединить аккаунты",
          notificationMessages.errorRetry,
          { dedupeKey: "merge-accounts-failed" }
        );
      }
      setMergeOwnerId(null);
    } finally {
      setIsMerging(false);
    }
  }, [mergeOwnerId, mergeAccounts, notifyError, notifySuccess]);

  const handleMergeCancel = useCallback(() => {
    setMergeOwnerId(null);
  }, []);

  return {
    externalLinks,
    isExternalLinksLoading,
    unlinkingProvider,
    unlinkError,
    handleProviderLink,
    handleProviderUnlink,
    showMergeDialog: mergeOwnerId !== null,
    isMerging,
    handleMergeConfirm,
    handleMergeCancel,
  };
};
