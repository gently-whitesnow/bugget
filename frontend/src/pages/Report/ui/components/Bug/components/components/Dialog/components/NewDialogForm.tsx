import { memo, useState, useCallback } from "react";
import { Send } from "lucide-react";
import { useUnit } from "effector-react";

import {
  createCommentAttachmentFx,
  createCommentFx,
} from "@/pages/Report/model-comment";
import { CommentAudiences, commentMaxLength } from "@/shared/config";
import { ComposerInput } from "@/shared/ui";

type Props = {
  reportId: string;
  bugId: number;
  disabled?: boolean;
  disabledReason?: string;
};

const NewDialogForm = memo((props: Props) => {
  const { reportId, bugId, disabled = false, disabledReason } = props;

  const [text, setText] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const createComment = useUnit(createCommentFx);
  const addAttachment = useUnit(createCommentAttachmentFx);

  const handleSubmit = useCallback(
    async (files: File[]) => {
      if (!text.trim() && files.length === 0) return;

      setIsSubmitting(true);
      const currentText = text;

      try {
        const created = await createComment({
          reportId,
          bugId,
          text: currentText.trim() || "Файл прикреплен",
          audience: CommentAudiences.EXTERNAL,
        });

        if (created?.id && files.length > 0) {
          for (const file of files) {
            await addAttachment({
              reportId,
              bugId,
              commentId: created.id,
              file,
            });
          }
        }

        setText("");
      } catch (error) {
        console.error("Ошибка при отправке сообщения тестеру:", error);
      } finally {
        setIsSubmitting(false);
      }
    },
    [text, reportId, bugId, createComment, addAttachment]
  );

  return (
    <div className="flex flex-col gap-1">
      <div className="flex items-center gap-1 text-xs text-info">
        <Send className="w-3 h-3" />
        <span>Отправится в Telegram тестеру</span>
      </div>
      <ComposerInput
        value={text}
        onChange={setText}
        onSend={handleSubmit}
        disabled={disabled}
        isSubmitting={isSubmitting}
        placeholder={
          disabled && disabledReason
            ? disabledReason
            : "Напишите сообщение тестеру..."
        }
        maxLength={commentMaxLength}
        enableAttachments
      />
    </div>
  );
});

NewDialogForm.displayName = "NewDialogForm";

export default NewDialogForm;
