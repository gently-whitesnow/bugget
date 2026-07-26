import { KeyboardEvent, useEffect, useRef, useState } from "react";
import {
  Check,
  Copy,
  FileText,
  Loader2,
  Pencil,
  Terminal,
  Trash2,
} from "lucide-react";

import { PendingAttachment } from "../types";

const fileExtensionPattern = /\.[^./\\]+$/;

type Props = {
  name: string;
  kind: PendingAttachment["kind"];
  previewUrl?: string | null;
  disabled?: boolean;
  autoEdit?: boolean;
  canRename?: boolean;
  canRemove?: boolean;
  onRename: (name: string) => void | Promise<unknown>;
  onRemove: () => void;
  onCommit?: (name: string) => void;
  onOpen?: () => void;
  onCopy?: () => void | Promise<unknown>;
};

const AttachmentChip = ({
  name,
  kind,
  previewUrl,
  disabled = false,
  autoEdit = false,
  canRename: canRenameProp,
  canRemove = true,
  onRename,
  onRemove,
  onCommit,
  onOpen,
  onCopy,
}: Props) => {
  const [isEditing, setIsEditing] = useState(false);
  const [draftName, setDraftName] = useState(name);
  const [editingWidth, setEditingWidth] = useState<number | null>(null);
  const [isCopying, setIsCopying] = useState(false);
  const [isCopied, setIsCopied] = useState(false);
  const chipRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const isCurl = kind === "curl";
  const canRename = canRenameProp ?? (isCurl && !disabled);
  const canOpen = !!onOpen && !disabled;
  const canCopy = isCurl && !!onCopy && !disabled;
  const inputWidth = `${Math.min(
    Math.max(draftName.length + 1, isCurl ? 10 : 14),
    isCurl ? 24 : 32
  )}ch`;

  const normalizeFileName = (value: string) => {
    if (!isCurl || fileExtensionPattern.test(value)) return value;

    return `${value}.txt`;
  };

  useEffect(() => {
    if (!isEditing) return;

    inputRef.current?.focus();
    inputRef.current?.select();
  }, [isEditing]);

  useEffect(() => {
    if (!autoEdit || !canRename) return;

    setIsEditing(true);
  }, [autoEdit, canRename]);

  useEffect(() => {
    if (isEditing) return;

    setDraftName(name);
  }, [isEditing, name]);

  const commitRename = () => {
    const nextName = normalizeFileName(draftName.trim());
    if (nextName) {
      setDraftName(nextName);
      onRename(nextName);
      onCommit?.(nextName);
    } else {
      setDraftName(name);
    }
    setIsEditing(false);
    setEditingWidth(null);
  };

  const cancelRename = () => {
    setDraftName(name);
    setIsEditing(false);
    setEditingWidth(null);
  };

  const handleInputKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    event.stopPropagation();

    if (event.key === "Enter") {
      event.preventDefault();
      commitRename();
      return;
    }

    if (event.key === "Escape") {
      event.preventDefault();
      cancelRename();
    }
  };

  const startEditing = () => {
    if (!canRename) return;

    setEditingWidth(chipRef.current?.getBoundingClientRect().width ?? null);
    setIsEditing(true);
  };

  const handleNameClick = () => {
    if (canOpen) {
      onOpen?.();
      return;
    }

    startEditing();
  };

  const handleCopy = async () => {
    if (!canCopy || isCopying) return;

    setIsCopying(true);
    try {
      await onCopy?.();
      setIsCopied(true);
      window.setTimeout(() => setIsCopied(false), 1500);
    } catch {
      setIsCopied(false);
    } finally {
      setIsCopying(false);
    }
  };

  const nameTitle = canOpen ? name : canRename ? "Переименовать файл" : name;

  return (
    <div
      ref={chipRef}
      className={`group inline-flex min-w-0 max-w-full items-center overflow-hidden rounded-md border transition-colors duration-200 ${
        isCurl
          ? "h-6 gap-1.5 border-info/30 bg-info/10 px-2 text-xs leading-5 text-info shadow-sm shadow-info/5"
          : "min-h-7 gap-1.5 border-base-300 bg-base-200 px-2 py-1 text-xs text-base-content"
      }`}
      style={editingWidth ? { width: editingWidth } : undefined}
    >
      {isCurl ? (
        <Terminal className="h-3.5 w-3.5 shrink-0" />
      ) : previewUrl ? (
        <img
          src={previewUrl}
          alt={name}
          className="h-5 w-5 shrink-0 rounded object-cover"
        />
      ) : (
        <FileText className="h-3.5 w-3.5 shrink-0 text-base-content/70" />
      )}

      {isCurl && (
        <span className="rounded bg-info/15 px-1 font-mono text-[0.65rem] leading-4">
          curl
        </span>
      )}

      {isEditing ? (
        <input
          ref={inputRef}
          value={draftName}
          onChange={(event) => setDraftName(event.target.value)}
          onBlur={commitRename}
          onKeyDown={handleInputKeyDown}
          className={`min-h-0 max-w-[45vw] rounded border px-1.5 text-xs outline-none transition-colors duration-200 ${
            isCurl
              ? "h-4 leading-4 border-info/30 bg-info/5 text-info focus:border-info/50 focus:ring-0"
              : "h-5 leading-5 border-base-300 bg-base-100 text-base-content focus:border-info focus:ring-1 focus:ring-info/30"
          }`}
          style={{ width: editingWidth ? "100%" : inputWidth }}
          disabled={disabled}
          aria-label="Название файла"
        />
      ) : (
        <button
          type="button"
          className="min-w-0 max-w-48 cursor-pointer truncate text-left font-medium hover:underline disabled:cursor-default disabled:hover:no-underline"
          onClick={handleNameClick}
          disabled={!canRename && !canOpen}
          title={nameTitle}
        >
          {name}
        </button>
      )}

      {canRename && !isEditing && (
        <button
          type="button"
          className="cursor-pointer rounded p-0.5 text-base-content/50 transition-colors duration-200 hover:bg-base-300 hover:text-base-content"
          onClick={startEditing}
          title="Переименовать файл"
          aria-label="Переименовать файл"
        >
          <Pencil className="h-3 w-3" />
        </button>
      )}

      {canCopy && !isEditing && (
        <button
          type="button"
          className="cursor-pointer rounded p-0.5 text-info/70 transition-colors duration-200 hover:bg-info/10 hover:text-info disabled:cursor-default disabled:opacity-50"
          onClick={handleCopy}
          title={isCopied ? "Curl скопирован" : "Скопировать curl"}
          aria-label={isCopied ? "Curl скопирован" : "Скопировать curl"}
          disabled={isCopying}
        >
          {isCopying ? (
            <Loader2 className="h-3 w-3 animate-spin" />
          ) : isCopied ? (
            <Check className="h-3 w-3" />
          ) : (
            <Copy className="h-3 w-3" />
          )}
        </button>
      )}

      <button
        type="button"
        className="cursor-pointer rounded p-0.5 text-base-content/50 transition-colors duration-200 hover:bg-error/10 hover:text-error disabled:cursor-default disabled:opacity-40"
        onClick={onRemove}
        title="Убрать файл"
        aria-label="Убрать файл"
        disabled={disabled || !canRemove}
      >
        <Trash2 className="h-3 w-3" />
      </button>
    </div>
  );
};

export default AttachmentChip;
