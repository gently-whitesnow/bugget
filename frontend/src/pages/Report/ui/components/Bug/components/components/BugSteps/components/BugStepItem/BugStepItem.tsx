import { useState, DragEvent, useMemo } from "react";
import { GripVertical, Link, Pencil, Trash2 } from "lucide-react";
import { useUnit } from "effector-react";
import ActionDropdown, { ActionItem } from "@/shared/ui/ActionDropdown";

import { AutoLinkText, InlineTextEdit, FilePreview } from "@/shared/ui";
import { usePasteFile } from "@/shared/lib";
import { $authUserStore } from "@/entities/user";
import {
  getBugStepAnchorHref,
  getBugStepElementId,
  useCopyEntityAnchorLink,
} from "@/pages/Report/lib";
import { getHighlightClasses } from "@/pages/Report/ui/components/Bug/utils";

import {
  createBugStepAttachmentFx,
  deleteBugStepAttachmentFx,
  renameBugStepAttachmentFx,
} from "@/pages/Report/model-bug-step";
import { BugStep } from "@/entities/report";
import { AttachmentTypes, bugStepMaxLength } from "@/shared/config";

type Props = {
  step: BugStep;
  index: number;
  reportId: string;
  bugId: number;
  disabled?: boolean;
  isReorderable: boolean;
  isDragging: boolean;
  isHighlighted?: boolean;
  onUpdate: (stepId: number, text: string) => void;
  onDelete: (stepId: number) => void;
  onDragStart: (stepId: number, event: DragEvent) => void;
  onDragOver: (stepId: number, event: DragEvent) => void;
  onDrop: () => void;
};

const BugStepItem = ({
  step,
  index,
  reportId,
  bugId,
  disabled = false,
  isReorderable,
  isDragging,
  isHighlighted = false,
  onUpdate,
  onDelete,
  onDragStart,
  onDragOver,
  onDrop,
}: Props) => {
  const [isEditing, setIsEditing] = useState(false);

  const createAttachment = useUnit(createBugStepAttachmentFx);
  const deleteAttachment = useUnit(deleteBugStepAttachmentFx);
  const renameAttachment = useUnit(renameBugStepAttachmentFx);
  const currentUser = useUnit($authUserStore);

  const { handlePaste } = usePasteFile({
    onFileUpload: (file) => {
      if (reportId && isEditing) {
        return createAttachment({
          reportId,
          bugId,
          stepId: step.id,
          file,
        });
      }
      return Promise.resolve();
    },
  });

  const handleSave = (text: string) => {
    const trimmed = text.trim();
    if (trimmed && trimmed !== step.text) {
      onUpdate(step.id, trimmed);
    }
    setIsEditing(false);
  };

  const handleUploadAttachment = (file: File) => {
    if (reportId) {
      return createAttachment({
        reportId,
        bugId,
        stepId: step.id,
        file,
      });
    }
    return Promise.resolve();
  };

  const handleDeleteAttachment = (attachmentId: number) => {
    if (reportId) {
      deleteAttachment({
        reportId,
        bugId,
        stepId: step.id,
        attachmentId,
      });
    }
  };

  const handleRenameAttachment = (attachmentId: number, fileName: string) => {
    if (!reportId) return Promise.resolve();

    return renameAttachment({
      reportId,
      bugId,
      stepId: step.id,
      attachmentId,
      fileName,
    });
  };

  const handleCopyStepLink = useCopyEntityAnchorLink({
    parentId: bugId,
    entityId: step.id,
    getAnchorHref: getBugStepAnchorHref,
    errorLogLabel: "Не удалось скопировать ссылку на шаг",
  });

  const actionItems: ActionItem[] = useMemo(
    () => [
      {
        icon: <Link className="w-4 h-4" />,
        label: "Скопировать ссылку",
        onClick: handleCopyStepLink,
      },
      {
        icon: <Pencil className="w-4 h-4" />,
        label: "Редактировать",
        onClick: () => setIsEditing(true),
        className: "text-info",
      },
      {
        icon: <Trash2 className="w-4 h-4" />,
        label: "Удалить",
        onClick: () => onDelete(step.id),
        className: "text-error hover:bg-error/10",
      },
    ],
    [handleCopyStepLink, onDelete, step.id]
  );

  return (
    <div
      id={getBugStepElementId(bugId, step.id)}
      style={{ viewTransitionName: `step-${step.id}` } as React.CSSProperties}
      className={`flex items-center gap-3 rounded-md p-2 border transition-colors duration-300 ${getHighlightClasses(
        isHighlighted
      )} ${isDragging ? "opacity-40 bg-base-200" : ""}`}
      onDragOver={(event) => onDragOver(step.id, event)}
      onDrop={onDrop}
    >
      <button
        type="button"
        onClick={handleCopyStepLink}
        className="group w-8 h-8 rounded-full border border-base-300 bg-base-200 flex items-center justify-center text-sm font-medium text-base-content/70 cursor-pointer hover:bg-primary/10 hover:border-primary hover:text-primary transition-colors"
        title="Скопировать ссылку на шаг"
      >
        <span className="group-hover:hidden">{index + 1}</span>
        <Link className="w-4 h-4 hidden group-hover:block" />
      </button>

      <div className="flex-1">
        <div className="flex-1 min-w-0">
          {isEditing ? (
            <InlineTextEdit
              initialValue={step.text}
              onSave={handleSave}
              onCancel={() => setIsEditing(false)}
              onPaste={handlePaste}
              autoFocus
              maxLength={bugStepMaxLength}
            />
          ) : (
            <div className="">
              <AutoLinkText
                text={step.text}
                className="whitespace-pre-wrap break-words text-base-content text-sm leading-none"
              />
            </div>
          )}

          <div className="mt-2">
            <FilePreview
              attachments={step.attachments || []}
              reportId={reportId}
              bugId={bugId}
              stepId={step.id}
              attachType={AttachmentTypes.BUG_STEP}
              onAttachmentUpload={handleUploadAttachment}
              onAttachmentDelete={handleDeleteAttachment}
              onAttachmentRename={handleRenameAttachment}
              disabled={disabled}
              currentUserId={currentUser?.id}
            />
          </div>
        </div>
      </div>

      {!disabled && !isEditing && (
        <div className="flex flex-col gap-1 self-start">
          <ActionDropdown items={actionItems} />
          {isReorderable && (
            <button
              type="button"
              className="btn btn-ghost btn-xs p-1 text-base-content/70 cursor-grab active:cursor-grabbing"
              draggable
              onDragStart={(event) => onDragStart(step.id, event)}
              onDragEnd={onDrop}
              title="Перетащить"
            >
              <GripVertical className="w-4 h-4" />
            </button>
          )}
        </div>
      )}
    </div>
  );
};

export default BugStepItem;
