import type { ChangeEvent } from "react";
import type { ExternalLink } from "@/entities/user";
import { MergeAccountsDialog } from "./components/MergeAccountsDialog";
import { MattermostSection } from "./components/MattermostSection";

const providerLabels: Record<string, string> = {
  telegram: "Telegram",
  google: "Google",
  yandex: "Яндекс",
  mattermost: "Mattermost",
};

type Props = {
  currentUserAvatar: string | null;
  currentUserName: string;
  userInitial: string;
  profileName: string;
  profileError: string | null;
  isProfileUpdating: boolean;
  hasProfileNameChanges: boolean;
  mattermostUserId: string | null;
  mattermostIdInput: string;
  isMattermostDisconnecting: boolean;
  isMattermostLinking: boolean;
  externalLinks: ExternalLink[];
  isExternalLinksLoading: boolean;
  unlinkingProvider: string | null;
  onAvatarUpload: (event: ChangeEvent<HTMLInputElement>) => void;
  onAvatarDelete: () => void;
  onProfileNameChange: (value: string) => void;
  onProfileNameSave: () => void;
  onMattermostDisconnect: () => void;
  onMattermostIdInputChange: (value: string) => void;
  onMattermostLink: () => void;
  showExternalProviders: boolean;
  externalProviders: readonly string[];
  showInternalProviders: boolean;
  internalProviders: readonly string[];
  onProviderLink: (provider: string) => void;
  onProviderUnlink: (provider: string) => void;
  showMergeDialog: boolean;
  isMerging: boolean;
  onMergeConfirm: () => void;
  onMergeCancel: () => void;
};

export const UserProfileSection = ({
  currentUserAvatar,
  currentUserName,
  userInitial,
  profileName,
  profileError,
  isProfileUpdating,
  hasProfileNameChanges,
  mattermostUserId,
  mattermostIdInput,
  isMattermostDisconnecting,
  isMattermostLinking,
  externalLinks,
  isExternalLinksLoading,
  unlinkingProvider,
  onAvatarUpload,
  onAvatarDelete,
  onProfileNameChange,
  onProfileNameSave,
  onMattermostDisconnect,
  onMattermostIdInputChange,
  onMattermostLink,
  showExternalProviders,
  externalProviders,
  showInternalProviders,
  internalProviders,
  onProviderLink,
  onProviderUnlink,
  showMergeDialog,
  isMerging,
  onMergeConfirm,
  onMergeCancel,
}: Props) => {
  const canUnlink = externalLinks.length > 1;
  return (
    <div className="bg-base-100 rounded-xl border border-base-300/50 overflow-hidden w-full">
      <div className="px-5 py-4 bg-base-200/50 border-b border-base-300/50">
        <h3 className="font-semibold text-base-content">
          Профиль пользователя
        </h3>
      </div>

      <div className="px-5 py-5">
        <div className="flex flex-wrap items-start gap-5">
          <div className="w-24 h-24 rounded-full overflow-hidden border border-base-300/60 bg-base-200 flex-shrink-0 flex items-center justify-center">
            {currentUserAvatar ? (
              <img
                src={currentUserAvatar}
                alt={currentUserName || "Пользователь"}
                className="w-full h-full object-cover"
              />
            ) : (
              <span className="text-2xl font-semibold text-primary">
                {userInitial}
              </span>
            )}
          </div>

          <div className="flex min-w-[min(100%,14rem)] flex-1 basis-72 flex-col gap-3">
            <p className="text-lg font-semibold text-base-content">
              Загрузить новый аватар
            </p>
            <input
              type="file"
              className="file-input file-input-bordered file-input-sm w-full max-w-xs"
              accept="image/jpeg,image/png,image/gif,image/webp"
              onChange={onAvatarUpload}
              disabled={isProfileUpdating}
            />
            <p className="text-sm text-base-content/60">
              Рекомендуемый размер: 192 x 192 пикселей.
              <br />
              Максимальный размер файла: 200 KB.
            </p>
            <button
              className="btn btn-outline btn-error btn-sm w-fit"
              onClick={onAvatarDelete}
              disabled={!currentUserAvatar || isProfileUpdating}
            >
              Удалить аватар
            </button>
          </div>
        </div>

        <div className="mt-5 flex w-full max-w-[min(100%,26.25rem)] flex-col gap-2">
          <label
            htmlFor="profile-user-name"
            className="text-sm font-medium text-base-content"
          >
            Имя пользователя
          </label>
          <div className="flex flex-wrap gap-2">
            <input
              id="profile-user-name"
              type="text"
              className="input input-bordered flex-1"
              value={profileName}
              onChange={(event) => onProfileNameChange(event.target.value)}
              disabled={isProfileUpdating}
            />
            <button
              className="btn btn-primary btn-sm"
              onClick={onProfileNameSave}
              disabled={
                !hasProfileNameChanges ||
                profileName.trim().length === 0 ||
                isProfileUpdating
              }
            >
              {isProfileUpdating ? (
                <span className="loading loading-spinner loading-xs" />
              ) : (
                "Сохранить имя"
              )}
            </button>
          </div>
        </div>

        {profileError && (
          <div className="alert alert-error mt-4 py-2">
            <span className="text-sm">{profileError}</span>
          </div>
        )}

        {showInternalProviders && internalProviders.includes("mattermost") && (
          <MattermostSection
            mattermostUserId={mattermostUserId}
            mattermostIdInput={mattermostIdInput}
            isMattermostDisconnecting={isMattermostDisconnecting}
            isMattermostLinking={isMattermostLinking}
            onMattermostDisconnect={onMattermostDisconnect}
            onMattermostIdInputChange={onMattermostIdInputChange}
            onMattermostLink={onMattermostLink}
          />
        )}

        {showExternalProviders && (
          <div className="mt-5 pt-5 border-t border-base-300/50">
            <p className="text-sm font-medium text-base-content mb-3">
              Привязанные аккаунты
            </p>
            {isExternalLinksLoading ? (
              <span className="loading loading-spinner loading-sm" />
            ) : (
              <div className="flex flex-col gap-2">
                {externalProviders.map((provider) => {
                  const link = externalLinks.find(
                    (l) => l.provider === provider
                  );
                  const label = providerLabels[provider] ?? provider;
                  const isUnlinking = unlinkingProvider === provider;

                  return (
                    <div
                      key={provider}
                      className="flex items-center gap-3 py-1"
                    >
                      <span className="text-sm w-20 font-medium">{label}</span>
                      {link ? (
                        <>
                          <span className="text-sm text-base-content/70 flex-1 truncate">
                            {link.email ?? link.externalId}
                          </span>
                          <button
                            className="btn btn-outline btn-error btn-xs"
                            onClick={() => onProviderUnlink(provider)}
                            disabled={!canUnlink || isUnlinking}
                            title={
                              !canUnlink
                                ? "Нельзя отвязать единственный способ входа"
                                : undefined
                            }
                          >
                            {isUnlinking ? (
                              <span className="loading loading-spinner loading-xs" />
                            ) : (
                              "Отвязать"
                            )}
                          </button>
                        </>
                      ) : (
                        <>
                          <span className="text-sm text-base-content/40 flex-1">
                            не привязан
                          </span>
                          <button
                            className="btn btn-outline btn-primary btn-xs"
                            onClick={() => onProviderLink(provider)}
                          >
                            Привязать
                          </button>
                        </>
                      )}
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        )}
      </div>

      {showMergeDialog && (
        <MergeAccountsDialog
          isMerging={isMerging}
          onConfirm={onMergeConfirm}
          onCancel={onMergeCancel}
        />
      )}
    </div>
  );
};
