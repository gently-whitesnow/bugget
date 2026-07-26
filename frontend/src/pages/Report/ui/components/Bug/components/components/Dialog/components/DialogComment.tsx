import { memo } from "react";
import { useUnit } from "effector-react";

import { useExternalUser } from "@/entities/beta-test";
import { Attachment } from "@/entities/report";
import { $usersStore } from "@/entities/report";
import { $authUserStore } from "@/entities/user";
import {
  createCommentAttachmentFx,
  deleteCommentAttachmentFx,
  renameCommentAttachmentFx,
} from "@/pages/Report/model-comment";
import { CreatorTypes } from "@/shared/config";
import { getCommentTimeDisplay } from "@/shared/lib";
import { Avatar, AutoLinkText, FilePreview } from "@/shared/ui";

type Props = {
  reportId: string;
  bugId: number;
  id: number;
  workspaceId: string | number | null;
  creatorUserId: string;
  creatorType: number;
  text: string;
  createdAt: string;
  attachments?: Attachment[] | null;
};

const youString = "Вы";
const unknownUserString = "Пользователь";

const DialogComment = memo((props: Props) => {
  const {
    reportId,
    bugId,
    id,
    workspaceId,
    creatorUserId,
    creatorType,
    text,
    createdAt,
    attachments,
  } = props;

  const users = useUnit($usersStore);
  const currentUser = useUnit($authUserStore);
  const addAttachment = useUnit(createCommentAttachmentFx);
  const removeAttachment = useUnit(deleteCommentAttachmentFx);
  const renameAttachment = useUnit(renameCommentAttachmentFx);

  const isTester = creatorType === CreatorTypes.TG_BETA_TESTER;
  const isMyComment = currentUser?.id === creatorUserId;
  const externalUser = useExternalUser(
    isTester ? workspaceId : null,
    isTester ? creatorUserId : null
  );

  const internalName =
    currentUser?.id && creatorUserId === currentUser.id
      ? youString
      : (users[creatorUserId]?.name ?? unknownUserString);

  const displayName = isTester
    ? (externalUser?.displayName ?? "Тестер")
    : internalName;
  const avatarUrl = isTester
    ? externalUser?.imageUrl
    : users[creatorUserId]?.imageUrl;

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

  return (
    <div className="flex items-start gap-3 rounded-md p-2 border border-base-200">
      <Avatar src={avatarUrl ?? undefined} width={8} />
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <span className="text-xs font-medium text-base-content">
            {displayName}
          </span>
          <span className="text-xs text-base-content/60">
            {getCommentTimeDisplay(createdAt)}
          </span>
        </div>
        <AutoLinkText
          text={text}
          className="whitespace-pre-wrap break-words text-base-content text-sm"
        />
        <div className="mt-2">
          <FilePreview
            attachments={attachments || []}
            reportId={reportId}
            bugId={bugId}
            attachType={2}
            commentId={id}
            onAttachmentUpload={handleUploadAttachment}
            onAttachmentDelete={handleRemoveAttachment}
            onAttachmentRename={handleRenameAttachment}
            disabled={!isMyComment}
            currentUserId={currentUser?.id}
          />
        </div>
      </div>
    </div>
  );
});

DialogComment.displayName = "DialogComment";

export default DialogComment;
