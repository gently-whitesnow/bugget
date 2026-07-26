import { useCallback, useEffect, useState } from "react";
import { useUnit } from "effector-react";
import {
  $authUserStore,
  fetchCurrentUserFx,
  isMattermostIdValid,
  linkMattermost,
  mattermostIdMaxLength,
  updateCurrentUser,
} from "@/entities/user";
import { $bootstrapState } from "@/shared/model";
import {
  userNameRequired,
  mattermostUserIdRequired,
  mattermostBotDmUrl,
  BootstrapStatus,
} from "@/shared/config";

export const RequiredUserSettings = () => {
  const runFetchCurrentUser = useUnit(fetchCurrentUserFx);
  const user = useUnit($authUserStore);
  const bootstrapState = useUnit($bootstrapState);

  const [name, setName] = useState("");
  const [mattermostId, setMattermostId] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  const currentName = user?.name ?? "";

  useEffect(() => {
    setName(currentName);
  }, [currentName]);

  const needsName =
    userNameRequired &&
    (!user.name?.trim() || user.name?.startsWith("пользователь"));
  const needsMattermostUserId =
    mattermostUserIdRequired && !user?.mattermostUserId;

  const refreshUser = useCallback(async () => {
    if (bootstrapState.status !== BootstrapStatus.READY) return;
    const workspaceId = bootstrapState.workspace.id;
    const teamId = bootstrapState.defaultTeamId;
    await runFetchCurrentUser({ workspaceId, teamId });
  }, [bootstrapState, runFetchCurrentUser]);

  const handleSaveName = useCallback(async () => {
    const trimmed = name.trim();
    if (!trimmed) return;

    setError(null);
    setIsSaving(true);
    try {
      await updateCurrentUser({ name: trimmed });
      await refreshUser();
    } catch {
      setError("Не удалось сохранить имя");
    } finally {
      setIsSaving(false);
    }
  }, [name, refreshUser]);

  const handleLinkMattermost = useCallback(async () => {
    const trimmed = mattermostId.trim();
    if (!trimmed) return;

    setError(null);
    setIsSaving(true);
    try {
      await linkMattermost(trimmed);
      await refreshUser();
    } catch {
      setError("Не удалось привязать Mattermost");
    } finally {
      setIsSaving(false);
    }
  }, [mattermostId, refreshUser]);

  return (
    <div className="min-h-screen flex items-center justify-center bg-base-100">
      <div className="max-w-md w-full mx-auto px-4">
        <div className="text-center mb-8">
          <h1 className="text-3xl font-bold mb-2">Настройка профиля</h1>
          <p className="text-base-content/70">
            Для продолжения работы необходимо заполнить обязательные данные
          </p>
        </div>

        {error && (
          <div className="alert alert-error mb-6">
            <span>{error}</span>
          </div>
        )}

        <div className="card bg-base-200 shadow-xl">
          <div className="card-body gap-5">
            {needsName && (
              <div className="flex flex-col gap-2">
                <label
                  htmlFor="required-user-name"
                  className="text-sm font-medium text-base-content"
                >
                  Смените имя пользователя
                </label>
                <p className="text-sm text-base-content/60">
                  Например: Иванов Иван
                </p>
                <input
                  id="required-user-name"
                  type="text"
                  className="input input-bordered w-full"
                  placeholder="Введите ваше имя"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  disabled={isSaving}
                />
                <button
                  className="btn btn-primary w-full"
                  onClick={handleSaveName}
                  disabled={!name.trim() || isSaving}
                >
                  {isSaving ? (
                    <>
                      <span className="loading loading-spinner loading-sm" />
                      Сохраняем...
                    </>
                  ) : (
                    "Сохранить имя"
                  )}
                </button>
              </div>
            )}

            {needsMattermostUserId && (
              <div className="flex flex-col gap-2">
                <p className="text-sm font-medium text-base-content">
                  Привязка Mattermost
                </p>
                <p className="text-sm text-base-content/60">
                  {mattermostBotDmUrl ? (
                    <>
                      Напишите любое сообщение{" "}
                      <a
                        href={mattermostBotDmUrl}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="link link-primary"
                      >
                        боту в Mattermost
                      </a>
                      , он ответит вашим User ID. Скопируйте и вставьте его
                      ниже.
                    </>
                  ) : (
                    "Введите ваш Mattermost User ID"
                  )}
                </p>
                <input
                  type="text"
                  className="input input-bordered w-full font-mono"
                  placeholder="Mattermost User ID"
                  maxLength={mattermostIdMaxLength}
                  value={mattermostId}
                  onChange={(e) => setMattermostId(e.target.value)}
                  disabled={isSaving}
                />
                {!isMattermostIdValid(mattermostId) && (
                  <p className="text-sm text-error">
                    Необходимая длина идентификатора - {mattermostIdMaxLength}{" "}
                    символов
                  </p>
                )}
                <button
                  className="btn btn-primary w-full"
                  onClick={handleLinkMattermost}
                  disabled={!isMattermostIdValid(mattermostId) || isSaving}
                >
                  {isSaving ? (
                    <>
                      <span className="loading loading-spinner loading-sm" />
                      Привязываем...
                    </>
                  ) : (
                    "Привязать Mattermost"
                  )}
                </button>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
