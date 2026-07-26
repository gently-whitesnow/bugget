import { useState, useRef, useEffect, useCallback } from "react";
import { MessageCircle } from "lucide-react";
import { useStoreMap } from "effector-react";

import { $commentsByBugId } from "@/pages/Report/model-comment";
import {
  commentHashPattern,
  getCommentElementId,
  useScrollToNestedHashHighlight,
} from "@/pages/Report/lib";
import { useLayout } from "@/shared/lib";
import { CommentAudiences } from "@/shared/config";
import { SectionHeaderChip } from "@/shared/ui";
import Comment from "./components/Comment/Comment";
import NewCommentForm from "./components/NewCommentForm/NewCommentForm";

type Props = {
  reportId: string;
  bugId: number;
  disabled?: boolean;
  resolved?: boolean;
};

const Comments = ({
  reportId,
  bugId,
  disabled = false,
  resolved = false,
}: Props) => {
  const { scrollContainerRef } = useLayout();
  const comments = useStoreMap({
    store: $commentsByBugId,
    keys: [bugId],
    fn: (state, [id]) =>
      (state[id] || []).filter((c) => c.audience === CommentAudiences.INTERNAL),
  });

  const [isExpanded, setIsExpanded] = useState(false);
  const [highlightedCommentId, setHighlightedCommentId] = useState<
    number | null
  >(null);
  const initialized = useRef(false);

  useEffect(() => {
    if (!initialized.current && comments.length > 0 && !resolved) {
      setIsExpanded(true);
      initialized.current = true;
    }
  }, [comments.length, resolved]);
  const getCommentId = useCallback(
    (comment: (typeof comments)[number]) => comment.id,
    []
  );

  useScrollToNestedHashHighlight({
    parentId: bugId,
    items: comments,
    getItemId: getCommentId,
    hashPattern: commentHashPattern,
    getElementId: getCommentElementId,
    setIsExpanded,
    setHighlightedId: setHighlightedCommentId,
    scrollContainerRef,
  });

  if (!isExpanded) {
    return (
      <SectionHeaderChip
        count={comments.length}
        icon={<MessageCircle className="w-3 h-3 text-info" />}
        texts={{
          zero: "Оставить комментарий",
          one: "комментарий",
          few: "комментария",
          many: "комментариев",
        }}
        onClick={() => setIsExpanded(true)}
        disabled={disabled}
      />
    );
  }

  return (
    <div className="w-full bg-base-100 border border-base-300 rounded-lg p-3">
      <SectionHeaderChip
        count={comments.length}
        icon={<MessageCircle className="w-3 h-3 text-info" />}
        texts={{
          zero: "Комментарии",
          one: "комментарий",
          few: "комментария",
          many: "комментариев",
        }}
        onClick={() => setIsExpanded(false)}
        className="mb-2"
        disabled={disabled}
      />

      {!!comments.length && (
        <div className="space-y-2 mb-2">
          {comments.map((comment) => (
            <Comment
              key={comment.id}
              reportId={reportId}
              bugId={bugId}
              id={comment.id}
              text={comment.text}
              creatorUserId={comment.creatorUserId}
              creatorType={comment.creatorType}
              createdAt={comment.createdAt}
              attachments={comment.attachments}
              isHighlighted={highlightedCommentId === comment.id}
            />
          ))}
        </div>
      )}

      <NewCommentForm reportId={reportId} bugId={bugId} disabled={disabled} />
    </div>
  );
};

export default Comments;
