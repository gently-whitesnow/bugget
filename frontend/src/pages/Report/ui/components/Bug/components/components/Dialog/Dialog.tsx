import { useEffect, useState } from "react";
import { Send } from "lucide-react";
import { useStoreMap } from "effector-react";

import { $commentsByBugId } from "@/pages/Report/model-comment";
import { botApi } from "@/shared/api";
import { CommentAudiences } from "@/shared/config";
import { SectionHeaderChip } from "@/shared/ui";

import DialogComment from "./components/DialogComment";
import NewDialogForm from "./components/NewDialogForm";

type BetaTestState = "open" | "closed" | "unavailable";

type Props = {
  reportId: string;
  bugId: number;
  workspaceId: string | number | null;
  disabled?: boolean;
};

const betaClosedReason = "Бета-тест закрыт — отправка сообщений недоступна";
const betaStateLoadingReason = "Проверяем состояние бета-теста...";
const betaStateUnavailableReason = "Не удалось проверить состояние бета-теста";

const Dialog = ({ reportId, bugId, workspaceId, disabled = false }: Props) => {
  const externalComments = useStoreMap({
    store: $commentsByBugId,
    keys: [bugId],
    fn: (state, [id]) =>
      (state[id] || []).filter((c) => c.audience === CommentAudiences.EXTERNAL),
  });

  const [isExpanded, setIsExpanded] = useState(false);
  const [betaState, setBetaState] = useState<BetaTestState | null>(null);

  useEffect(() => {
    if (!isExpanded) return;
    setBetaState(null);
  }, [isExpanded, workspaceId]);

  useEffect(() => {
    if (!isExpanded || workspaceId == null || betaState !== null) return;

    let cancelled = false;
    botApi
      .get<{ state: BetaTestState }>(`/workspaces/${workspaceId}/beta-test`)
      .then((response) => {
        if (!cancelled) setBetaState(response.data.state);
      })
      .catch((error: { response?: { status?: number } }) => {
        if (cancelled) return;
        setBetaState(error.response?.status === 404 ? "closed" : "unavailable");
      });

    return () => {
      cancelled = true;
    };
  }, [isExpanded, workspaceId, betaState]);

  const isLoadingState =
    isExpanded && workspaceId != null && betaState === null;
  const isClosed = workspaceId == null || betaState === "closed";
  const isUnavailable = betaState === "unavailable";
  const formDisabled = disabled || isLoadingState || isClosed || isUnavailable;

  if (!isExpanded) {
    return (
      <SectionHeaderChip
        count={externalComments.length}
        icon={<Send className="w-3 h-3 text-info" />}
        texts={{
          zero: "Диалог с тестером",
          one: "сообщение тестеру",
          few: "сообщения тестеру",
          many: "сообщений тестеру",
        }}
        onClick={() => setIsExpanded(true)}
        disabled={disabled}
      />
    );
  }

  return (
    <div className="w-full bg-base-100 border border-base-300 rounded-lg p-3">
      <SectionHeaderChip
        count={externalComments.length}
        icon={<Send className="w-3 h-3 text-info" />}
        texts={{
          zero: "Диалог с тестером",
          one: "сообщение тестеру",
          few: "сообщения тестеру",
          many: "сообщений тестеру",
        }}
        onClick={() => setIsExpanded(false)}
        className="mb-2"
        disabled={disabled}
      />

      {externalComments.length === 0 ? (
        <div className="text-sm text-base-content/60 py-2">
          Здесь появится переписка с тестером. Ответы тестеру уходят в Telegram.
        </div>
      ) : (
        <div className="space-y-2 mb-2">
          {externalComments.map((comment) => (
            <DialogComment
              key={comment.id}
              reportId={reportId}
              bugId={bugId}
              id={comment.id}
              workspaceId={workspaceId}
              creatorUserId={comment.creatorUserId}
              creatorType={comment.creatorType}
              text={comment.text}
              createdAt={comment.createdAt}
              attachments={comment.attachments}
            />
          ))}
        </div>
      )}

      <NewDialogForm
        reportId={reportId}
        bugId={bugId}
        disabled={formDisabled}
        disabledReason={
          isLoadingState
            ? betaStateLoadingReason
            : isUnavailable
              ? betaStateUnavailableReason
              : isClosed
                ? betaClosedReason
                : undefined
        }
      />
    </div>
  );
};

export default Dialog;
