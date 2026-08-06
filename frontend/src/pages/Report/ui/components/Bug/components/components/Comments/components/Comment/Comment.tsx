import { memo, useState, useMemo, useCallback } from "react";
import { Bot, Link, Pencil, Trash2 } from "lucide-react";
import { useUnit } from "effector-react";
import ActionDropdown, { ActionItem } from "@/shared/ui/ActionDropdown";
import { usePasteFile, getCommentTimeDisplay } from "@/shared/lib";
import { $authUserStore } from "@/entities/user";
import {
  deleteCommentEvent,
  updateCommentEvent,
  createCommentAttachmentFx,
  deleteCommentAttachmentFx,
  renameCommentAttachmentFx,
} from "@/pages/Report/model-comment";
import { Avatar, FilePreview, AutoLinkText, InlineTextEdit } from "@/shared/ui";
import { $usersStore, Attachment } from "@/entities/report";
import {
  getHighlightClasses,
  useUserDisplayName,
} from "@/pages/Report/ui/components/Bug/utils";
import {
  AttachmentTypes,
  commentMaxLength,
  CreatorTypes,
} from "@/shared/config";
import {
  getCommentAnchorHref,
  getCommentElementId,
  useCopyEntityAnchorLink,
} from "@/pages/Report/lib";

type Props = {
  reportId: string;
  bugId: number;
  id: number;
  text: string;
  creatorUserId: string;
  creatorType: CreatorTypes;
  createdAt: string;
  attachments?: Attachment[] | null;
  isHighlighted?: boolean;
};

const Comment = memo((props: Props) => {
  const {
    reportId,
    bugId,
    id,
    text,
    creatorUserId,
    creatorType,
    createdAt,
    attachments,
    isHighlighted = false,
  } = props;

  const updateComment = useUnit(updateCommentEvent);
  const deleteComment = useUnit(deleteCommentEvent);
  const addAttachment = useUnit(createCommentAttachmentFx);
  const removeAttachment = useUnit(deleteCommentAttachmentFx);
  const renameAttachment = useUnit(renameCommentAttachmentFx);
  const currentUser = useUnit($authUserStore);
  const users = useUnit($usersStore);

  const userDisplayName = useUserDisplayName(creatorUserId, creatorType);
  const isMyComment = currentUser?.id === creatorUserId;
  const isSystemComment = creatorType === CreatorTypes.SYSTEM;
  const isAgentComment = creatorType === CreatorTypes.AGENT;
  const avatarUrl = creatorUserId ? users[creatorUserId]?.imageUrl : undefined;

  const [isEditing, setIsEditing] = useState(false);

  const handleUpdate = (newText: string) => {
    const trimmed = newText.trim();
    if (!trimmed) return;
    updateComment({
      reportId,
      bugId,
      commentId: id,
      text: trimmed,
    });
    setIsEditing(false);
  };

  const handleDelete = useCallback(() => {
    deleteComment({ reportId, bugId, commentId: id });
  }, [deleteComment, reportId, bugId, id]);

  const handleUploadAttachment = (file: File) => {
    return addAttachment({
      reportId,
      bugId,
      commentId: id,
      file,
    });
  };

  const handleRemoveAttachment = (attachmentId: number) => {
    removeAttachment({
      reportId,
      bugId,
      commentId: id,
      attachmentId,
    });
  };

  const handleRenameAttachment = (attachmentId: number, fileName: string) => {
    return renameAttachment({
      reportId,
      bugId,
      commentId: id,
      attachmentId,
      fileName,
    });
  };

  const handleCopyCommentLink = useCopyEntityAnchorLink({
    parentId: bugId,
    entityId: id,
    getAnchorHref: getCommentAnchorHref,
    errorLogLabel: "Failed to copy comment link",
  });

  const { handlePaste } = usePasteFile({
    onFileUpload: (file) => {
      return addAttachment({
        reportId,
        bugId,
        commentId: id,
        file,
      });
    },
  });

  const actionItems: ActionItem[] = useMemo(() => {
    const items: ActionItem[] = [
      {
        icon: <Link className="w-4 h-4" />,
        label: "Скопировать ссылку",
        onClick: handleCopyCommentLink,
      },
    ];

    if (isMyComment) {
      items.push(
        {
          icon: <Pencil className="w-4 h-4" />,
          label: "Редактировать",
          onClick: () => setIsEditing(true),
          className: "text-info",
        },
        {
          icon: <Trash2 className="w-4 h-4" />,
          label: "Удалить",
          onClick: handleDelete,
          className: "text-error hover:bg-error/10",
        }
      );
    }

    return items;
  }, [handleCopyCommentLink, handleDelete, isMyComment]);

  return (
    <div
      id={getCommentElementId(bugId, id)}
      className={`flex items-start gap-3 rounded-md p-2 border transition-colors duration-300 ${getHighlightClasses(
        isHighlighted,
        "border-primary ring-2 ring-primary/30 bg-base-200/40"
      )}`}
    >
      {isSystemComment || isAgentComment ? (
        <div className="w-8 h-8 rounded-full bg-base-200 flex items-center justify-center shrink-0">
          <Bot className="w-6 h-6 text-base-content/80" />
        </div>
      ) : (
        <Avatar src={avatarUrl ?? undefined} width={8} />
      )}
      <div className="flex-1 min-w-0">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="text-xs font-medium text-base-content">
              {userDisplayName}
            </span>
            {isAgentComment ? (
              <span className="text-[10px] uppercase tracking-wide text-base-content/50">
                агент
              </span>
            ) : null}
            <span className="text-xs text-base-content/60">
              {getCommentTimeDisplay(createdAt)}
            </span>
          </div>
          <ActionDropdown items={actionItems} />
        </div>

        <div className="relative z-0">
          {isEditing ? (
            <InlineTextEdit
              initialValue={text}
              onSave={handleUpdate}
              onCancel={() => setIsEditing(false)}
              onPaste={handlePaste}
              rows={1}
              placeholder="Введите текст комментария... (Enter для сохранения)"
              autoFocus
              maxLength={commentMaxLength}
            />
          ) : (
            <AutoLinkText
              text={text}
              className="whitespace-pre-wrap break-words text-base-content text-sm"
            />
          )}
        </div>
        <div className="mt-2">
          <FilePreview
            attachments={attachments || []}
            reportId={reportId}
            bugId={bugId}
            attachType={AttachmentTypes.COMMENT}
            onAttachmentUpload={handleUploadAttachment}
            onAttachmentDelete={handleRemoveAttachment}
            onAttachmentRename={handleRenameAttachment}
            commentId={id}
            disabled={!isMyComment}
            currentUserId={currentUser?.id}
          />
        </div>
      </div>
    </div>
  );
});

export default Comment;
