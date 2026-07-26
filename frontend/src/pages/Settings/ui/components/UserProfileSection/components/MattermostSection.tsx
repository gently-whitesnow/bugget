import { isMattermostIdValid, mattermostIdMaxLength } from "@/entities/user";
import { mattermostBotDmUrl } from "@/shared/config";

type Props = {
  mattermostUserId: string | null;
  mattermostIdInput: string;
  isMattermostDisconnecting: boolean;
  isMattermostLinking: boolean;
  onMattermostDisconnect: () => void;
  onMattermostIdInputChange: (value: string) => void;
  onMattermostLink: () => void;
};

export const MattermostSection = ({
  mattermostUserId,
  mattermostIdInput,
  isMattermostDisconnecting,
  isMattermostLinking,
  onMattermostDisconnect,
  onMattermostIdInputChange,
  onMattermostLink,
}: Props) => {
  return (
    <div className="mt-5 pt-5 border-t border-base-300/50">
      <p className="text-sm font-medium text-base-content mb-2">Mattermost</p>
      {mattermostUserId ? (
        <div className="flex items-center gap-3">
          <span className="badge badge-success gap-1">Подключён</span>
          <button
            className="btn btn-outline btn-error btn-sm"
            onClick={onMattermostDisconnect}
            disabled={isMattermostDisconnecting}
          >
            {isMattermostDisconnecting ? (
              <span className="loading loading-spinner loading-xs" />
            ) : (
              "Отключить"
            )}
          </button>
        </div>
      ) : (
        <div className="flex w-full max-w-[min(100%,26.25rem)] flex-col gap-3">
          {mattermostBotDmUrl && (
            <p className="text-sm text-base-content/60">
              Напишите любое сообщение{" "}
              <a
                href={mattermostBotDmUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="link link-primary"
              >
                боту в Mattermost
              </a>
              , он ответит вашим User ID.
            </p>
          )}
          <div className="responsive-inline [--responsive-gap:0.5rem] [--responsive-item-min:11rem]">
            <input
              type="text"
              className="input input-bordered input-sm flex-1 font-mono"
              placeholder="Mattermost User ID"
              maxLength={mattermostIdMaxLength}
              value={mattermostIdInput}
              onChange={(e) => onMattermostIdInputChange(e.target.value)}
              disabled={isMattermostLinking}
            />
            {!isMattermostIdValid(mattermostIdInput) && (
              <p className="text-sm text-error">
                Необходимая длина идентификатора - {mattermostIdMaxLength}{" "}
                символов
              </p>
            )}
            <button
              className="btn btn-primary btn-sm"
              onClick={onMattermostLink}
              disabled={
                !isMattermostIdValid(mattermostIdInput) || isMattermostLinking
              }
            >
              {isMattermostLinking ? (
                <span className="loading loading-spinner loading-xs" />
              ) : (
                "Привязать"
              )}
            </button>
          </div>
        </div>
      )}
    </div>
  );
};
