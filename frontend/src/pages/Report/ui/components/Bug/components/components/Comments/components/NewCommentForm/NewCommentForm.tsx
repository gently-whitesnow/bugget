import { memo, useState, useCallback } from "react";
import { useUnit } from "effector-react";

import { createWithAttachments } from "@/pages/Report/lib";
import {
  createCommentAttachmentFx,
  createCommentFx,
  deleteCommentEvent,
} from "@/pages/Report/model-comment";
import { $authUserStore } from "@/entities/user";
import { $usersStore } from "@/entities/report";
import { Avatar, ComposerInput } from "@/shared/ui";
import { commentMaxLength, CommentAudiences } from "@/shared/config";

type Props = {
  reportId: string;
  bugId: number;
  disabled?: boolean;
};

const NewCommentForm = memo((props: Props) => {
  const { reportId, bugId, disabled = false } = props;

  const [text, setText] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const createComment = useUnit(createCommentFx);
  const addAttachment = useUnit(createCommentAttachmentFx);
  const deleteComment = useUnit(deleteCommentEvent);
  const currentUser = useUnit($authUserStore);
  const users = useUnit($usersStore);
  const avatarUrl = currentUser?.id
    ? users[currentUser.id]?.imageUrl
    : undefined;

  const handleSubmit = useCallback(
    async (files: File[]) => {
      if (!text.trim() && files.length === 0) return false;

      setIsSubmitting(true);
      const currentText = text;

      try {
        const sent = await createWithAttachments({
          text: currentText,
          files,
          create: (commentText) =>
            createComment({
              reportId,
              bugId,
              text: commentText,
              audience: CommentAudiences.INTERNAL,
            }),
          upload: (commentId, file) =>
            addAttachment({ reportId, bugId, commentId, file }),
          remove: (commentId) => deleteComment({ reportId, bugId, commentId }),
        });

        if (sent) setText("");
        return sent;
      } catch (error) {
        console.error("Ошибка при создании комментария:", error);
        return false;
      } finally {
        setIsSubmitting(false);
      }
    },
    [text, reportId, bugId, createComment, addAttachment, deleteComment]
  );

  return (
    <div className="flex items-start gap-3">
      <Avatar src={avatarUrl ?? undefined} width={8} />
      <div className="flex-1">
        <ComposerInput
          value={text}
          onChange={setText}
          onSend={handleSubmit}
          disabled={disabled}
          isSubmitting={isSubmitting}
          placeholder="Оставьте сообщение..."
          enableAttachments
          maxLength={commentMaxLength}
        />
      </div>
    </div>
  );
});

export default NewCommentForm;
