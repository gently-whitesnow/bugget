import { useEffect, useRef, useState } from "react";
import { formatDistanceToNow } from "date-fns";
import { ru } from "date-fns/locale";
import { Trash2, Link as LinkIcon } from "lucide-react";

import { useUnit } from "effector-react";
import { BugStatuses, CreatorTypes } from "@/shared/config";
import { AutoResizeTextarea } from "@/shared/ui";
import { BugClientEntity } from "@/entities/report";
import { $authUserStore } from "@/entities/user";
import { $usersStore } from "@/entities/report";
import {
  removeNewBugEvent,
  updateNewBugTitleEvent,
} from "@/pages/Report/model-create-bug";
import { getBugAnchorHref, useCopyAnchorLink } from "../../../../../../lib";

import BugFixRequestButton from "./components/BugFixRequestButton/BugFixRequestButton";
import BugStatusSelect from "./components/BugStatusSelect/BugStatusSelect";

type Props = {
  bug: BugClientEntity;
  onStatusChange?: (status: BugStatuses) => void;
  onTitleChange?: (title: string) => void;
  isFirstBug?: boolean;
};

const BugHeader = ({
  bug,
  onStatusChange,
  onTitleChange,
  isFirstBug = false,
}: Props) => {
  const currentUser = useUnit($authUserStore);
  const users = useUnit($usersStore);
  const [removeLocalBug, updateLocalBugTitle] = useUnit([
    removeNewBugEvent,
    updateNewBugTitleEvent,
  ]);
  const isAgentBug = bug.creatorType === CreatorTypes.AGENT;
  const isAuthorCurrentUser = Boolean(
    currentUser?.id && bug.creatorUserId === currentUser.id
  );
  const authorName = users[bug.creatorUserId]?.name;
  const authorFragment = isAgentBug
    ? authorName
      ? ` агентом ${authorName}`
      : " агентом"
    : isAuthorCurrentUser
      ? " вами"
      : authorName
        ? ` пользователем ${authorName}`
        : "";

  const displayTitle = bug.title ?? `Баг #${bug.id}`;
  const [localTitle, setLocalTitle] = useState(displayTitle);
  const inputRef = useRef<HTMLTextAreaElement | null>(null);

  // Sync localTitle when bug.title changes from a WebSocket patch event
  useEffect(() => {
    if (document.activeElement !== inputRef.current) {
      setLocalTitle(displayTitle);
    }
  }, [displayTitle]);

  const handleDeleteLocalBug = () => {
    removeLocalBug({ clientId: bug.clientId });
  };

  const handleTitleSave = (nextTitle: string) => {
    const trimmed = nextTitle.trim();
    if (!trimmed) {
      setLocalTitle(displayTitle);
      return;
    }

    setLocalTitle(trimmed);

    if (bug.isLocalOnly) {
      updateLocalBugTitle({ clientId: bug.clientId, title: trimmed });
    } else if (onTitleChange) {
      onTitleChange(trimmed);
    }
  };

  const inputClassName =
    "w-full text-lg font-bold min-h-[28px] bg-transparent border-none outline-none focus:outline-none focus:ring-0";

  const handleCopyBugLink = useCopyAnchorLink({
    anchorHref: getBugAnchorHref(bug.id),
    errorLogLabel: "Не удалось скопировать ссылку на баг",
  });

  return (
    <div className="bug-card-actions mb-1">
      <div className="flex items-start gap-2">
        <div className="flex-1 min-w-0">
          <AutoResizeTextarea
            ref={inputRef}
            value={localTitle}
            onChange={setLocalTitle}
            onBlur={handleTitleSave}
            onSave={handleTitleSave}
            maxLength={128}
            rollbackValue={displayTitle}
            className={inputClassName}
            placeholder="Название бага"
          />
        </div>

        <div className="flex items-center gap-2">
          {bug.isLocalOnly
            ? !isFirstBug && (
                <button
                  onClick={handleDeleteLocalBug}
                  className="btn btn-ghost btn-sm text-error hover:bg-error/10"
                  title="Удалить локальный баг"
                >
                  <Trash2 className="w-5 h-5" />
                </button>
              )
            : onStatusChange && (
                <>
                  {bug.status === BugStatuses.FIXED && (
                    <span className="badge badge-warning badge-soft p-3">
                      ожидает проверки
                    </span>
                  )}
                  <BugFixRequestButton bugId={bug.id} status={bug.status} />
                  <BugStatusSelect
                    status={bug.status}
                    onChange={onStatusChange}
                  />
                </>
              )}
        </div>
      </div>

      {!bug.isLocalOnly && bug.createdAt && (
        <div className="mt-0.5 flex items-center gap-2">
          <div className="text-xs text-base-content/55">
            Создан{" "}
            {formatDistanceToNow(new Date(bug.createdAt), {
              addSuffix: true,
              locale: ru,
            })}
            <span>{authorFragment}</span>
          </div>
          {isAgentBug ? (
            <span className="text-[10px] uppercase tracking-wide text-base-content/50">
              агент
            </span>
          ) : null}
          <a
            href={getBugAnchorHref(bug.id)}
            onClick={handleCopyBugLink}
            className="text-base-content/55 hover:text-primary transition-colors"
            title="Скопировать ссылку на баг"
          >
            <LinkIcon className="w-4 h-4" />
          </a>
        </div>
      )}
    </div>
  );
};

export default BugHeader;
