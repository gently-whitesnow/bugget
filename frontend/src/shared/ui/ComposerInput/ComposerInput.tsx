import { KeyboardEvent, useEffect, useRef, useState, useCallback } from "react";
import { ArrowUp, Paperclip } from "lucide-react";
import MarkdownTextarea from "@/shared/ui/MarkdownTextarea";
import {
  copyToClipboard,
  createCurlAttachmentFile,
  getClipboardFiles,
} from "@/shared/lib";
import AttachmentChip from "./components/AttachmentChip";
import { PendingAttachment } from "./types";

const imageFileNamePattern = /\.(jpe?g|png|gif|webp)$/i;

type Props = {
  value: string;
  onChange: (value: string) => void;
  onSend: (attachments: File[]) => void;
  disabled?: boolean;
  placeholder?: string;
  autoFocus?: boolean;
  isSubmitting?: boolean;
  isSendDisabled?: boolean;
  enableAttachments?: boolean;
  maxLength?: number;
};

const ComposerInput = ({
  value,
  onChange,
  onSend,
  disabled = false,
  placeholder,
  autoFocus = false,
  isSubmitting = false,
  isSendDisabled,
  enableAttachments = false,
  maxLength,
}: Props) => {
  const textareaRef = useRef<HTMLDivElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const nextAttachmentIdRef = useRef(0);
  const attachmentsRef = useRef<PendingAttachment[]>([]);
  const [attachments, setAttachments] = useState<PendingAttachment[]>([]);

  const createAttachment = useCallback(
    (file: File, kind: PendingAttachment["kind"] = "file") => ({
      id: nextAttachmentIdRef.current++,
      file,
      name: file.name,
      kind,
      previewUrl: imageFileNamePattern.test(file.name)
        ? URL.createObjectURL(file)
        : null,
    }),
    []
  );

  const getFileForUpload = (attachment: PendingAttachment) => {
    if (attachment.name === attachment.file.name) return attachment.file;

    return new File([attachment.file], attachment.name, {
      type: attachment.file.type,
      lastModified: attachment.file.lastModified,
    });
  };

  useEffect(() => {
    attachmentsRef.current = attachments;
  }, [attachments]);

  useEffect(() => {
    return () => {
      attachmentsRef.current.forEach((attachment) => {
        if (attachment.previewUrl) {
          URL.revokeObjectURL(attachment.previewUrl);
        }
      });
    };
  }, []);

  const handleKeyDown = (e: KeyboardEvent<HTMLDivElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      if (!isSendDisabled && !disabled && !isSubmitting) {
        handleSend();
      }
    }
  };

  const handleSend = () => {
    onSend(attachments.map(getFileForUpload));
    attachments.forEach((attachment) => {
      if (attachment.previewUrl) {
        URL.revokeObjectURL(attachment.previewUrl);
      }
    });
    setAttachments([]);
  };

  const handleFileSelect = () => {
    fileInputRef.current?.click();
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (files?.length) {
      const nextAttachments = Array.from(files).map((file) =>
        createAttachment(file)
      );

      setAttachments((prev) => [...prev, ...nextAttachments]);
    }
    // Сбрасываем значение чтобы можно было выбрать тот же файл снова
    e.target.value = "";
  };

  const handleRemoveAttachment = (index: number) => {
    setAttachments((prev) => {
      const attachment = prev[index];
      if (attachment?.previewUrl) {
        URL.revokeObjectURL(attachment.previewUrl);
      }
      return prev.filter((_, i) => i !== index);
    });
  };

  const handleRenameAttachment = (id: number, name: string) => {
    setAttachments((prev) =>
      prev.map((attachment) =>
        attachment.id === id ? { ...attachment, name } : attachment
      )
    );
  };

  const handleCopyAttachment = async (attachment: PendingAttachment) => {
    const text = await attachment.file.text();
    await copyToClipboard(text);
  };

  const handlePaste = useCallback(
    (event: React.ClipboardEvent<HTMLDivElement>) => {
      if (!enableAttachments) return;

      const files = getClipboardFiles(event.clipboardData);
      const clipboardText = event.clipboardData?.getData("text/plain") ?? "";
      const curlFile = createCurlAttachmentFile(clipboardText);
      if (!curlFile && files.length === 0) return;

      const pastedAttachments = [
        ...files.map((file) => createAttachment(file)),
        ...(curlFile ? [createAttachment(curlFile, "curl")] : []),
      ];

      event.preventDefault();
      setAttachments((prev) => [...prev, ...pastedAttachments]);
    },
    [createAttachment, enableAttachments]
  );

  // Default disabled logic if isSendDisabled is not provided: empty value and no attachments
  const sendButtonDisabled =
    isSendDisabled !== undefined
      ? isSendDisabled
      : !value.trim() && attachments.length === 0;

  const isDisabled = disabled || isSubmitting;

  return (
    <div className="flex flex-col gap-2">
      <div className="flex gap-2 items-center">
        <MarkdownTextarea
          ref={textareaRef}
          value={value}
          onInput={onChange}
          onKeyDown={handleKeyDown}
          onPaste={handlePaste}
          placeholder={placeholder}
          className="textarea textarea-bordered resize-none min-h-auto flex-1 focus:outline-none"
          style={{ minHeight: "2.5rem" }}
          maxLength={maxLength}
          autoFocus={autoFocus}
        />
        <div className="flex flex-row gap-1 items-center">
          <button
            type="button"
            className="btn btn-primary p-2 btn-circle"
            onClick={handleSend}
            disabled={isDisabled || sendButtonDisabled}
            title="Отправить"
          >
            <ArrowUp />
          </button>
          {enableAttachments && (
            <>
              <button
                type="button"
                className="btn btn-ghost p-2 btn-circle text-base-content/70 hover:text-base-content"
                onClick={handleFileSelect}
                disabled={isDisabled}
                title="Прикрепить файл"
              >
                <Paperclip className="w-5 h-5" />
              </button>
              <input
                ref={fileInputRef}
                type="file"
                multiple
                className="sr-only"
                onChange={handleFileChange}
                tabIndex={-1}
              />
            </>
          )}
        </div>
      </div>

      {attachments.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {attachments.map((attachment, index) => (
            <AttachmentChip
              key={attachment.id}
              name={attachment.name}
              kind={attachment.kind}
              previewUrl={attachment.previewUrl}
              disabled={isDisabled}
              onRename={(name) => handleRenameAttachment(attachment.id, name)}
              onRemove={() => handleRemoveAttachment(index)}
              onCopy={
                attachment.kind === "curl"
                  ? () => handleCopyAttachment(attachment)
                  : undefined
              }
            />
          ))}
        </div>
      )}
    </div>
  );
};

export default ComposerInput;
