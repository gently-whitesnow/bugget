import { useCallback, useEffect, useState, type ChangeEvent } from "react";
import { useUnit } from "effector-react";
import {
  $authUserStore,
  deleteCurrentUserAvatar,
  disconnectMattermost,
  fetchCurrentUserFx,
  linkMattermost,
  updateCurrentUser,
  uploadCurrentUserAvatar,
} from "@/entities/user";
import { getAppContext } from "@/shared/api";
import { notificationMessages, useNotifications } from "@/shared/model";

const maxAvatarSizeBytes = 200 * 1024;

export const useUserProfile = () => {
  const { notifyError } = useNotifications();
  const [user, fetchCurrentUser] = useUnit([
    $authUserStore,
    fetchCurrentUserFx,
  ]);

  const [profileName, setProfileName] = useState("");
  const [profileError, setProfileError] = useState<string | null>(null);
  const [isProfileUpdating, setIsProfileUpdating] = useState(false);
  const [isMattermostDisconnecting, setIsMattermostDisconnecting] =
    useState(false);
  const [mattermostIdInput, setMattermostIdInput] = useState("");
  const [isMattermostLinking, setIsMattermostLinking] = useState(false);

  const currentUserName = user?.name ?? "";
  const currentUserAvatar = user?.imageUrl ?? null;
  const userInitial = (currentUserName.trim().charAt(0) || "?").toUpperCase();
  const hasProfileNameChanges = profileName.trim() !== currentUserName.trim();

  const refreshCurrentUser = useCallback(async () => {
    const { workspaceId, teamId } = getAppContext();
    if (!workspaceId || !teamId) return;
    await fetchCurrentUser({ workspaceId, teamId });
  }, [fetchCurrentUser]);

  useEffect(() => {
    if (!user?.id) {
      refreshCurrentUser();
    }
  }, [user?.id, refreshCurrentUser]);

  useEffect(() => {
    setProfileName(currentUserName);
  }, [currentUserName]);

  const handleProfileNameSave = useCallback(async () => {
    const trimmedName = profileName.trim();
    if (!trimmedName) return;

    setProfileError(null);
    setIsProfileUpdating(true);
    try {
      await updateCurrentUser({ name: trimmedName });
      await refreshCurrentUser();
    } catch (error) {
      console.error("Failed to update user profile", error);
      setProfileError("Не удалось сохранить имя пользователя");
      notifyError(
        "Не удалось сохранить профиль",
        notificationMessages.errorRetry,
        { dedupeKey: "settings-profile-update-failed" }
      );
    } finally {
      setIsProfileUpdating(false);
    }
  }, [notifyError, profileName, refreshCurrentUser]);

  const handleAvatarUpload = useCallback(
    async (event: ChangeEvent<HTMLInputElement>) => {
      const file = event.target.files?.[0];
      event.target.value = "";
      if (!file) return;

      if (file.size > maxAvatarSizeBytes) {
        setProfileError("Максимальный размер аватарки — 200 KB");
        return;
      }

      setProfileError(null);
      setIsProfileUpdating(true);
      try {
        await uploadCurrentUserAvatar(file);
        await refreshCurrentUser();
      } catch (error) {
        console.error("Failed to upload avatar", error);
        setProfileError("Не удалось загрузить аватар");
        notifyError(
          "Не удалось загрузить аватар",
          notificationMessages.errorRetry,
          { dedupeKey: "settings-avatar-upload-failed" }
        );
      } finally {
        setIsProfileUpdating(false);
      }
    },
    [notifyError, refreshCurrentUser]
  );

  const handleAvatarDelete = useCallback(async () => {
    if (!currentUserAvatar) return;

    setProfileError(null);
    setIsProfileUpdating(true);
    try {
      await deleteCurrentUserAvatar();
      await refreshCurrentUser();
    } catch (error) {
      console.error("Failed to delete avatar", error);
      setProfileError("Не удалось удалить аватар");
      notifyError(
        "Не удалось удалить аватар",
        notificationMessages.errorRetry,
        { dedupeKey: "settings-avatar-delete-failed" }
      );
    } finally {
      setIsProfileUpdating(false);
    }
  }, [currentUserAvatar, notifyError, refreshCurrentUser]);

  const handleMattermostDisconnect = useCallback(async () => {
    setIsMattermostDisconnecting(true);
    try {
      await disconnectMattermost();
      await refreshCurrentUser();
    } catch (error) {
      console.error("Failed to disconnect Mattermost", error);
      setProfileError("Не удалось отключить Mattermost");
      notifyError(
        "Не удалось отключить Mattermost",
        notificationMessages.errorRetry,
        { dedupeKey: "settings-mattermost-disconnect-failed" }
      );
    } finally {
      setIsMattermostDisconnecting(false);
    }
  }, [notifyError, refreshCurrentUser]);

  const handleMattermostLink = useCallback(async () => {
    const trimmed = mattermostIdInput.trim();
    if (!trimmed) return;

    setIsMattermostLinking(true);
    setProfileError(null);
    try {
      await linkMattermost(trimmed);
      await refreshCurrentUser();
      setMattermostIdInput("");
    } catch (error) {
      console.error("Failed to link Mattermost", error);
      setProfileError("Не удалось привязать Mattermost");
      notifyError(
        "Не удалось привязать Mattermost",
        notificationMessages.errorRetry,
        { dedupeKey: "settings-mattermost-link-failed" }
      );
    } finally {
      setIsMattermostLinking(false);
    }
  }, [mattermostIdInput, notifyError, refreshCurrentUser]);

  return {
    user,
    currentUserName,
    currentUserAvatar,
    userInitial,
    profileName,
    profileError,
    isProfileUpdating,
    hasProfileNameChanges,
    mattermostIdInput,
    isMattermostDisconnecting,
    isMattermostLinking,
    setProfileName,
    setMattermostIdInput,
    handleProfileNameSave,
    handleAvatarUpload,
    handleAvatarDelete,
    handleMattermostDisconnect,
    handleMattermostLink,
  };
};
