import { forwardRef, useCallback, useRef, useState } from "react";
import { Attachment } from "@/entities/report";

import { useUnit } from "effector-react";
import { $authUserStore } from "@/entities/user";

import { AttachmentChip, FilePreview } from "@/shared/ui";
import type { AttachmentTypes } from "@/shared/config";
import { createCurlAttachmentFile, getClipboardFiles } from "@/shared/lib";
import { PendingAttachment } from "@/shared/ui";
import Title from "./components/Title/Title";
import ResultTextarea from "./components/ResultTextarea/ResultTextarea";

type Props = {
  reportId: string | null;
  bugId: number;
  title: string;
  value: string;
  colorType: "success" | "error";
  attachments: Attachment[];
  attachType: AttachmentTypes;
  autoFocus: boolean;
  onBlur: (value: string) => void;
  onAttachmentUpload: (file: File) => void | Promise<unknown>;
  onAttachmentDelete: (attachmentId: number) => void;
  onAttachmentRename?: (
    attachmentId: number,
    fileName: string
  ) => void | Promise<unknown>;
  onInput: (value: string) => void;
  disabled?: boolean;
};

const getFileForUpload = (attachment: PendingAttachment) => {
  if (attachment.name === attachment.file.name) return attachment.file;

  return new File([attachment.file], attachment.name, {
    type: attachment.file.type,
    lastModified: attachment.file.lastModified,
  });
};

const Result = forwardRef<HTMLDivElement, Props>(
  (
    {
      title,
      value,
      onBlur,
      colorType,
      autoFocus = false,
      attachments = [],
      reportId,
      bugId,
      attachType,
      onAttachmentUpload,
      onAttachmentDelete,
      onAttachmentRename,
      onInput,
      disabled = false,
    },
    ref
  ) => {
    const currentUser = useUnit($authUserStore);
    const nextAttachmentIdRef = useRef(0);
    const [pendingAttachments, setPendingAttachments] = useState<
      PendingAttachment[]
    >([]);

    const createPendingAttachment = useCallback(
      (file: File, kind: PendingAttachment["kind"] = "file") => ({
        id: nextAttachmentIdRef.current++,
        file,
        name: file.name,
        kind,
      }),
      []
    );

    const addPendingCurlAttachment = useCallback(
      (file: File) => {
        if (disabled) return;

        const nextAttachment = createPendingAttachment(file, "curl");
        setPendingAttachments((prev) => [...prev, nextAttachment]);
      },
      [createPendingAttachment, disabled]
    );

    const uploadPendingAttachments = useCallback(() => {
      if (disabled || pendingAttachments.length === 0) return;

      pendingAttachments
        .map(getFileForUpload)
        .forEach((file) => onAttachmentUpload(file));
      setPendingAttachments([]);
    }, [disabled, onAttachmentUpload, pendingAttachments]);

    const handleResultBlur = useCallback(
      (event: React.FocusEvent<HTMLDivElement>) => {
        if (event.currentTarget.contains(event.relatedTarget)) return;

        uploadPendingAttachments();
      },
      [uploadPendingAttachments]
    );

    const handleAttachmentUpload = useCallback(
      (file: File) => {
        onAttachmentUpload(file);
      },
      [onAttachmentUpload]
    );

    const handleRemovePendingAttachment = (index: number) => {
      setPendingAttachments((prev) => prev.filter((_, i) => i !== index));
    };

    const handleRenamePendingAttachment = (id: number, name: string) => {
      setPendingAttachments((prev) =>
        prev.map((attachment) =>
          attachment.id === id ? { ...attachment, name } : attachment
        )
      );
    };

    const handlePaste = useCallback(
      (event: React.ClipboardEvent<HTMLDivElement>) => {
        if (disabled) return;

        const files = getClipboardFiles(event.clipboardData);
        const clipboardText = event.clipboardData?.getData("text/plain") ?? "";
        const curlFile = createCurlAttachmentFile(clipboardText);

        if (!curlFile && files.length === 0) return;

        event.preventDefault();
        files.forEach((file) => onAttachmentUpload(file));
        if (curlFile) {
          addPendingCurlAttachment(curlFile);
        }
      },
      [addPendingCurlAttachment, disabled, onAttachmentUpload]
    );

    return (
      <div className={`rounded-r-lg flex flex-col`} onBlur={handleResultBlur}>
        <div className="flex items-center gap-2 mb-2">
          <Title text={title} color={`var(--color-${colorType})`} />
        </div>
        <ResultTextarea
          ref={ref}
          placeholder={`Опишите ${title}...`}
          value={value || ""}
          onBlur={onBlur}
          autoFocus={autoFocus}
          onInput={onInput}
          onPaste={handlePaste}
        />
        {pendingAttachments.length > 0 && (
          <div className="mt-2 flex flex-wrap gap-2">
            {pendingAttachments.map((attachment, index) => (
              <AttachmentChip
                key={attachment.id}
                name={attachment.name}
                kind={attachment.kind}
                disabled={disabled}
                onRename={(name) =>
                  handleRenamePendingAttachment(attachment.id, name)
                }
                onRemove={() => handleRemovePendingAttachment(index)}
              />
            ))}
          </div>
        )}
        {reportId && bugId && (
          <FilePreview
            attachments={attachments}
            reportId={reportId}
            bugId={bugId}
            attachType={attachType}
            onAttachmentUpload={handleAttachmentUpload}
            onAttachmentDelete={onAttachmentDelete}
            onAttachmentRename={onAttachmentRename}
            disabled={disabled}
            currentUserId={currentUser?.id}
          />
        )}
      </div>
    );
  }
);

export default Result;
